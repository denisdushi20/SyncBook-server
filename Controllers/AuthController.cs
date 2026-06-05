using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using SyncBook.Server.Data;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;

namespace SyncBook.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly IConfiguration _configuration;

    public AuthController(MongoDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet("check-email")]
    public async Task<ActionResult<EmailAvailabilityResponse>> CheckEmail([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return BadRequest(new { message = "A valid email address is required." });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existingUser = await _db.Users
            .Find(u => u.Email == normalizedEmail)
            .FirstOrDefaultAsync();

        return Ok(new EmailAvailabilityResponse
        {
            Available = existingUser is null
        });
    }

    [HttpGet("check-business-name")]
    public async Task<ActionResult<EmailAvailabilityResponse>> CheckBusinessName([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "A business name is required." });
        }

        var taken = await IsBusinessNameTakenAsync(name);
        return Ok(new EmailAvailabilityResponse
        {
            Available = !taken
        });
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
    {
        if (request.Role == UserRole.BusinessOwner)
        {
            var businessError = ValidateBusinessOnboarding(request.Business);
            if (businessError is not null)
            {
                return BadRequest(new { message = businessError });
            }
        }
        else if (request.Business is not null)
        {
            return BadRequest(new { message = "Business onboarding is only allowed for BusinessOwner registration." });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _db.Users
            .Find(u => u.Email == normalizedEmail)
            .FirstOrDefaultAsync();

        if (existingUser is not null)
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        if (request.Role == UserRole.BusinessOwner && request.Business is not null
            && await IsBusinessNameTakenAsync(request.Business.Name))
        {
            return Conflict(new { message = "A business with this name already exists." });
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role
        };

        await _db.Users.InsertOneAsync(user);

        string? businessId = null;

        if (request.Role == UserRole.BusinessOwner && request.Business is not null)
        {
            var business = new Business
            {
                OwnerId = user.Id,
                Name = request.Business.Name.Trim(),
                Email = request.Business.Email.Trim().ToLowerInvariant(),
                Phone = string.IsNullOrWhiteSpace(request.Business.Phone) ? null : request.Business.Phone.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Business.Description) ? null : request.Business.Description.Trim(),
                Category = string.IsNullOrWhiteSpace(request.Business.Category) ? null : request.Business.Category.Trim(),
                Image = string.IsNullOrWhiteSpace(request.Business.Image) ? null : request.Business.Image,
                Services = request.Business.Services
                    .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                    .Select(s => new BusinessService
                    {
                        Name = s.Name.Trim(),
                        DurationMinutes = s.DurationMinutes
                    })
                    .ToList(),
                WorkingHours = request.Business.WorkingHours
                    .Select(d => new DaySchedule
                    {
                        Day = d.Day,
                        IsOpen = d.IsOpen,
                        OpenTime = d.IsOpen ? d.OpenTime : null,
                        CloseTime = d.IsOpen ? d.CloseTime : null
                    })
                    .ToList()
            };

            await _db.Businesses.InsertOneAsync(business);
            businessId = business.Id;
        }

        return CreatedAtAction(nameof(Register), new RegisterResponse
        {
            User = new UserSnapshot
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                BusinessId = businessId
            }
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Find(u => u.Email == normalizedEmail)
            .FirstOrDefaultAsync();

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        string? businessId = null;

        if (user.Role == UserRole.BusinessOwner)
        {
            var business = await _db.Businesses
                .Find(b => b.OwnerId == user.Id)
                .FirstOrDefaultAsync();

            businessId = business?.Id;
        }

        var token = GenerateJwtToken(user, businessId);

        return Ok(new AuthResponse
        {
            Token = token,
            User = new UserSnapshot
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                BusinessId = businessId
            }
        });
    }

    private async Task<bool> IsBusinessNameTakenAsync(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        var escaped = Regex.Escape(trimmed);
        var filter = Builders<Business>.Filter.Regex(
            b => b.Name,
            new BsonRegularExpression($"^{escaped}$", "i"));

        var existing = await _db.Businesses.Find(filter).FirstOrDefaultAsync();
        return existing is not null;
    }

    private static string? ValidateBusinessOnboarding(BusinessOnboardingRequest? business)
    {
        if (business is null
            || string.IsNullOrWhiteSpace(business.Name)
            || string.IsNullOrWhiteSpace(business.Email))
        {
            return "Business onboarding is required for BusinessOwner registration.";
        }

        var validServices = business.Services?.Where(s => !string.IsNullOrWhiteSpace(s.Name)).ToList() ?? [];
        if (validServices.Count == 0)
        {
            return "At least one service is required.";
        }

        if (business.WorkingHours is null || business.WorkingHours.Count < 7)
        {
            return "Working hours for all 7 days are required.";
        }

        foreach (var day in business.WorkingHours)
        {
            if (day.IsOpen && (string.IsNullOrWhiteSpace(day.OpenTime) || string.IsNullOrWhiteSpace(day.CloseTime)))
            {
                return $"Open and close times are required for {day.Day} when the business is open.";
            }
        }

        return null;
    }

    private string GenerateJwtToken(User user, string? businessId)
    {
        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        var issuer = _configuration["Jwt:Issuer"] ?? "SyncBook";
        var audience = _configuration["Jwt:Audience"] ?? "SyncBook";
        var expirationHours = int.TryParse(_configuration["Jwt:ExpirationHours"], out var hours) ? hours : 24;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("role", user.Role.ToString())
        };

        if (!string.IsNullOrEmpty(businessId))
        {
            claims.Add(new Claim("businessId", businessId));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expirationHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
