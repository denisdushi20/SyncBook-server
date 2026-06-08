using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using SyncBook.Server.Data;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;
using SyncBook.Server.Services;
using SyncBook.Server.Validation;

namespace SyncBook.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly CurrentUserService _currentUser;
    private readonly IEmailService _emailService;

    public AuthController(
        MongoDbContext db,
        IConfiguration configuration,
        CurrentUserService currentUser,
        IEmailService emailService)
    {
        _db = db;
        _configuration = configuration;
        _currentUser = currentUser;
        _emailService = emailService;
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

    [HttpPost("send-registration-code")]
    public async Task<IActionResult> SendRegistrationCode([FromBody] SendRegistrationCodeRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (!normalizedEmail.Contains('@'))
        {
            return BadRequest(new { message = "A valid email address is required." });
        }

        var existingUser = await _db.Users
            .Find(u => u.Email == normalizedEmail)
            .FirstOrDefaultAsync();

        if (existingUser is not null)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        await InvalidateActiveCodesAsync(normalizedEmail, VerificationCodePurpose.Registration);
        var code = VerificationCodeGenerator.GenerateSixDigitCode();
        await _db.VerificationCodes.InsertOneAsync(new VerificationCode
        {
            Email = normalizedEmail,
            Purpose = VerificationCodePurpose.Registration,
            CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        });

        await _emailService.SendVerificationCodeEmailAsync(
            normalizedEmail,
            code,
            VerificationEmailPurpose.Registration);

        return Ok(new { message = "Verification code sent. Check your email." });
    }

    [HttpPost("verify-registration-code")]
    public async Task<IActionResult> VerifyRegistrationCode([FromBody] VerifyCodeRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var matched = await FindValidCodeAsync(normalizedEmail, VerificationCodePurpose.Registration, request.Code);
        if (matched is null)
        {
            return BadRequest(new { message = "Invalid or expired verification code." });
        }

        await MarkCodeUsedAsync(matched.Id);
        return Ok(new { message = "Email verified successfully.", email = normalizedEmail });
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
            Role = request.Role,
            AuthProvider = AuthProvider.Local
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
                        Id = ObjectId.GenerateNewId().ToString(),
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
            await ProvisionOwnerStaffAsync(user.Id, business.Id, business.WorkingHours);
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

        if (user is null || string.IsNullOrEmpty(user.Password)
            || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
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

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>> GetCurrentUser()
    {
        if (string.IsNullOrEmpty(_currentUser.UserId))
        {
            return Unauthorized();
        }

        var user = await _db.Users
            .Find(u => u.Id == _currentUser.UserId)
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var businessId = await ResolveBusinessIdAsync(user);

        return Ok(new UserProfileResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            AuthProvider = user.AuthProvider,
            BusinessId = businessId
        });
    }

    [Authorize]
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrEmpty(_currentUser.UserId))
        {
            return Unauthorized();
        }

        var user = await _db.Users
            .Find(u => u.Id == _currentUser.UserId)
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (user.AuthProvider == AuthProvider.Local)
        {
            if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(user.Password)
                || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.Password))
            {
                return BadRequest(new { message = "Current password is incorrect." });
            }
        }
        else if (user.AuthProvider != AuthProvider.Google)
        {
            return BadRequest(new { message = "Password change is not supported for this account." });
        }

        var update = Builders<User>.Update.Set(u => u.Password, BCrypt.Net.BCrypt.HashPassword(request.NewPassword));
        await _db.Users.UpdateOneAsync(u => u.Id == user.Id, update);

        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Find(u => u.Email == normalizedEmail)
            .FirstOrDefaultAsync();

        if (user is not null && user.AuthProvider == AuthProvider.Local)
        {
            await InvalidateActiveCodesAsync(normalizedEmail, VerificationCodePurpose.PasswordReset);
            var code = VerificationCodeGenerator.GenerateSixDigitCode();
            await _db.VerificationCodes.InsertOneAsync(new VerificationCode
            {
                Email = normalizedEmail,
                Purpose = VerificationCodePurpose.PasswordReset,
                UserId = user.Id,
                CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            });

            await _emailService.SendVerificationCodeEmailAsync(
                normalizedEmail,
                code,
                VerificationEmailPurpose.PasswordReset);
        }

        return Ok(new { message = "If an account exists for that email, a verification code has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var matched = await FindValidCodeAsync(normalizedEmail, VerificationCodePurpose.PasswordReset, request.Code);
        if (matched is null)
        {
            return BadRequest(new { message = "Invalid or expired verification code." });
        }

        var user = await _db.Users
            .Find(u => u.Id == matched.UserId)
            .FirstOrDefaultAsync();

        if (user is null || user.AuthProvider != AuthProvider.Local)
        {
            return BadRequest(new { message = "Unable to reset password for this account." });
        }

        var passwordUpdate = Builders<User>.Update.Set(u => u.Password, BCrypt.Net.BCrypt.HashPassword(request.NewPassword));
        await _db.Users.UpdateOneAsync(u => u.Id == user.Id, passwordUpdate);
        await MarkCodeUsedAsync(matched.Id);

        return Ok(new { message = "Password has been reset. You can sign in now." });
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        var clientId = _configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId) || clientId == "YOUR_GOOGLE_CLIENT_ID")
        {
            return BadRequest(new { message = "Google Sign-In is not configured." });
        }

        var isBusinessIntent = string.Equals(request.Intent, "business", StringComparison.OrdinalIgnoreCase);

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
        }
        catch
        {
            return Unauthorized(new { message = "Invalid Google token." });
        }

        if (string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Subject))
        {
            return BadRequest(new { message = "Google account did not provide required profile information." });
        }

        var normalizedEmail = payload.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Find(u => u.GoogleId == payload.Subject || u.Email == normalizedEmail)
            .FirstOrDefaultAsync();

        if (user is null)
        {
            user = new User
            {
                FullName = string.IsNullOrWhiteSpace(payload.Name) ? normalizedEmail : payload.Name.Trim(),
                Email = normalizedEmail,
                Password = null,
                Role = isBusinessIntent ? UserRole.BusinessOwner : UserRole.Customer,
                AuthProvider = AuthProvider.Google,
                GoogleId = payload.Subject
            };

            await _db.Users.InsertOneAsync(user);
        }
        else if (user.AuthProvider == AuthProvider.Local)
        {
            if (user.Role == UserRole.BusinessOwner)
            {
                var linkUpdate = Builders<User>.Update.Set(u => u.GoogleId, payload.Subject);
                await _db.Users.UpdateOneAsync(u => u.Id == user.Id, linkUpdate);
                user.GoogleId = payload.Subject;
            }
            else
            {
                return Conflict(new { message = "An account with this email already exists. Sign in with email and password." });
            }
        }
        else if (string.IsNullOrEmpty(user.GoogleId))
        {
            var linkUpdate = Builders<User>.Update.Set(u => u.GoogleId, payload.Subject);
            await _db.Users.UpdateOneAsync(u => u.Id == user.Id, linkUpdate);
            user.GoogleId = payload.Subject;
        }

        if (isBusinessIntent && user.Role == UserRole.Customer)
        {
            var roleUpdate = Builders<User>.Update.Set(u => u.Role, UserRole.BusinessOwner);
            await _db.Users.UpdateOneAsync(u => u.Id == user.Id, roleUpdate);
            user.Role = UserRole.BusinessOwner;
        }

        var businessId = await ResolveBusinessIdAsync(user);
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

    [Authorize]
    [HttpPost("complete-business-onboarding")]
    public async Task<ActionResult<AuthResponse>> CompleteBusinessOnboarding(
        [FromBody] CompleteBusinessOnboardingRequest request)
    {
        if (string.IsNullOrEmpty(_currentUser.UserId))
        {
            return Unauthorized();
        }

        var user = await _db.Users
            .Find(u => u.Id == _currentUser.UserId)
            .FirstOrDefaultAsync();

        if (user is null || user.Role != UserRole.BusinessOwner)
        {
            return BadRequest(new { message = "Only business owners can complete onboarding." });
        }

        var existingBusiness = await _db.Businesses
            .Find(b => b.OwnerId == user.Id)
            .FirstOrDefaultAsync();

        if (existingBusiness is not null)
        {
            return Conflict(new { message = "Business onboarding is already complete." });
        }

        var businessError = ValidateBusinessOnboarding(request.Business);
        if (businessError is not null)
        {
            return BadRequest(new { message = businessError });
        }

        if (await IsBusinessNameTakenAsync(request.Business.Name))
        {
            return Conflict(new { message = "A business with this name already exists." });
        }

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
                    Id = ObjectId.GenerateNewId().ToString(),
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
        await ProvisionOwnerStaffAsync(user.Id, business.Id, business.WorkingHours);

        var token = GenerateJwtToken(user, business.Id);

        return Ok(new AuthResponse
        {
            Token = token,
            User = new UserSnapshot
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                BusinessId = business.Id
            }
        });
    }

    private async Task ProvisionOwnerStaffAsync(
        string userId,
        string businessId,
        List<DaySchedule> workingHours)
    {
        var staffMember = StaffBootstrapHelper.CreateOwnerStaffMember(userId, businessId, workingHours);
        await _db.StaffMembers.InsertOneAsync(staffMember);
    }

    private async Task<string?> ResolveBusinessIdAsync(User user)
    {
        if (user.Role != UserRole.BusinessOwner)
        {
            return null;
        }

        var business = await _db.Businesses
            .Find(b => b.OwnerId == user.Id)
            .FirstOrDefaultAsync();

        return business?.Id;
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

        return WorkingHoursValidator.Validate(business.WorkingHours);
    }

    private async Task InvalidateActiveCodesAsync(string email, VerificationCodePurpose purpose)
    {
        var filter = Builders<VerificationCode>.Filter.And(
            Builders<VerificationCode>.Filter.Eq(c => c.Email, email),
            Builders<VerificationCode>.Filter.Eq(c => c.Purpose, purpose),
            Builders<VerificationCode>.Filter.Eq(c => c.UsedAt, null));

        var update = Builders<VerificationCode>.Update.Set(c => c.UsedAt, DateTime.UtcNow);
        await _db.VerificationCodes.UpdateManyAsync(filter, update);
    }

    private async Task<VerificationCode?> FindValidCodeAsync(
        string email,
        VerificationCodePurpose purpose,
        string code)
    {
        var codes = await _db.VerificationCodes
            .Find(c => c.Email == email
                && c.Purpose == purpose
                && c.UsedAt == null
                && c.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        return codes.FirstOrDefault(c => BCrypt.Net.BCrypt.Verify(code, c.CodeHash));
    }

    private async Task MarkCodeUsedAsync(string codeId)
    {
        var update = Builders<VerificationCode>.Update.Set(c => c.UsedAt, DateTime.UtcNow);
        await _db.VerificationCodes.UpdateOneAsync(c => c.Id == codeId, update);
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
