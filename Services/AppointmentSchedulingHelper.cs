using SyncBook.Server.Models;
using SyncBook.Server.Models.Dtos;

namespace SyncBook.Server.Services;

public static class AppointmentSchedulingHelper
{
    public const int DefaultBufferMinutes = 15;
    public const int DefaultServiceDurationMinutes = 30;
    public const int SlotStepMinutes = 15;

    public static int ResolveBufferMinutes(int? customBufferMinutes) =>
        customBufferMinutes ?? DefaultBufferMinutes;

    public static BusinessService? FindService(Business business, string serviceId) =>
        business.Services.FirstOrDefault(s => s.Id == serviceId);

    public static int ResolveDurationMinutes(BusinessService service) =>
        service.DurationMinutes ?? DefaultServiceDurationMinutes;

    public static int GetEffectiveBuffer(Appointment appointment) =>
        appointment.BufferMinutes;

    public static bool Overlaps(
        Appointment existing,
        DateTime candidateStartUtc,
        DateTime candidateEndUtc)
    {
        var existingBuffer = GetEffectiveBuffer(existing);
        return existing.StartUtc < candidateEndUtc
            && existing.EndUtc.AddMinutes(existingBuffer) > candidateStartUtc;
    }

    public static bool HasConflict(
        IEnumerable<Appointment> existingAppointments,
        DateTime candidateStartUtc,
        DateTime candidateEndUtc)
    {
        return existingAppointments.Any(a =>
            Overlaps(a, candidateStartUtc, candidateEndUtc));
    }

    public static List<InternalAppointmentSlotDto> GenerateSlots(
        Business business,
        DateTime date,
        string serviceId,
        IReadOnlyList<Appointment> dayAppointments)
    {
        return GenerateStaffAwareSlots(
            business,
            date,
            serviceId,
            dayAppointments,
            [],
            null);
    }

    public static List<InternalAppointmentSlotDto> GenerateStaffAwareSlots(
        Business business,
        DateTime date,
        string serviceId,
        IReadOnlyList<Appointment> dayAppointments,
        IReadOnlyList<StaffMember> bookableStaff,
        string? staffId)
    {
        var service = FindService(business, serviceId);
        if (service is null)
        {
            return [];
        }

        var durationMinutes = ResolveDurationMinutes(service);
        var daySchedule = ResolveDaySchedule(business, date);
        if (daySchedule is null || !daySchedule.IsOpen
            || string.IsNullOrWhiteSpace(daySchedule.OpenTime)
            || string.IsNullOrWhiteSpace(daySchedule.CloseTime))
        {
            return [];
        }

        if (!TimeOnly.TryParse(daySchedule.OpenTime, out var openTime)
            || !TimeOnly.TryParse(daySchedule.CloseTime, out var closeTime))
        {
            return [];
        }

        var dayStart = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var activeAppointments = dayAppointments
            .Where(a => a.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed)
            .ToList();

        StaffMember? targetStaff = null;
        if (!string.IsNullOrWhiteSpace(staffId))
        {
            targetStaff = bookableStaff.FirstOrDefault(s => s.Id == staffId);
            if (targetStaff is null)
            {
                return [];
            }
        }

        var staffPool = bookableStaff.Where(s => s.IsBookable).ToList();
        var slots = new List<InternalAppointmentSlotDto>();
        var cursor = openTime;

        while (cursor.AddMinutes(durationMinutes) <= closeTime)
        {
            var startUtc = dayStart.Add(cursor.ToTimeSpan());
            var endUtc = startUtc.AddMinutes(durationMinutes);

            if (StaffPoolCapacityCalculator.HasLegacyBusinessConflict(activeAppointments, startUtc, endUtc))
            {
                cursor = cursor.AddMinutes(SlotStepMinutes);
                continue;
            }

            var isAvailable = targetStaff is not null
                ? StaffPoolCapacityCalculator.IsStaffAvailable(targetStaff, startUtc, endUtc, activeAppointments)
                : StaffPoolCapacityCalculator.HasPoolCapacity(staffPool, startUtc, endUtc, activeAppointments);

            if (isAvailable)
            {
                slots.Add(new InternalAppointmentSlotDto
                {
                    StartUtc = startUtc,
                    EndUtc = endUtc,
                    Label = cursor.ToString("HH:mm")
                });
            }

            cursor = cursor.AddMinutes(SlotStepMinutes);
        }

        return slots;
    }

    private static DaySchedule? ResolveDaySchedule(Business business, DateTime date)
    {
        var dayName = date.DayOfWeek.ToString();
        return business.WorkingHours?.FirstOrDefault(d =>
            string.Equals(d.Day, dayName, StringComparison.OrdinalIgnoreCase));
    }
}
