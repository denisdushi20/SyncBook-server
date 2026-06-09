using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Stripe;
using Stripe.Checkout;
using SyncBook.Server.Data;
using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;
using SyncBook.Server.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SyncBook.Server.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly CurrentUserService _currentUser;

    public BillingController(
        MongoDbContext db,
        IConfiguration configuration,
        CurrentUserService currentUser)
    {
        _db = db;
        _configuration = configuration;
        _currentUser = currentUser;
    }

    [HttpGet("plans")]
    [AllowAnonymous]
    public ActionResult<IReadOnlyList<BillingPlanDto>> GetPlans()
    {
        return Ok(BillingPlanCatalog.Plans);
    }

    [Authorize]
    [HttpPost("create-checkout-session")]
    public async Task<ActionResult<CreateCheckoutSessionResponse>> CreateCheckoutSession(
        [FromBody] CreateCheckoutSessionRequest request)
    {
        if (string.IsNullOrEmpty(_currentUser.UserId))
        {
            return Unauthorized();
        }

        var plan = BillingPlanCatalog.Find(request.PlanId);
        if (plan is null)
        {
            return BadRequest(new { message = "Invalid plan selected." });
        }

        var user = await _db.Users.Find(u => u.Id == _currentUser.UserId).FirstOrDefaultAsync();
        if (user is null || user.Role != UserRole.BusinessOwner)
        {
            return BadRequest(new { message = "Only business owners can subscribe." });
        }

        var business = await _db.Businesses.Find(b => b.OwnerId == user.Id).FirstOrDefaultAsync();
        if (business is null)
        {
            return BadRequest(new { message = "Complete business onboarding before choosing a plan." });
        }

        if (SubscriptionHelper.IsSubscriptionActive(business) && business.SubscriptionStatus == "active")
        {
            return Conflict(new { message = "Your workspace already has an active subscription." });
        }

        var frontendUrl = _configuration["Stripe:FrontendUrl"] ?? "http://localhost:4200";

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            CustomerEmail = user.Email,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = plan.Currency,
                        UnitAmount = plan.PriceCents,
                        Recurring = new SessionLineItemPriceDataRecurringOptions
                        {
                            Interval = "month"
                        },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"SyncBook {plan.Name}"
                        }
                    },
                    Quantity = 1
                }
            ],
            SuccessUrl = $"{frontendUrl.TrimEnd('/')}/payment/complete?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{frontendUrl.TrimEnd('/')}/choose-plan",
            Metadata = new Dictionary<string, string>
            {
                ["businessId"] = business.Id,
                ["userId"] = user.Id,
                ["planId"] = plan.Id
            }
        };

        if (!string.IsNullOrEmpty(business.StripeCustomerId))
        {
            options.Customer = business.StripeCustomerId;
            options.CustomerEmail = null;
        }

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        return Ok(new CreateCheckoutSessionResponse
        {
            SessionId = session.Id,
            Url = session.Url ?? string.Empty
        });
    }

    [Authorize]
    [HttpPost("confirm-checkout")]
    public async Task<ActionResult<AuthResponse>> ConfirmCheckout([FromBody] ConfirmCheckoutRequest request)
    {
        if (string.IsNullOrEmpty(_currentUser.UserId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            return BadRequest(new { message = "Session id is required." });
        }

        var sessionService = new SessionService();
        var session = await sessionService.GetAsync(request.SessionId);

        if (!session.Metadata.TryGetValue("userId", out var sessionUserId)
            || sessionUserId != _currentUser.UserId)
        {
            return Forbid();
        }

        if (!session.Metadata.TryGetValue("businessId", out var businessId))
        {
            return BadRequest(new { message = "Invalid checkout session." });
        }

        var business = await _db.Businesses.Find(b => b.Id == businessId).FirstOrDefaultAsync();
        if (business is null || business.OwnerId != _currentUser.UserId)
        {
            return Forbid();
        }

        if (session.Status != "complete")
        {
            return BadRequest(new { message = "Checkout session is not complete." });
        }

        session.Metadata.TryGetValue("planId", out var planId);

        await ActivateSubscriptionAsync(business, session.CustomerId, session.SubscriptionId, planId);

        var user = await _db.Users.Find(u => u.Id == _currentUser.UserId).FirstOrDefaultAsync();
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var refreshedBusiness = await _db.Businesses.Find(b => b.Id == businessId).FirstOrDefaultAsync();
        var token = GenerateJwtToken(user, businessId, SubscriptionHelper.IsSubscriptionActive(refreshedBusiness));

        return Ok(new AuthResponse
        {
            Token = token,
            User = new UserSnapshot
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                BusinessId = businessId,
                SubscriptionActive = SubscriptionHelper.IsSubscriptionActive(refreshedBusiness)
            }
        });
    }

    [Authorize]
    [HttpGet("overview")]
    public async Task<ActionResult<SubscriptionOverviewResponse>> GetSubscriptionOverview()
    {
        var business = await GetOwnerBusinessAsync();
        if (business is null)
        {
            return Ok(new SubscriptionOverviewResponse { Active = true, IsLegacy = true });
        }

        var isLegacy = string.IsNullOrEmpty(business.SubscriptionStatus)
            && string.IsNullOrEmpty(business.StripeSubscriptionId);

        if (isLegacy)
        {
            return Ok(new SubscriptionOverviewResponse
            {
                Active = true,
                IsLegacy = true,
                Status = "legacy"
            });
        }

        var plan = BillingPlanCatalog.Find(business.SelectedPlan ?? string.Empty);
        var overview = new SubscriptionOverviewResponse
        {
            Active = SubscriptionHelper.IsSubscriptionActive(business),
            Status = business.SubscriptionStatus,
            PlanId = business.SelectedPlan,
            PlanName = plan?.Name,
            PriceCents = plan?.PriceCents,
            Currency = plan?.Currency ?? "usd",
            Interval = "month",
            PaidAt = business.SubscriptionPaidAt,
            IsLegacy = false,
            AutoRenewal = SubscriptionHelper.IsSubscriptionActive(business)
        };

        if (!string.IsNullOrEmpty(business.StripeSubscriptionId))
        {
            try
            {
                var subscriptionService = new SubscriptionService();
                var subscription = await subscriptionService.GetAsync(business.StripeSubscriptionId);
                overview.AutoRenewal = !subscription.CancelAtPeriodEnd;
                overview.CurrentPeriodEnd = subscription.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;
                overview.Active = subscription.Status is "active" or "trialing";
                overview.Status = subscription.Status;
            }
            catch (StripeException)
            {
                // Fall back to MongoDB fields already set on overview.
            }
        }

        return Ok(overview);
    }

    [Authorize]
    [HttpGet("invoices")]
    public async Task<ActionResult<IReadOnlyList<BillingInvoiceDto>>> GetInvoices()
    {
        var business = await GetOwnerBusinessAsync();
        if (business is null || string.IsNullOrEmpty(business.StripeCustomerId))
        {
            return Ok(Array.Empty<BillingInvoiceDto>());
        }

        try
        {
            var invoiceService = new InvoiceService();
            var invoices = await invoiceService.ListAsync(new InvoiceListOptions
            {
                Customer = business.StripeCustomerId,
                Limit = 12
            });

            var result = invoices.Data
                .Select(MapInvoice)
                .OrderByDescending(i => i.Date)
                .ToList();

            return Ok(result);
        }
        catch (StripeException)
        {
            return Ok(Array.Empty<BillingInvoiceDto>());
        }
    }

    [Authorize]
    [HttpPost("portal-session")]
    public async Task<ActionResult<PortalSessionResponse>> CreatePortalSession()
    {
        if (string.IsNullOrEmpty(_currentUser.UserId))
        {
            return Unauthorized();
        }

        var business = await GetOwnerBusinessAsync();
        if (business is null || string.IsNullOrEmpty(business.StripeCustomerId))
        {
            return BadRequest(new { message = "No billing account found for this workspace." });
        }

        var frontendUrl = _configuration["Stripe:FrontendUrl"] ?? "http://localhost:4200";

        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = business.StripeCustomerId,
            ReturnUrl = $"{frontendUrl.TrimEnd('/')}/dashboard/subscription"
        };

        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(options);

        return Ok(new PortalSessionResponse
        {
            Url = session.Url ?? string.Empty
        });
    }

    [Authorize]
    [HttpGet("subscription")]
    public async Task<ActionResult<SubscriptionStatusResponse>> GetSubscriptionStatus()
    {
        if (string.IsNullOrEmpty(_currentUser.UserId))
        {
            return Unauthorized();
        }

        var business = await _db.Businesses.Find(b => b.OwnerId == _currentUser.UserId).FirstOrDefaultAsync();
        if (business is null)
        {
            return Ok(new SubscriptionStatusResponse { Active = true });
        }

        return Ok(new SubscriptionStatusResponse
        {
            Active = SubscriptionHelper.IsSubscriptionActive(business),
            PlanId = business.SelectedPlan,
            Status = business.SubscriptionStatus,
            PaidAt = business.SubscriptionPaidAt
        });
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var webhookSecret = _configuration["Stripe:WebhookSecret"];

        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return BadRequest(new { message = "Webhook secret is not configured." });
        }

        Event stripeEvent;
        try
        {
            var signature = Request.Headers["Stripe-Signature"];
            stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
        }
        catch (Exception)
        {
            return BadRequest(new { message = "Invalid webhook signature." });
        }

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                if (stripeEvent.Data.Object is Stripe.Checkout.Session session)
                {
                    await HandleCheckoutCompletedAsync(session);
                }
                break;

            case EventTypes.CustomerSubscriptionDeleted:
                if (stripeEvent.Data.Object is Subscription subscription)
                {
                    await HandleSubscriptionDeletedAsync(subscription);
                }
                break;
        }

        return Ok();
    }

    private async Task HandleCheckoutCompletedAsync(Stripe.Checkout.Session session)
    {
        if (!session.Metadata.TryGetValue("businessId", out var businessId))
        {
            return;
        }

        var business = await _db.Businesses.Find(b => b.Id == businessId).FirstOrDefaultAsync();
        if (business is null)
        {
            return;
        }

        session.Metadata.TryGetValue("planId", out var planId);
        await ActivateSubscriptionAsync(business, session.CustomerId, session.SubscriptionId, planId);
    }

    private async Task HandleSubscriptionDeletedAsync(Subscription subscription)
    {
        var business = await _db.Businesses
            .Find(b => b.StripeSubscriptionId == subscription.Id)
            .FirstOrDefaultAsync();

        if (business is null)
        {
            return;
        }

        var update = Builders<Business>.Update.Set(b => b.SubscriptionStatus, "canceled");
        await _db.Businesses.UpdateOneAsync(b => b.Id == business.Id, update);
    }

    private async Task<Business?> GetOwnerBusinessAsync()
    {
        if (string.IsNullOrEmpty(_currentUser.UserId))
        {
            return null;
        }

        return await _db.Businesses
            .Find(b => b.OwnerId == _currentUser.UserId)
            .FirstOrDefaultAsync();
    }

    private static BillingInvoiceDto MapInvoice(Invoice invoice)
    {
        var description = invoice.Lines?.Data?.FirstOrDefault()?.Description
            ?? "SyncBook subscription";

        return new BillingInvoiceDto
        {
            Id = invoice.Id,
            Date = invoice.Created,
            AmountCents = (int)(invoice.AmountPaid > 0 ? invoice.AmountPaid : invoice.AmountDue),
            Currency = invoice.Currency,
            Status = invoice.Status ?? "unknown",
            Description = description,
            ReceiptUrl = invoice.HostedInvoiceUrl ?? invoice.InvoicePdf
        };
    }

    private async Task ActivateSubscriptionAsync(
        Business business,
        string? customerId,
        string? subscriptionId,
        string? planId)
    {
        var update = Builders<Business>.Update
            .Set(b => b.SubscriptionStatus, "active")
            .Set(b => b.SubscriptionPaidAt, DateTime.UtcNow);

        if (!string.IsNullOrEmpty(customerId))
        {
            update = update.Set(b => b.StripeCustomerId, customerId);
        }

        if (!string.IsNullOrEmpty(subscriptionId))
        {
            update = update.Set(b => b.StripeSubscriptionId, subscriptionId);
        }

        if (!string.IsNullOrEmpty(planId))
        {
            update = update.Set(b => b.SelectedPlan, planId);
        }

        await _db.Businesses.UpdateOneAsync(b => b.Id == business.Id, update);
    }

    private string GenerateJwtToken(User user, string? businessId, bool subscriptionActive)
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
            claims.Add(new Claim("subscriptionActive", subscriptionActive ? "true" : "false"));
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
