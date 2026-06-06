using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SyncBook.Server.Data;
using SyncBook.Server.Mapping;
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

    public AppointmentsController(MongoDbContext db, CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
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

        return Ok(appointments.Select(BusinessMapper.ToDto).ToList());
    }
}
