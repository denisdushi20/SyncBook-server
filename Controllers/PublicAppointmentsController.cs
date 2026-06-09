using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SyncBook.Server.Data;
using SyncBook.Server.Mapping;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;
using SyncBook.Server.Services;

namespace SyncBook.Server.Controllers;

[ApiController]
[Route("api/appointments")]
public class PublicAppointmentsController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly BookingAlertDispatcher _bookingAlertDispatcher;
    private readonly AvailabilityChangeDispatcher _availabilityDispatcher;

    public PublicAppointmentsController(
        MongoDbContext db,
        BookingAlertDispatcher bookingAlertDispatcher,
        AvailabilityChangeDispatcher availabilityDispatcher)
    {
        _db = db;
        _bookingAlertDispatcher = bookingAlertDispatcher;
        _availabilityDispatcher = availabilityDispatcher;
    }

    [HttpPost("public")]
    public async Task<ActionResult<AppointmentDto>> CreatePublic(
        [FromBody] CreatePublicAppointmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BusinessId)
            || string.IsNullOrWhiteSpace(request.CustomerName)
            || string.IsNullOrWhiteSpace(request.CustomerEmail)
            || string.IsNullOrWhiteSpace(request.CustomerPhone)
            || string.IsNullOrWhiteSpace(request.ServiceId))
        {
            return BadRequest(new { message = "Business, customer name, email, phone, and service are required." });
        }

        var startUtc = DateTime.SpecifyKind(request.StartTime, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(request.EndTime, DateTimeKind.Utc);

        if (startUtc >= endUtc)
        {
            return BadRequest(new { message = "Start time must be before end time." });
        }

        var business = await _db.Businesses.Find(b => b.Id == request.BusinessId).FirstOrDefaultAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        var service = AppointmentSchedulingHelper.FindService(business, request.ServiceId);
        if (service is null)
        {
            return BadRequest(new { message = "Service not found." });
        }

        var expectedDuration = AppointmentSchedulingHelper.ResolveDurationMinutes(service);
        var actualDuration = (endUtc - startUtc).TotalMinutes;
        if (Math.Abs(actualDuration - expectedDuration) > 1)
        {
            return BadRequest(new { message = "Appointment duration does not match the selected service." });
        }

        var bufferMinutes = AppointmentSchedulingHelper.DefaultBufferMinutes;

        var staffMembers = await _db.StaffMembers
            .Find(s => s.BusinessId == request.BusinessId && s.IsBookable)
            .ToListAsync();

        var dayStart = DateTime.SpecifyKind(startUtc.Date, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var dayAppointments = await _db.Appointments
            .Find(a => a.BusinessId == request.BusinessId
                && a.StartUtc >= dayStart
                && a.StartUtc < dayEnd
                && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .ToListAsync();

        if (StaffPoolCapacityCalculator.HasLegacyBusinessConflict(dayAppointments, startUtc, endUtc))
        {
            return Conflict(new { message = "The selected time slot conflicts with an existing appointment." });
        }

        var assignedStaff = StaffPoolCapacityCalculator.SelectStaffForBooking(
            staffMembers,
            startUtc,
            endUtc,
            dayAppointments);

        if (assignedStaff is null)
        {
            return Conflict(new { message = "No staff members are available for the selected time slot." });
        }

        var freshDayAppointments = await _db.Appointments
            .Find(a => a.BusinessId == request.BusinessId
                && a.StartUtc >= dayStart
                && a.StartUtc < dayEnd
                && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .ToListAsync();

        if (StaffPoolCapacityCalculator.HasLegacyBusinessConflict(freshDayAppointments, startUtc, endUtc))
        {
            return Conflict(new { message = "The selected time slot conflicts with an existing appointment." });
        }

        var recheckedStaff = StaffPoolCapacityCalculator.SelectStaffForBooking(
            staffMembers,
            startUtc,
            endUtc,
            freshDayAppointments);

        if (recheckedStaff is null)
        {
            return Conflict(new { message = "No staff members are available for the selected time slot." });
        }

        var appointment = new Appointment
        {
            BusinessId = request.BusinessId,
            CustomerName = request.CustomerName.Trim(),
            CustomerEmail = request.CustomerEmail.Trim(),
            CustomerPhone = request.CustomerPhone.Trim(),
            ServiceId = request.ServiceId,
            ServiceName = service.Name,
            ServicePrice = service.Price ?? 0,
            StaffId = recheckedStaff.Id,
            StartUtc = startUtc,
            EndUtc = endUtc,
            BufferMinutes = bufferMinutes,
            Status = AppointmentStatus.Pending
        };

        await _db.Appointments.InsertOneAsync(appointment);

        await _bookingAlertDispatcher.DispatchAsync(appointment, "PublicForm");
        await _availabilityDispatcher.DispatchAsync(
            appointment.BusinessId,
            appointment.StartUtc,
            appointment.EndUtc);

        var staffUser = await _db.Users.Find(u => u.Id == recheckedStaff.UserId).FirstOrDefaultAsync();
        return Ok(BusinessMapper.ToDto(appointment, staffUser?.FullName));
    }
}
