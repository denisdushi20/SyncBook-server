using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SyncBook.Server.Data;
using SyncBook.Server.Mapping;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;
using SyncBook.Server.Services;

namespace SyncBook.Server.Controllers;

[ApiController]
[Route("api/businesses/me/appointments")]
[Authorize(Policy = "BusinessOwner")]
public class AppointmentsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly CurrentUserService _currentUser;
    private readonly AvailabilityChangeDispatcher _availabilityDispatcher;

    public AppointmentsController(
        MongoDbContext db,
        CurrentUserService currentUser,
        AvailabilityChangeDispatcher availabilityDispatcher)
    {
        _db = db;
        _currentUser = currentUser;
        _availabilityDispatcher = availabilityDispatcher;
    }

    [HttpGet]
    public async Task<ActionResult<List<AppointmentDto>>> GetForRange(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var businessId = _currentUser.BusinessId;
        if (string.IsNullOrEmpty(businessId))
        {
            return Forbid();
        }

        if (from >= to)
        {
            return BadRequest(new { message = "The 'from' date must be before the 'to' date." });
        }

        var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to, DateTimeKind.Utc);

        var appointments = await _db.Appointments
            .Find(a => a.BusinessId == businessId && a.StartUtc >= fromUtc && a.StartUtc < toUtc)
            .SortBy(a => a.StartUtc)
            .ToListAsync();

        var staffNames = await ResolveStaffNamesAsync(appointments);
        return Ok(appointments.Select(a => BusinessMapper.ToDto(
            a,
            a.StaffId is not null ? staffNames.GetValueOrDefault(a.StaffId) : null)).ToList());
    }

    [HttpGet("/api/appointments/internal/slots")]
    public async Task<ActionResult<InternalSlotsResponse>> GetInternalSlots(
        [FromQuery] DateTime date,
        [FromQuery] string serviceId,
        [FromQuery] string? staffId = null)
    {
        var businessId = _currentUser.BusinessId;
        if (string.IsNullOrEmpty(businessId))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(serviceId))
        {
            return BadRequest(new { message = "Service ID is required." });
        }

        var business = await _db.Businesses.Find(b => b.Id == businessId).FirstOrDefaultAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        if (AppointmentSchedulingHelper.FindService(business, serviceId) is null)
        {
            return BadRequest(new { message = "Service not found." });
        }

        var staffMembers = await _db.StaffMembers
            .Find(s => s.BusinessId == businessId)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(staffId)
            && staffMembers.All(s => s.Id != staffId))
        {
            return BadRequest(new { message = "Staff member not found." });
        }

        var dayStart = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var dayAppointments = await _db.Appointments
            .Find(a => a.BusinessId == businessId && a.StartUtc >= dayStart && a.StartUtc < dayEnd)
            .ToListAsync();

        var slots = AppointmentSchedulingHelper.GenerateStaffAwareSlots(
            business,
            date,
            serviceId,
            dayAppointments,
            staffMembers,
            staffId);

        return Ok(new InternalSlotsResponse { Slots = slots });
    }

    [HttpPost("/api/appointments/internal")]
    public async Task<ActionResult<AppointmentDto>> CreateInternal(
        [FromBody] CreateInternalAppointmentRequest request)
    {
        var businessId = _currentUser.BusinessId;
        if (string.IsNullOrEmpty(businessId))
        {
            return Forbid();
        }

        if (!string.Equals(request.BusinessId, businessId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName)
            || string.IsNullOrWhiteSpace(request.CustomerEmail)
            || string.IsNullOrWhiteSpace(request.CustomerPhone)
            || string.IsNullOrWhiteSpace(request.ServiceId)
            || string.IsNullOrWhiteSpace(request.StaffId))
        {
            return BadRequest(new { message = "Customer name, email, phone, service, and staff are required." });
        }

        var startUtc = DateTime.SpecifyKind(request.StartTime, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(request.EndTime, DateTimeKind.Utc);

        if (startUtc >= endUtc)
        {
            return BadRequest(new { message = "Start time must be before end time." });
        }

        var business = await _db.Businesses.Find(b => b.Id == businessId).FirstOrDefaultAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        var service = AppointmentSchedulingHelper.FindService(business, request.ServiceId);
        if (service is null)
        {
            return BadRequest(new { message = "Service not found." });
        }

        var staff = await _db.StaffMembers
            .Find(s => s.Id == request.StaffId && s.BusinessId == businessId)
            .FirstOrDefaultAsync();

        if (staff is null)
        {
            return BadRequest(new { message = "Staff member not found." });
        }

        if (!staff.IsBookable)
        {
            return BadRequest(new { message = "The selected staff member is not bookable." });
        }

        if (!StaffScheduleCoverageHelper.CoversInterval(staff, startUtc, endUtc))
        {
            return BadRequest(new { message = "The selected time is outside the staff member's working hours." });
        }

        var expectedDuration = AppointmentSchedulingHelper.ResolveDurationMinutes(service);
        var actualDuration = (endUtc - startUtc).TotalMinutes;
        if (Math.Abs(actualDuration - expectedDuration) > 1)
        {
            return BadRequest(new { message = "Appointment duration does not match the selected service." });
        }

        var bufferMinutes = AppointmentSchedulingHelper.ResolveBufferMinutes(request.CustomBufferMinutes);

        var dayStart = DateTime.SpecifyKind(startUtc.Date, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var dayAppointments = await _db.Appointments
            .Find(a => a.BusinessId == businessId
                && a.StartUtc >= dayStart
                && a.StartUtc < dayEnd
                && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .ToListAsync();

        if (StaffPoolCapacityCalculator.HasLegacyBusinessConflict(dayAppointments, startUtc, endUtc))
        {
            return Conflict(new { message = "The selected time slot conflicts with an existing appointment." });
        }

        if (!StaffPoolCapacityCalculator.IsStaffAvailable(staff, startUtc, endUtc, dayAppointments))
        {
            return Conflict(new { message = "The selected time slot conflicts with an existing appointment for this staff member." });
        }

        var appointment = new Appointment
        {
            BusinessId = businessId,
            CustomerName = request.CustomerName.Trim(),
            CustomerEmail = request.CustomerEmail.Trim(),
            CustomerPhone = request.CustomerPhone.Trim(),
            ServiceId = request.ServiceId,
            ServiceName = service.Name,
            ServicePrice = service.Price ?? 0,
            StaffId = staff.Id,
            StartUtc = startUtc,
            EndUtc = endUtc,
            BufferMinutes = bufferMinutes,
            Status = AppointmentStatus.Confirmed
        };

        await _db.Appointments.InsertOneAsync(appointment);

        await _availabilityDispatcher.DispatchAsync(
            appointment.BusinessId,
            appointment.StartUtc,
            appointment.EndUtc);

        var staffUser = await _db.Users.Find(u => u.Id == staff.UserId).FirstOrDefaultAsync();
        return Ok(BusinessMapper.ToDto(appointment, staffUser?.FullName));
    }

    [HttpPatch("{id}/status")]
    public async Task<ActionResult<AppointmentDto>> UpdateStatus(
        string id,
        [FromBody] UpdateAppointmentStatusRequest request)
    {
        var businessId = _currentUser.BusinessId;
        if (string.IsNullOrEmpty(businessId))
        {
            return Forbid();
        }

        if (request.Status is not (AppointmentStatus.Confirmed
            or AppointmentStatus.Cancelled
            or AppointmentStatus.Completed))
        {
            return BadRequest(new { message = "Only Confirmed, Cancelled, or Completed status transitions are supported." });
        }

        var appointment = await _db.Appointments
            .Find(a => a.Id == id && a.BusinessId == businessId)
            .FirstOrDefaultAsync();

        if (appointment is null)
        {
            return NotFound(new { message = "Appointment not found." });
        }

        if (request.Status == AppointmentStatus.Completed)
        {
            if (appointment.Status != AppointmentStatus.Confirmed)
            {
                return BadRequest(new { message = "Only confirmed appointments can be marked as paid." });
            }

            await _db.Appointments.UpdateOneAsync(
                a => a.Id == id,
                Builders<Appointment>.Update.Set(a => a.Status, AppointmentStatus.Completed));

            appointment.Status = AppointmentStatus.Completed;

            await _availabilityDispatcher.DispatchAsync(
                appointment.BusinessId,
                appointment.StartUtc,
                appointment.EndUtc);
        }
        else if (appointment.Status == AppointmentStatus.Pending)
        {
            if (request.Status == AppointmentStatus.Confirmed)
            {
                await _db.Appointments.UpdateOneAsync(
                    a => a.Id == id,
                    Builders<Appointment>.Update.Set(a => a.Status, AppointmentStatus.Confirmed));

                appointment.Status = AppointmentStatus.Confirmed;
            }
            else
            {
                await _db.Appointments.UpdateOneAsync(
                    a => a.Id == id,
                    Builders<Appointment>.Update.Set(a => a.Status, AppointmentStatus.Cancelled));

                appointment.Status = AppointmentStatus.Cancelled;

                await _availabilityDispatcher.DispatchAsync(
                    appointment.BusinessId,
                    appointment.StartUtc,
                    appointment.EndUtc);
            }
        }
        else
        {
            return BadRequest(new { message = "This appointment cannot be updated to the requested status." });
        }

        string? staffName = null;
        if (!string.IsNullOrEmpty(appointment.StaffId))
        {
            var staff = await _db.StaffMembers.Find(s => s.Id == appointment.StaffId).FirstOrDefaultAsync();
            if (staff is not null)
            {
                var staffUser = await _db.Users.Find(u => u.Id == staff.UserId).FirstOrDefaultAsync();
                staffName = staffUser?.FullName;
            }
        }

        return Ok(BusinessMapper.ToDto(appointment, staffName));
    }

    private async Task<Dictionary<string, string>> ResolveStaffNamesAsync(List<Appointment> appointments)
    {
        var staffIds = appointments
            .Where(a => !string.IsNullOrEmpty(a.StaffId))
            .Select(a => a.StaffId!)
            .Distinct()
            .ToList();

        if (staffIds.Count == 0)
        {
            return [];
        }

        var staffMembers = await _db.StaffMembers
            .Find(s => staffIds.Contains(s.Id))
            .ToListAsync();

        var userIds = staffMembers.Select(s => s.UserId).Distinct().ToList();
        var users = await _db.Users.Find(u => userIds.Contains(u.Id)).ToListAsync();
        var userNames = users.ToDictionary(u => u.Id, u => u.FullName);

        return staffMembers.ToDictionary(
            s => s.Id,
            s => userNames.GetValueOrDefault(s.UserId, string.Empty));
    }
}

