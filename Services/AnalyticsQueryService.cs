using MongoDB.Driver;

using SyncBook.Server.Data;

using SyncBook.Server.Models;

using SyncBook.Server.Models.Dtos;



namespace SyncBook.Server.Services;



public class AnalyticsQueryService

{

    private readonly MongoDbContext _db;



    public AnalyticsQueryService(MongoDbContext db)

    {

        _db = db;

    }



    public async Task<AnalyticsDashboardDto> GetDashboardAsync(

        string businessId,

        string range,

        int? year = null,

        int? month = null)

    {

        var (fromUtc, toUtc, normalizedRange, trendYear, trendMonth) = ResolveRange(range, year, month);

        var business = await _db.Businesses.Find(b => b.Id == businessId).FirstOrDefaultAsync()

            ?? throw new InvalidOperationException("Business not found.");



        var serviceLookup = business.Services.ToDictionary(

            s => s.Id,

            s => new ServiceLookup(s.Name, s.Price ?? 0));



        var appointments = await LoadAppointmentsInRangeAsync(businessId, fromUtc, toUtc, normalizedRange, trendYear, trendMonth);



        decimal ResolvePrice(Appointment appointment)

        {

            if (appointment.ServicePrice.HasValue)

            {

                return appointment.ServicePrice.Value;

            }



            return serviceLookup.TryGetValue(appointment.ServiceId, out var service)

                ? service.Price

                : 0;

        }



        var totalRevenue = appointments

            .Where(a => a.Status == AppointmentStatus.Completed)

            .Sum(ResolvePrice);



        var potentialRevenue = appointments

            .Where(a => a.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed)

            .Sum(ResolvePrice);



        var paidCount = appointments.Count(a => a.Status == AppointmentStatus.Completed);



        var pendingCount = appointments.Count(a => a.Status == AppointmentStatus.Pending);

        var confirmedCount = appointments.Count(a => a.Status == AppointmentStatus.Confirmed);

        var completedCount = appointments.Count(a => a.Status == AppointmentStatus.Completed);

        var cancelledCount = appointments.Count(a => a.Status == AppointmentStatus.Cancelled);

        var totalBookings = appointments.Count;



        var averageOrderValue = paidCount > 0

            ? totalRevenue / paidCount

            : 0;



        var resolvedCount = completedCount + cancelledCount;

        var bookingFulfillmentRatePercent = resolvedCount > 0

            ? Math.Round((decimal)completedCount / resolvedCount * 100, 1)

            : 0;



        var bookingStats = BuildStatusBreakdown(

            totalBookings,

            pendingCount,

            confirmedCount,

            completedCount,

            cancelledCount);



        var topPerformingServices = appointments

            .Where(a => a.Status == AppointmentStatus.Completed)

            .GroupBy(a => a.ServiceId)

            .Select(group =>

            {

                var first = group.First();

                var serviceName = serviceLookup.TryGetValue(group.Key, out var service)

                    ? service.Name

                    : (string.IsNullOrWhiteSpace(first.ServiceName) ? "Unknown" : first.ServiceName);



                return new TopServiceDto

                {

                    ServiceId = group.Key,

                    ServiceName = serviceName,

                    BookingCount = group.Count(),

                    Revenue = group.Sum(ResolvePrice)

                };

            })

            .OrderByDescending(s => s.Revenue)

            .ThenByDescending(s => s.BookingCount)

            .Take(5)

            .ToList();



        var bookingTrends = BuildBookingTrends(appointments, normalizedRange, fromUtc, toUtc, trendYear, trendMonth);



        return new AnalyticsDashboardDto

        {

            Range = normalizedRange,

            FromUtc = fromUtc,

            ToUtc = toUtc,

            TotalRevenue = totalRevenue,

            PotentialRevenue = potentialRevenue,

            TotalBookings = totalBookings,

            AverageOrderValue = Math.Round(averageOrderValue, 2),

            BookingFulfillmentRatePercent = bookingFulfillmentRatePercent,

            BookingStats = bookingStats,

            TopPerformingServices = topPerformingServices,

            BookingTrends = bookingTrends

        };

    }



    private async Task<List<Appointment>> LoadAppointmentsInRangeAsync(

        string businessId,

        DateTime fromUtc,

        DateTime toUtc,

        string normalizedRange,

        int trendYear,

        int trendMonth)

