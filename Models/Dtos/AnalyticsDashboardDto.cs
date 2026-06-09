namespace SyncBook.Server.Models.Dtos;

public class AnalyticsDashboardDto
{
    public string Range { get; set; } = string.Empty;

    public DateTime FromUtc { get; set; }

    public DateTime ToUtc { get; set; }

    public decimal TotalRevenue { get; set; }

    public decimal PotentialRevenue { get; set; }

    public int TotalBookings { get; set; }

    public decimal AverageOrderValue { get; set; }

    public decimal BookingFulfillmentRatePercent { get; set; }

    public BookingStatusBreakdownDto BookingStats { get; set; } = new();

    public List<TopServiceDto> TopPerformingServices { get; set; } = [];

    public List<DailyBookingTrendDto> BookingTrends { get; set; } = [];
}

public class BookingStatusBreakdownDto
{
    public int PendingCount { get; set; }

    public int ConfirmedCount { get; set; }

    public int CompletedCount { get; set; }

    public int CancelledCount { get; set; }

    public decimal PendingPercent { get; set; }

    public decimal ConfirmedPercent { get; set; }

    public decimal CompletedPercent { get; set; }

    public decimal CancelledPercent { get; set; }
}

public class TopServiceDto
{
    public string ServiceId { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public int BookingCount { get; set; }

    public decimal Revenue { get; set; }
}

public class DailyBookingTrendDto
{
    public string Date { get; set; } = string.Empty;

    public int BookingCount { get; set; }
}
