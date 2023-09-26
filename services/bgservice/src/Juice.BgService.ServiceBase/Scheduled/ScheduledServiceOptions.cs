namespace Juice.BgService.Scheduled
{
    public class ScheduledServiceOptions
    {
        public Frequency[] Frequencies { get; set; } = Array.Empty<Frequency>();
        public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan GetHealthCheckTimeout()
        {
            return HealthCheckTimeout.Negate();
        }

        public ScheduledServiceOptions OccursOnce()
        {
            Frequencies = new Frequency[1] { Frequency.Once() };
            return this;
        }

        public ScheduledServiceOptions OccursInterval(TimeSpan delay)
        {
            Frequencies = new Frequency[1] { Frequency.Interval(delay) };
            return this;
        }
    }

    public class Frequency
    {
        public bool RunOnStartup { get; set; }
        public OccursType Occurs { get; set; }
        public DailyFrequency Daily { get; set; }
        public WeeklyFrequency Weekly { get; set; }
        public MonthlyFrequency Monthly { get; set; }
        private bool _occurred;
        public bool IsOccurred => _occurred;
        public void Occurred()
        {
            _occurred = true;
        }

        public static Frequency Once()
        {
            return new Frequency { RunOnStartup = true, Occurs = OccursType.Once };
        }

        public static Frequency Interval(TimeSpan delay)
        {
            return new Frequency
            {
                RunOnStartup = true,
                Occurs = OccursType.Daily,
                Daily = new DailyFrequency
                {
                    OccursEvery = delay
                }
            };
        }
    }

    public enum OccursType
    {
        Any = 0,
        Daily = 1,
        Weekly = 2,
        Monthly = 3,
        Once = 4
    }

    public enum SpecialDay
    {
        Day = 0,
        Weekday = 1,
        Weekend = 2
    }

    public enum DaySequence
    {
        First = 1,
        Second = 2,
        Third = 3,
        Fourth = 4,
        Last = 0
    }

    public class DailyFrequency
    {
        public int RecursEvery { get; set; } // day(s)/week(s)/month(s)
        public TimeSpan? OccursOnceAt { get; set; }
        public TimeSpan? OccursEvery { get; set; } // seconds
        public TimeSpan? StartingAt { get; set; }
        public TimeSpan? Duration { get; set; }
    }

    public class WeeklyFrequency : DailyFrequency
    {
        public DayOfWeek[] OnDays { get; set; }
        public DayOfWeek StartOfWeek { get; set; }
    }

    public class MonthlyFrequency : DailyFrequency
    {
        public int? OnDay { get; set; }
        public DayOfWeek? DayOfWeek { get; set; }
        public DaySequence? On { get; set; }
        public SpecialDay? SpecialDay { get; set; }
    }

    public static class FrequencyExtensions
    {

        public static DateTimeOffset? NextOccursAt(this IEnumerable<Frequency> frequencies, DateTimeOffset? lastProcessed, bool isStartup = false)
        {
            return frequencies.Select(f => (f.Occurs != OccursType.Once || !f.IsOccurred) ? f.NextOccursAt(lastProcessed, isStartup) : default)
                .Where(n => n.HasValue)
                .OrderBy(n => n.Value)
                .FirstOrDefault();
        }

        public static DateTimeOffset? NextOccursAt(this Frequency frequency, DateTimeOffset? lastProcessed, bool isStartup = false)
        {
            if (frequency == null) { return null; }
            if (isStartup && frequency.RunOnStartup) { return DateTimeOffset.Now; }

            var daily = frequency.Occurs == OccursType.Daily ? frequency.Daily
                : frequency.Occurs == OccursType.Weekly ? frequency.Weekly as DailyFrequency
                : frequency.Occurs == OccursType.Monthly ? frequency.Monthly as DailyFrequency
                : null;

            if (daily == null)
            {
                return null;
            }

            if (frequency.Occurs == OccursType.Daily)
            {
                return frequency.Daily.NextOccursAt(lastProcessed);
            }
            else if (frequency.Occurs == OccursType.Weekly)
            {
                return frequency.Weekly.NextOccursAt(lastProcessed);
            }
            else if (frequency.Occurs == OccursType.Monthly)
            {
                return frequency.Monthly.NextOccursAt(lastProcessed);
            }
            return null;
        }
        #region Daily

        /// <summary>
        /// Get next occurs time of DailyFrequency
        /// </summary>
        /// <param name="daily"></param>
        /// <param name="lastProcessed"></param>
        /// <returns></returns>
        public static DateTimeOffset NextOccursAt(this DailyFrequency daily, DateTimeOffset? lastProcessed)
        {
            var last = lastProcessed ?? DateTimeOffset.Now;
            var recurs = daily.RecursEvery < 1 ? 1 : daily.RecursEvery;

            if (daily.OccursOnceAt.HasValue)
            {
                return daily.GetDailyOccursOnceAt(last.Date, last.Offset);
            }
            else
            {
                var occursEvery = daily.OccursEvery ?? new TimeSpan(0, 0, 30); // seconds

                var (start, end) = daily.GetDailyOccursRange(last.Date, last.Offset);

                // slide start and end to next recurs date
                if (end < last)
                {
                    start = start.AddDays(recurs);
                    end = end.AddDays(recurs);
                }

                if (last < start)
                {
                    return start;
                }

                var next = last > start ? last : start;
                while (next <= last)
                {
                    next = next.Add(occursEvery);
                }

                if (next > end) // if next occurs > ending time of date, return starting time of next recurs date
                {
                    return start.AddDays(recurs);
                }
                return next;
            }
        }

        /// <summary>
        /// Get time range of daily occurs on date
        /// </summary>
        /// <param name="daily"></param>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static (DateTimeOffset Start, DateTimeOffset End) GetDailyOccursRange(this DailyFrequency daily, DateTime dt, TimeSpan offset)
        {
            var startingAt = daily.StartingAt ?? new TimeSpan(0, 0, 0);
            var duration = daily.Duration ?? new TimeSpan(23, 59, 59);
            var start = new DateTimeOffset(dt, offset).Add(startingAt);
            var end = start + duration;
            var endOfDay = new DateTimeOffset(dt, offset).AddDays(1).AddTicks(-1);
            if (end > endOfDay)
            {
                end = endOfDay;
            }
            return (start, end);
        }

        /// <summary>
        /// Get nearest occurs time of daily on today or next day when it occurs once at
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static DateTimeOffset GetDailyOccursOnceAt(this DailyFrequency daily, DateTime lastOccuredDate, TimeSpan offset)
        {
            var time = daily.OccursOnceAt!.Value;
            var nextDate = lastOccuredDate.AddDays(1);

            return new DateTimeOffset(nextDate.Add(time), offset);
        }
        #endregion

        #region Weekly
        /// <summary>
        /// Get next occurs time of WeeklyFrequency
        /// </summary>
        /// <param name="lastProcessed"></param>
        /// <returns></returns>
        public static DateTimeOffset NextOccursAt(this WeeklyFrequency weekly, DateTimeOffset? lastProcessed)
        {
            var now = DateTimeOffset.Now;
            var today = now.Date;
            var nextDate =
                lastProcessed.HasValue ? weekly.NextOccursDate(lastProcessed.Value.Date)
                : weekly.IsOccursOn(today) ? today
                : weekly.NextOccursDate(today);

            if (weekly.OccursOnceAt.HasValue)
            {
                var last = lastProcessed ?? now;
                var time = weekly.OccursOnceAt!.Value;
                var next = new DateTimeOffset(nextDate, last.Offset).Add(time);

                while (next < last)
                {
                    nextDate = weekly.NextOccursDate(nextDate);
                    next = nextDate.Add(time);
                }
                return next;
            }
            else
            {
                var (start, end) = weekly.GetDailyOccursRange(nextDate, lastProcessed?.Offset ?? now.Offset);
                while (now > end)
                {
                    nextDate = weekly.NextOccursDate(nextDate);
                    (start, end) = weekly.GetDailyOccursRange(nextDate, lastProcessed?.Offset ?? now.Offset);
                }

                if (now < start)
                {
                    return start;
                }

                var next = (DateTimeOffset)(lastProcessed.HasValue && lastProcessed > start ? lastProcessed! : start);
                var occursEvery = weekly.OccursEvery ?? new TimeSpan(0, 0, 30); // seconds
                while (next < now)
                {
                    next = next.Add(occursEvery);
                }

                if (next > end) // if next occurs > ending time of date, return starting time of next recurs date
                {
                    nextDate = weekly.NextOccursDate(nextDate);
                    (start, end) = weekly.GetDailyOccursRange(nextDate, lastProcessed?.Offset ?? now.Offset);
                    return start;
                }
                return next;
            }

        }

        /// <summary>
        /// Get next occurs date of WeeklyFrequency
        /// </summary>
        /// <param name="last"></param>
        /// <returns></returns>
        public static DateTime NextOccursDate(this WeeklyFrequency weekly, DateTime last)
        {
            var recurs = weekly.RecursEvery < 1 ? 1 : weekly.RecursEvery;
            var onDays = weekly.OnDays ?? Array.Empty<DayOfWeek>();
            var anyDay = !onDays.Any() || onDays.Intersect(Enum.GetValues<DayOfWeek>()).Count() == Enum.GetValues<DayOfWeek>().Count();

            var endOfLastWeek = last.EndOfWeek(weekly.StartOfWeek);

            var next = anyDay ? last.AddDays(1) : last.NextSequenceDate(onDays);

            if (next > endOfLastWeek)
            {
                next = last.StartOfWeek(weekly.StartOfWeek).AddDays(recurs * 7);
                if (!anyDay && !onDays.Contains(next.DayOfWeek))
                {
                    next = next.NextSequenceDate(onDays);
                }
            }
            return next;
        }

        public static bool IsOccursOn(this WeeklyFrequency weekly, DateTime dt)
        {
            var onDays = weekly.OnDays ?? Array.Empty<DayOfWeek>();
            var anyDay = !onDays.Any() || onDays.Intersect(Enum.GetValues<DayOfWeek>()).Count() == Enum.GetValues<DayOfWeek>().Count();
            return anyDay || onDays.Contains(dt.DayOfWeek);
        }

        /// <summary>
        /// Get next date of sequence days
        /// </summary>
        /// <param name="last"></param>
        /// <param name="onDays"></param>
        /// <returns></returns>
        public static DateTime NextSequenceDate(this DateTime last, IEnumerable<DayOfWeek> onDays)
        {
            var next = last.AddDays(1);
            while (!onDays.Contains(next.DayOfWeek))
            {
                next = next.AddDays(1);
            }
            return next;
        }
        #endregion

        #region Monthly

        /// <summary>
        /// Get next occurs time of MonthlyFrequency
        /// </summary>
        /// <param name="monthly"></param>
        /// <param name="lastProcessed"></param>
        /// <returns></returns>
        ///
        public static DateTimeOffset NextOccursAt(this MonthlyFrequency monthly, DateTimeOffset? lastProcessed)
        {
            var now = DateTimeOffset.Now;
            var today = now.Date;

            var last = lastProcessed ?? now;
            var nextDate =
               lastProcessed.HasValue ?
               (monthly.IsOccursOn(last.Date) ? last.Date
               : monthly.NextOccursDate(lastProcessed.Value.Date))
               : (monthly.IsOccursOn(today) ? today
               : monthly.NextOccursDate(today));

            if (monthly.OccursOnceAt.HasValue)
            {
                var time = monthly.OccursOnceAt!.Value;

                var next = new DateTimeOffset(nextDate, last.Offset).Add(time);

                while (next < last)
                {
                    nextDate = monthly.NextOccursDate(nextDate);
                    next = nextDate.Add(time);
                }
                return next;
            }
            else
            {
                var (start, end) = monthly.GetDailyOccursRange(nextDate, last.Offset);
                while (last > end)
                {
                    nextDate = monthly.NextOccursDate(nextDate);
                    (start, end) = monthly.GetDailyOccursRange(nextDate, last.Offset);
                }
                if (last < start)
                {
                    return start;
                }
                var next = (DateTimeOffset)(lastProcessed.HasValue && lastProcessed > start ? lastProcessed! : start);
                var occursEvery = monthly.OccursEvery ?? new TimeSpan(0, 0, 30); // seconds
                while (next <= last)
                {
                    next = next.Add(occursEvery);
                }
                if (next > end) // if next occurs > ending time of date, return starting time of next recurs date
                {
                    nextDate = monthly.NextOccursDate(nextDate);
                    (start, end) = monthly.GetDailyOccursRange(nextDate, last.Offset);
                    return start;
                }
                return next;
            }
        }

        /// <summary>
        /// Get next occurs date of MonthlyFrequency
        /// </summary>
        /// <param name="last"></param>
        /// <returns></returns>
        public static DateTime NextOccursDate(this MonthlyFrequency monthly, DateTime last)
        {
            var recurs = monthly.RecursEvery < 1 ? 1 : monthly.RecursEvery;

            if (monthly.OnDay.HasValue)
            {
                var onDay = monthly.OnDay.Value;

                var next = new DateTime(last.Year, last.Month, onDay);
                if (last < next)
                {
                    return next;
                }
                return next.AddMonths(recurs);
            }
            else if (monthly.On.HasValue && (monthly.SpecialDay.HasValue || monthly.DayOfWeek.HasValue))
            {
                var next = monthly.SpecialDay.HasValue
                    ? last.SpecialDayOfMonth(monthly.On.Value, monthly.SpecialDay.Value)
                    : last.SpecifiedWeekdayOfMonth(monthly.On.Value, monthly.DayOfWeek!.Value);
                if (last < next)
                {
                    return next;
                }
                next = new DateTime(next.Year, next.Month, 1).AddMonths(recurs);
                return monthly.SpecialDay.HasValue
                    ? next.SpecialDayOfMonth(monthly.On.Value, monthly.SpecialDay.Value)
                    : next.SpecifiedWeekdayOfMonth(monthly.On.Value, monthly.DayOfWeek!.Value);
            }
            throw new InvalidOperationException("Invalid monthly frequency");
        }

        public static DateTime SpecialDayOfMonth(this DateTime dt, DaySequence on, SpecialDay specialDay)
        {
            if (specialDay == SpecialDay.Weekday)
            {
                if (on == DaySequence.Last)
                {
                    var lastDay = DateTime.DaysInMonth(dt.Year, dt.Month);
                    var lastWeekday = new DateTime(dt.Year, dt.Month, lastDay);
                    while (!lastWeekday.IsWeekday())
                    {
                        lastWeekday = lastWeekday.AddDays(-1);
                    }
                    return lastWeekday;
                }
                else
                {
                    var firstDay = new DateTime(dt.Year, dt.Month, 1);
                    return firstDay.AddDays((int)on - 1);
                }
            }
            else if (specialDay == SpecialDay.Weekend)
            {
                if (on == DaySequence.Last)
                {
                    var lastDay = DateTime.DaysInMonth(dt.Year, dt.Month);
                    var lastWeekend = new DateTime(dt.Year, dt.Month, lastDay);
                    while (lastWeekend.IsWeekday())
                    {
                        lastWeekend = lastWeekend.AddDays(-1);
                    }
                    return lastWeekend;
                }
                else
                {
                    var firstWeekend = new DateTime(dt.Year, dt.Month, 1);
                    while (firstWeekend.IsWeekday())
                    {
                        firstWeekend = firstWeekend.AddDays(1);
                    }
                    return firstWeekend.AddDays(((int)on - 1) * 7);
                }
            }

            // default to day of month
            if (on == DaySequence.Last)
            {
                return new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month));
            }
            else
            {
                return new DateTime(dt.Year, dt.Month, (int)on);
            }
        }

        public static DateTime SpecifiedWeekdayOfMonth(this DateTime dt, DaySequence on, DayOfWeek day)
        {
            if (on == DaySequence.Last)
            {
                var lastDay = DateTime.DaysInMonth(dt.Year, dt.Month);
                var lastWeekday = new DateTime(dt.Year, dt.Month, lastDay);
                while (lastWeekday.DayOfWeek < day)
                {
                    lastWeekday = lastWeekday.AddDays(-1);
                }
                return lastWeekday;
            }
            else
            {
                var firstDay = new DateTime(dt.Year, dt.Month, 1);
                while (firstDay.DayOfWeek < day)
                {
                    firstDay = firstDay.AddDays(1);
                }
                return firstDay.AddDays(((int)on - 1) * 7);
            }
        }

        public static bool IsOccursOn(this MonthlyFrequency monthly, DateTime dt)
        {
            if (monthly.OnDay.HasValue)
            {
                return dt.Day == monthly.OnDay.Value;
            }
            else if (monthly.On.HasValue && (monthly.SpecialDay.HasValue || monthly.DayOfWeek.HasValue))
            {
                if (monthly.SpecialDay.HasValue)
                {
                    return dt.SpecialDayOfMonth(monthly.On.Value, monthly.SpecialDay.Value) == dt;
                }
                else
                {
                    return dt.SpecifiedWeekdayOfMonth(monthly.On.Value, monthly.DayOfWeek!.Value) == dt;
                }
            }
            throw new InvalidOperationException("Invalid monthly frequency");
        }
        #endregion

        public static bool IsWeekday(this DateTime dt)
        {
            return dt.DayOfWeek != DayOfWeek.Saturday && dt.DayOfWeek != DayOfWeek.Sunday;
        }

        public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }

        public static DateTime EndOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            return dt.StartOfWeek(startOfWeek).AddDays(7);
        }

    }
}
