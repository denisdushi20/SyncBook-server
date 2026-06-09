namespace SyncBook.Server.Models.Dtos;

public class BillingPlanDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Tagline { get; set; } = string.Empty;

    public int PriceCents { get; set; }

    public string Currency { get; set; } = "usd";

    public bool Featured { get; set; }

    public List<string> Features { get; set; } = [];
}

public class CreateCheckoutSessionRequest
{
    public string PlanId { get; set; } = string.Empty;
}

public class CreateCheckoutSessionResponse
{
    public string SessionId { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}

public class ConfirmCheckoutRequest
{
    public string SessionId { get; set; } = string.Empty;
}

public class SubscriptionStatusResponse
{
    public bool Active { get; set; }

    public string? PlanId { get; set; }

    public string? Status { get; set; }

    public DateTime? PaidAt { get; set; }
}

public class SubscriptionOverviewResponse
{
    public bool Active { get; set; }

    public string? Status { get; set; }

    public string? PlanId { get; set; }

    public string? PlanName { get; set; }

    public int? PriceCents { get; set; }

    public string Currency { get; set; } = "usd";

    public string Interval { get; set; } = "month";

    public bool AutoRenewal { get; set; }

    public DateTime? CurrentPeriodEnd { get; set; }

    public DateTime? PaidAt { get; set; }

    public bool IsLegacy { get; set; }
}

public class BillingInvoiceDto
{
    public string Id { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public int AmountCents { get; set; }

    public string Currency { get; set; } = "usd";

    public string Status { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? ReceiptUrl { get; set; }
}

public class PortalSessionResponse
{
    public string Url { get; set; } = string.Empty;
}
