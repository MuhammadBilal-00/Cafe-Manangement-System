using Cafe.Models;

namespace Cafe.Services
{
    /// <summary>Phase 2: time-based menu availability (daily window + day-of-week mask).</summary>
    public static class MenuAvailability
    {
        public static bool IsAvailable(MenuItem m, DateTime now)
        {
            if (!m.Availability) return false;

            // Day-of-week mask: bit0=Sun … bit6=Sat.
            var dayBit = 1 << (int)now.DayOfWeek;
            if ((m.AvailableDaysMask & dayBit) == 0) return false;

            // Daily time window (only enforced when both ends are set).
            if (m.AvailableFromTime.HasValue && m.AvailableToTime.HasValue)
            {
                var t = now.TimeOfDay;
                var from = m.AvailableFromTime.Value;
                var to = m.AvailableToTime.Value;
                if (from <= to)
                {
                    if (t < from || t > to) return false;
                }
                else // window wraps past midnight
                {
                    if (t < from && t > to) return false;
                }
            }
            return true;
        }
    }
}
