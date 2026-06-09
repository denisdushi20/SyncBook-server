using SyncBook.Server.Models.Dtos;

namespace SyncBook.Server.Services;

public static class BillingPlanCatalog
{
    public static IReadOnlyList<BillingPlanDto> Plans { get; } =
    [
        new BillingPlanDto
        {
            Id = "starter",
            Name = "Starter",
            Tagline = "For solo practitioners",
            PriceCents = 1900,
            Featured = false,
            Features =
            [
                "Up to 50 bookings / month",
                "1 business profile",
                "Real-time slot sync",
                "Email notifications"
            ]
        },
        new BillingPlanDto
        {
            Id = "professional",
            Name = "Professional",
            Tagline = "For growing teams",
            PriceCents = 4900,
            Featured = true,
            Features =
            [
                "Unlimited bookings",
                "Up to 5 staff members",
                "Live owner dashboard",
                "Priority SignalR channels",
                "Custom business hours"
            ]
        }
    ];

    public static BillingPlanDto? Find(string planId) =>
        Plans.FirstOrDefault(p => p.Id.Equals(planId, StringComparison.OrdinalIgnoreCase));
}
