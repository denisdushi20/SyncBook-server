using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SyncBook.Server.Data;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;

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

        return Ok(businesses.Select(MapToPublicDto).ToList());
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

        return Ok(MapToPublicDto(business));
    }

    private static BusinessPublicDto MapToPublicDto(Business business) => new()
    {
        Id = business.Id,
        Name = business.Name,
        Email = business.Email,
        Phone = business.Phone,
        Description = business.Description,
        Category = business.Category,
        Image = business.Image,
        Services = business.Services ?? [],
        WorkingHours = business.WorkingHours ?? []
    };
}
