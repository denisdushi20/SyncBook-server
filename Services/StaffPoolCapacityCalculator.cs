using SyncBook.Server.Models;

namespace SyncBook.Server.Services;

public static class StaffPoolCapacityCalculator
{
    public static bool HasStaffConflict(
        IEnumerable<Appointment> staffAppointments,
        DateTime startUtc,
        DateTime endUtc)
    {
        return staffAppointments
            .Where(a => a.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed)
            .Any(a => AppointmentSchedulingHelper.Overlaps(a, startUtc, endUtc));
    }

    public static bool IsStaffAvailable(
        StaffMember staff,
        DateTime startUtc,
        DateTime endUtc,
        IEnumerable<Appointment> dayAppointments)
    {
        if (!staff.IsBookable)
        {
            return false;
        }

        if (!StaffScheduleCoverageHelper.CoversInterval(staff, startUtc, endUtc))
        {
            return false;
        }

        var staffAppointments = dayAppointments.Where(a => a.StaffId == staff.Id);
        return !HasStaffConflict(staffAppointments, startUtc, endUtc);
    }

    public static List<StaffMember> GetAvailableStaff(
        IEnumerable<StaffMember> pool,
        DateTime startUtc,
        DateTime endUtc,
        IEnumerable<Appointment> dayAppointments) =>
        pool
            .Where(s => IsStaffAvailable(s, startUtc, endUtc, dayAppointments))
            .OrderBy(s => s.Id)
            .ToList();

    public static bool HasPoolCapacity(
        IEnumerable<StaffMember> pool,
        DateTime startUtc,
        DateTime endUtc,
        IEnumerable<Appointment> dayAppointments) =>
        GetAvailableStaff(pool, startUtc, endUtc, dayAppointments).Count > 0;

    public static StaffMember? SelectStaffForBooking(
        IEnumerable<StaffMember> pool,
        DateTime startUtc,
        DateTime endUtc,
        IEnumerable<Appointment> dayAppointments) =>
        GetAvailableStaff(pool, startUtc, endUtc, dayAppointments).FirstOrDefault();

    public static bool HasLegacyBusinessConflict(
        IEnumerable<Appointment> dayAppointments,
        DateTime startUtc,
        DateTime endUtc)
    {
        var legacyAppointments = dayAppointments
            .Where(a => string.IsNullOrEmpty(a.StaffId)
                && a.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed);

        return AppointmentSchedulingHelper.HasConflict(legacyAppointments, startUtc, endUtc);
    }
}
