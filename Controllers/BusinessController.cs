using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SyncBook.Server.Data;
using SyncBook.Server.Mapping;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;
using SyncBook.Server.Services;

namespace SyncBook.Server.Controllers;

[ApiController]
[Route("api/businesses")]
public class BusinessController : ControllerBase
{
    private readonly MongoDbContext _db;

    public BusinessController(MongoDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<BusinessPublicDto>>> GetAll()
    {
        var businesses = await _db.Businesses
            .Find(FilterDefinition<Business>.Empty)
            .SortBy(b => b.Name)
            .ToListAsync();

        return Ok(businesses.Select(BusinessMapper.ToPublicDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BusinessPublicDto>> GetById(string id)
    {
        var business = await _db.Businesses
            .Find(b => b.Id == id)
            .FirstOrDefaultAsync();

        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        return Ok(BusinessMapper.ToPublicDto(business));
    }

    [HttpGet("{id}/slots")]
    public async Task<ActionResult<InternalSlotsResponse>> GetSlots(
        string id,
        [FromQuery] DateTime date,
        [FromQuery] string serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            return BadRequest(new { message = "Service ID is required." });
        }

        var business = await _db.Businesses.Find(b => b.Id == id).FirstOrDefaultAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        if (AppointmentSchedulingHelper.FindService(business, serviceId) is null)
        {
            return BadRequest(new { message = "Service not found." });
        }

        var staffMembers = await _db.StaffMembers
            .Find(s => s.BusinessId == id)
            .ToListAsync();

        var dayStart = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var dayAppointments = await _db.Appointments
            .Find(a => a.BusinessId == id && a.StartUtc >= dayStart && a.StartUtc < dayEnd)
            .ToListAsync();

        var slots = AppointmentSchedulingHelper.GenerateStaffAwareSlots(
            business,
            date,
            serviceId,
            dayAppointments,
            staffMembers,
            null);

        return Ok(new InternalSlotsResponse { Slots = slots });
    }

    [HttpGet("{id}/appointments/today")]
    public async Task<ActionResult<List<TodayAppointmentSummaryDto>>> GetTodayAppointments(string id)
    {
        var business = await _db.Businesses.Find(b => b.Id == id).FirstOrDefaultAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        var today = DateTime.UtcNow.Date;
        var dayStart = DateTime.SpecifyKind(today, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var appointments = await _db.Appointments
            .Find(a => a.BusinessId == id
                && a.StartUtc >= dayStart
                && a.StartUtc < dayEnd
                && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .SortBy(a => a.StartUtc)
            .ToListAsync();

        return Ok(appointments.Select(a => new TodayAppointmentSummaryDto
        {
            Id = a.Id,
            StartUtc = a.StartUtc,
            EndUtc = a.EndUtc,
            ServiceName = a.ServiceName,
            Status = a.Status
        }).ToList());
    }
}
