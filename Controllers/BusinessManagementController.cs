using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SyncBook.Server.Data;
using SyncBook.Server.Mapping;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;
using SyncBook.Server.Services;
using SyncBook.Server.Validation;

namespace SyncBook.Server.Controllers;

[ApiController]
[Route("api/businesses/me")]
[Authorize(Policy = "BusinessOwner")]
public class BusinessManagementController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly CurrentUserService _currentUser;
    private readonly AvailabilityChangeDispatcher _availabilityDispatcher;

    public BusinessManagementController(
        MongoDbContext db,
        CurrentUserService currentUser,
        AvailabilityChangeDispatcher availabilityDispatcher)
    {
        _db = db;
        _currentUser = currentUser;
        _availabilityDispatcher = availabilityDispatcher;
    }

    [HttpGet]
    public async Task<ActionResult<BusinessPublicDto>> GetMyBusiness()
    {
        var business = await FindOwnedBusinessAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        return Ok(BusinessMapper.ToPublicDto(business));
    }

    [HttpPut("working-hours")]
    public async Task<ActionResult<BusinessPublicDto>> UpdateWorkingHours(
        [FromBody] UpdateWorkingHoursRequest request)
    {
        var validationError = WorkingHoursValidator.Validate(request.WorkingHours);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var business = await FindOwnedBusinessAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        var workingHours = request.WorkingHours
            .Select(d => new DaySchedule
            {
                Day = d.Day,
                IsOpen = d.IsOpen,
                OpenTime = d.IsOpen ? d.OpenTime : null,
                CloseTime = d.IsOpen ? d.CloseTime : null
            })
            .ToList();

        await _db.Businesses.UpdateOneAsync(
            b => b.Id == business.Id,
            Builders<Business>.Update.Set(b => b.WorkingHours, workingHours));

        business.WorkingHours = workingHours;
        await _availabilityDispatcher.DispatchConfigChangedAsync(business.Id, workingHours);
        return Ok(BusinessMapper.ToPublicDto(business));
    }

    [HttpPatch("live")]
    public async Task<ActionResult<BusinessPublicDto>> UpdateLiveStatus(
        [FromBody] UpdateBusinessLiveStatusRequest request)
    {
        var business = await FindOwnedBusinessAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        await _db.Businesses.UpdateOneAsync(
            b => b.Id == business.Id,
            Builders<Business>.Update.Set(b => b.IsLive, request.IsLive));

        business.IsLive = request.IsLive;
        await _availabilityDispatcher.DispatchStatusChangedAsync(business.Id, request.IsLive);
        return Ok(BusinessMapper.ToPublicDto(business));
    }

    [HttpPost("services")]
    public async Task<ActionResult<BusinessPublicDto>> AddService([FromBody] UpsertServiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Service name is required." });
        }

        var business = await FindOwnedBusinessAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        var services = BusinessMapper.NormalizeServices(business.Services);
        services.Add(new BusinessService
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = request.Name.Trim(),
            DurationMinutes = request.DurationMinutes,
            Price = request.Price
        });

        await _db.Businesses.UpdateOneAsync(
            b => b.Id == business.Id,
            Builders<Business>.Update.Set(b => b.Services, services));

        business.Services = services;
        return Ok(BusinessMapper.ToPublicDto(business));
    }

    [HttpPut("services/{serviceId}")]
    public async Task<ActionResult<BusinessPublicDto>> UpdateService(
        string serviceId,
        [FromBody] UpsertServiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Service name is required." });
        }

        var business = await FindOwnedBusinessAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        var services = BusinessMapper.NormalizeServices(business.Services);
        var index = services.FindIndex(s => s.Id == serviceId);
        if (index < 0)
        {
            return NotFound(new { message = "Service not found." });
        }

        services[index] = new BusinessService
        {
            Id = serviceId,
            Name = request.Name.Trim(),
            DurationMinutes = request.DurationMinutes,
            Price = request.Price
        };

        await _db.Businesses.UpdateOneAsync(
            b => b.Id == business.Id,
            Builders<Business>.Update.Set(b => b.Services, services));

        business.Services = services;
        return Ok(BusinessMapper.ToPublicDto(business));
    }

    [HttpDelete("services/{serviceId}")]
    public async Task<ActionResult<BusinessPublicDto>> DeleteService(string serviceId)
    {
        var business = await FindOwnedBusinessAsync();
        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        var services = BusinessMapper.NormalizeServices(business.Services);
        if (services.Count <= 1)
        {
            return BadRequest(new { message = "At least one service is required." });
        }

        var removed = services.RemoveAll(s => s.Id == serviceId);
        if (removed == 0)
        {
            return NotFound(new { message = "Service not found." });
        }

        await _db.Businesses.UpdateOneAsync(
            b => b.Id == business.Id,
            Builders<Business>.Update.Set(b => b.Services, services));

        business.Services = services;
        return Ok(BusinessMapper.ToPublicDto(business));
    }

    private async Task<Business?> FindOwnedBusinessAsync()
    {
        var businessId = _currentUser.BusinessId;
        if (string.IsNullOrEmpty(businessId))
        {
            return null;
        }

        return await _db.Businesses.Find(b => b.Id == businessId).FirstOrDefaultAsync();
    }
}
