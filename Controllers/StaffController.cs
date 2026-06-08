using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SyncBook.Server.Data;
using SyncBook.Server.Mapping;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;
using SyncBook.Server.Services;
using SyncBook.Server.Validation;

namespace SyncBook.Server.Controllers;

[ApiController]
public class StaffController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly CurrentUserService _currentUser;

    public StaffController(MongoDbContext db, CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("/api/staff")]
    public async Task<ActionResult<List<StaffMemberDto>>> GetByBusiness(
        [FromQuery] string businessId,
        [FromQuery] bool bookableOnly = false)
    {
        if (string.IsNullOrWhiteSpace(businessId))
        {
            return BadRequest(new { message = "Business ID is required." });
        }

        var business = await _db.Businesses.Find(b => b.Id == businessId).FirstOrDefaultAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        var filter = Builders<StaffMember>.Filter.Eq(s => s.BusinessId, businessId);
        if (bookableOnly)
        {
            filter &= Builders<StaffMember>.Filter.Eq(s => s.IsBookable, true);
        }

        var staffMembers = await _db.StaffMembers.Find(filter).ToListAsync();
        var dtos = await MapStaffWithNamesAsync(staffMembers);

        return Ok(dtos);
    }

    [HttpGet("/api/businesses/me/staff/me")]
    [Authorize(Policy = "BusinessOwner")]
    public async Task<ActionResult<StaffMemberDto>> GetMyStaffProfile()
    {
        var businessId = _currentUser.BusinessId;
        var userId = _currentUser.UserId;

        if (string.IsNullOrEmpty(businessId) || string.IsNullOrEmpty(userId))
        {
            return Forbid();
        }

        var staff = await _db.StaffMembers
            .Find(s => s.BusinessId == businessId && s.UserId == userId)
            .FirstOrDefaultAsync();

        if (staff is null)
        {
            return NotFound(new { message = "Staff profile not found." });
        }

        var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        return Ok(BusinessMapper.ToStaffDto(staff, user?.FullName ?? string.Empty));
    }

    [HttpPut("/api/businesses/me/staff/me")]
    [Authorize(Policy = "BusinessOwner")]
    public async Task<ActionResult<StaffMemberDto>> UpdateMyStaffProfile(
        [FromBody] UpdateMyStaffScheduleRequest request)
    {
        var businessId = _currentUser.BusinessId;
        var userId = _currentUser.UserId;

        if (string.IsNullOrEmpty(businessId) || string.IsNullOrEmpty(userId))
        {
            return Forbid();
        }

        var validationError = StaffWeeklyScheduleValidator.Validate(request.WeeklySchedule);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var staff = await _db.StaffMembers
            .Find(s => s.BusinessId == businessId && s.UserId == userId)
            .FirstOrDefaultAsync();

        if (staff is null)
        {
            return NotFound(new { message = "Staff profile not found." });
        }

        var weeklySchedule = request.WeeklySchedule.Select(e => new StaffWeeklyScheduleEntry
        {
            DayOfWeek = e.DayOfWeek,
            StartTime = e.IsAvailable ? e.StartTime : null,
            EndTime = e.IsAvailable ? e.EndTime : null,
            IsAvailable = e.IsAvailable
        }).ToList();

        await _db.StaffMembers.UpdateOneAsync(
            s => s.Id == staff.Id,
            Builders<StaffMember>.Update
                .Set(s => s.IsBookable, request.IsBookable)
                .Set(s => s.WeeklySchedule, weeklySchedule));

        staff.IsBookable = request.IsBookable;
        staff.WeeklySchedule = weeklySchedule;

        var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        return Ok(BusinessMapper.ToStaffDto(staff, user?.FullName ?? string.Empty));
    }

    private async Task<List<StaffMemberDto>> MapStaffWithNamesAsync(List<StaffMember> staffMembers)
    {
        if (staffMembers.Count == 0)
        {
            return [];
        }

        var userIds = staffMembers.Select(s => s.UserId).Distinct().ToList();
        var users = await _db.Users
            .Find(u => userIds.Contains(u.Id))
            .ToListAsync();

        var userNames = users.ToDictionary(u => u.Id, u => u.FullName);

        return staffMembers
            .Select(s => BusinessMapper.ToStaffDto(
                s,
                userNames.GetValueOrDefault(s.UserId, string.Empty)))
            .ToList();
    }
}