    {

        if (normalizedRange == "month")

        {

            return await _db.Appointments

                .Find(a => a.BusinessId == businessId

                    && a.StartUtc.Year == trendYear

                    && a.StartUtc.Month == trendMonth)

                .ToListAsync();

        }



        return await _db.Appointments

            .Find(a => a.BusinessId == businessId && a.StartUtc >= fromUtc && a.StartUtc < toUtc)

            .ToListAsync();

    }



    private static (DateTime FromUtc, DateTime ToUtc, string NormalizedRange, int TrendYear, int TrendMonth) ResolveRange(

        string? range,

        int? year,

        int? month)

    {

        var now = DateTime.UtcNow;

        var normalized = string.IsNullOrWhiteSpace(range) ? "month" : range.Trim().ToLowerInvariant();



        if (normalized is "30days")

        {

            normalized = "month";

        }



        DateTime fromUtc;

        DateTime toUtc;

        int trendYear;

        int trendMonth;



        switch (normalized)

        {

            case "month":

                trendYear = year ?? now.Year;

                trendMonth = month ?? now.Month;

                if (trendMonth is < 1 or > 12)

                {

                    trendYear = now.Year;

                    trendMonth = now.Month;

                }



                fromUtc = new DateTime(trendYear, trendMonth, 1, 0, 0, 0, DateTimeKind.Utc);

                toUtc = fromUtc.AddMonths(1);

                break;

            case "90days":

                trendYear = now.Year;

                trendMonth = now.Month;

                fromUtc = now.Date.AddDays(-90);

                toUtc = now.Date.AddDays(1);

                break;

            case "year":

                trendYear = year ?? now.Year;

                fromUtc = new DateTime(trendYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                toUtc = fromUtc.AddYears(1);

                trendMonth = now.Month;

                break;

            default:

                normalized = "month";

                trendYear = now.Year;

                trendMonth = now.Month;

                fromUtc = new DateTime(trendYear, trendMonth, 1, 0, 0, 0, DateTimeKind.Utc);

                toUtc = fromUtc.AddMonths(1);

                break;

        }



        return (fromUtc, toUtc, normalized, trendYear, trendMonth);

    }



    private static List<DailyBookingTrendDto> BuildBookingTrends(

        List<Appointment> appointments,

        string normalizedRange,

        DateTime fromUtc,

        DateTime toUtc,

        int year,

        int month)

    {

        var countsByDate = appointments

            .GroupBy(a => ToCalendarDateKey(a.StartUtc))

            .ToDictionary(g => g.Key, g => g.Count());



        DateTime trendStart;

        DateTime trendEndExclusive;

        if (normalizedRange == "month")

        {

            trendStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

            trendEndExclusive = trendStart.AddMonths(1);

        }

        else

        {

            trendStart = fromUtc.Date;

            trendEndExclusive = toUtc.Date;

        }



        var bookingTrends = new List<DailyBookingTrendDto>();

        for (var day = trendStart; day < trendEndExclusive; day = day.AddDays(1))

        {

            var dateKey = ToCalendarDateKey(day);

            bookingTrends.Add(new DailyBookingTrendDto

            {

                Date = dateKey,

                BookingCount = countsByDate.GetValueOrDefault(dateKey, 0)

            });

        }



        return bookingTrends;

    }



    /// <summary>

    /// Uses Y/M/D components of the stored timestamp as the business calendar day

    /// (timestamps are UTC-marked wall-clock values in this app).

    /// </summary>

    private static string ToCalendarDateKey(DateTime timestamp) =>

        $"{timestamp.Year:0000}-{timestamp.Month:00}-{timestamp.Day:00}";



    private static BookingStatusBreakdownDto BuildStatusBreakdown(

        int total,

        int pending,

        int confirmed,

        int completed,

        int cancelled)

    {

        decimal Percent(int count) => total > 0

            ? Math.Round((decimal)count / total * 100, 1)

            : 0;



        return new BookingStatusBreakdownDto

        {

            PendingCount = pending,

            ConfirmedCount = confirmed,

            CompletedCount = completed,

            CancelledCount = cancelled,

            PendingPercent = Percent(pending),

            ConfirmedPercent = Percent(confirmed),

            CompletedPercent = Percent(completed),

            CancelledPercent = Percent(cancelled)

        };

    }



    private sealed record ServiceLookup(string Name, decimal Price);

}


