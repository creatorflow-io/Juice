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
        day = 0,
        weekDay = 1,
        weekendDay = 2
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
        public TimeSpan? OccursEvery { get; set; } // miliseconds
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
            var Now = DateTimeOffset.Now;
            var recurs = daily.RecursEvery < 1 ? 1 : daily.RecursEvery; // recurs every x day(s)/week(s)/month(s)

            if (frequency.Occurs == OccursType.Daily)
            {
                if (daily.OccursOnceAt.HasValue)
                {
                    var time = daily.OccursOnceAt.Value;
                    var last = lastProcessed?.AddDays(recurs) ?? DateTimeOffset.Now;
                    if (last < DateTimeOffset.Now && (DateTimeOffset.Now - last).TotalDays > 30)
                    {
                        last = DateTimeOffset.Now;
                    }
                    var next = new DateTimeOffset(last.Year, last.Month, last.Day, time.Hours, time.Minutes, time.Seconds, last.Offset);
                    while (next < DateTimeOffset.Now)
                    {
                        last = next;
                        next = new DateTimeOffset(last.Year, last.Month, last.Day + recurs, time.Hours, time.Minutes, time.Seconds, last.Offset);
                    }
                    return next;
                }
                else
                {

                    var occursEvery = daily.OccursEvery ?? new TimeSpan(0, 0, 30); // miliseconds
                    var last = lastProcessed ?? Now;
                    if (last < Now && (Now - last).TotalDays > 10) // max 10 day(s) from last to now
                    {
                        last = Now;
                    }

                    var startingAt = daily.StartingAt ?? new TimeSpan(0, 0, 0);
                    var duration = daily.Duration ?? new TimeSpan(23, 59, 59);
                    if (duration > new TimeSpan(23, 59, 59))
                    {
                        duration = new TimeSpan(23, 59, 59);
                    }

                    var starting = new DateTimeOffset(last.Year, last.Month, last.Day, startingAt.Hours, startingAt.Minutes, startingAt.Seconds, last.Offset);

                    if (last < starting)
                    {
                        starting = starting.AddDays(-1);
                    }
                    var ending = starting.Add(duration);

                    // > ending time on @date
                    while (Now > ending)
                    {
                        starting = starting.AddDays(recurs);
                        ending = starting.Add(duration);
                    }

                    var next = last.Add(occursEvery);
                    while (next < Now)
                    {
                        next = next.Add(occursEvery);
                    }

                    if (next < starting) // if next occurs < starting time of date, return starting time of date
                    {
                        return starting;
                    }

                    if (next > ending) // if next occurs > ending time of date, return starting time of next recurs date
                    {
                        return starting.AddDays(recurs);
                    }
                    return next;
                }
            }
            else if (frequency.Occurs == OccursType.Weekly)
            {
                recurs = recurs * 7;
                if (daily.OccursOnceAt.HasValue)
                {
                    var time = daily.OccursOnceAt.Value;
                    if (frequency.Weekly.OnDays?.Any() ?? false)
                    {
                        var last = lastProcessed ?? DateTimeOffset.Now;

                        var startOfWeek = last.StartOfWeek(frequency.Weekly.StartOfWeek);
                        var endOfWeek = last.EndOfWeek(frequency.Weekly.StartOfWeek);

                        while (Now > endOfWeek) // while Now is later ending of range, calc next range
                        {
                            startOfWeek = startOfWeek.AddDays(recurs);
                            endOfWeek = endOfWeek.AddDays(recurs);
                        }

                        if (last < startOfWeek)
                        {
                            return new DateTimeOffset(startOfWeek, last.Offset).Add(time);
                        }

                        var daysOccurs = new DateTimeOffset(startOfWeek, last.Offset).DaysOccursOfWeek(frequency.Weekly.OnDays); // each day occurs in week
                        foreach (var day in daysOccurs)
                        {
                            var next = new DateTimeOffset(day.Date, day.Offset).Add(time);
                            if (next > last)
                            {
                                return new DateTimeOffset(day.Date, day.Offset).Add(time);
                            }
                        }
                        while (true)
                        {
                            startOfWeek = startOfWeek.AddDays(recurs);
                            daysOccurs = new DateTimeOffset(startOfWeek, last.Offset).DaysOccursOfWeek(frequency.Weekly.OnDays); // each day occurs in week
                            foreach (var day in daysOccurs)
                            {
                                var next = new DateTimeOffset(day.Date, day.Offset).Add(time);
                                if (next > last)
                                {
                                    return new DateTimeOffset(day.Date, day.Offset).Add(time);
                                }
                            }
                        }
                    }
                    else
                    {
                        var last = lastProcessed?.AddDays(recurs) ?? DateTimeOffset.Now;
                        if (last < DateTimeOffset.Now && (DateTimeOffset.Now - last).TotalDays > 30)
                        {
                            last = DateTimeOffset.Now;
                        }
                        var next = new DateTimeOffset(last.Date, last.Offset).Add(time);
                        while (next < DateTimeOffset.Now)
                        {
                            last = next;
                            next = last.AddDays(recurs);
                        }
                        return next;
                    }

                }
                else
                {
                    var occursEvery = daily.OccursEvery ?? new TimeSpan(0, 0, 30); // miliseconds
                    var last = lastProcessed ?? Now;
                    if (last < Now && (Now - last).TotalDays > 10) // max 10 day(s) from last to now
                    {
                        last = Now;
                    }

                    var startingAt = daily.StartingAt ?? new TimeSpan(0, 0, 0);
                    var duration = daily.Duration ?? new TimeSpan(23, 59, 59);
                    if (duration > new TimeSpan(23, 59, 59))
                    {
                        duration = new TimeSpan(23, 59, 59);
                    }

                    var next = last.Add(occursEvery);


                    if (frequency.Weekly.OnDays?.Any() ?? false)
                    {
                        // finding time range from Now
                        var startingOfWeek = last.StartingOfWeek(frequency.Weekly.OnDays, startingAt);
                        var endingOfWeek = startingOfWeek.EndingOfWeekByStarting(frequency.Weekly.OnDays, startingAt, duration);

                        while (Now > endingOfWeek) // while Now is later ending of range, calc next range
                        {
                            startingOfWeek = startingOfWeek.AddDays(recurs);
                            endingOfWeek = endingOfWeek.AddDays(recurs);
                        }

                        while (next < Now)
                        {
                            next = next.Add(occursEvery);
                        }

                        while (next > endingOfWeek) // while next > ending of range, calc next range
                        {
                            startingOfWeek = startingOfWeek.AddDays(recurs);
                            endingOfWeek = endingOfWeek.AddDays(recurs);
                        }

                        if (next < startingOfWeek) // if next < starting of range, return starting
                        {
                            return startingOfWeek;
                        }

                        var daysOccurs = startingOfWeek.DaysOccursOfWeek(frequency.Weekly.OnDays); // each day occurs in week
                        foreach (var day in daysOccurs)
                        {
                            // calc time range in day
                            var starting = new DateTimeOffset(day.Year, day.Month, day.Day, startingAt.Hours, startingAt.Minutes, startingAt.Seconds, last.Offset);

                            var ending = starting.Add(duration);
                            if (next >= starting && next <= ending)
                            {
                                break;
                            }

                            if (next <= starting)
                            {
                                return starting;
                            }
                            else if (next <= ending)
                            {
                                return next;
                            }
                        }

                    }

                    else
                    {
                        var starting = new DateTimeOffset(last.Year, last.Month, last.Day, startingAt.Hours, startingAt.Minutes, startingAt.Seconds, last.Offset);

                        if (last < starting)
                        {
                            starting = starting.AddDays(-1);
                        }

                        var ending = starting.Add(duration);

                        // > ending time on @date
                        while (Now > ending)
                        {
                            starting = starting.AddDays(recurs);
                            ending = starting.Add(duration);
                        }

                        while (next < Now)
                        {
                            next = next.Add(occursEvery);
                        }

                        if (next < starting) // if next occurs < starting time of date, return starting time of date
                        {
                            return starting;
                        }

                        if (next > ending) // if next occurs > ending time of date, return starting time of next recurs date
                        {
                            return starting.AddDays(recurs);
                        }
                    }

                    return next;
                }
            }
            else if (frequency.Occurs == OccursType.Monthly)
            {
                if (daily.OccursOnceAt.HasValue)
                {
                    var time = daily.OccursOnceAt.Value;
                    if (frequency.Monthly.On.HasValue && (frequency.Monthly.SpecialDay.HasValue || frequency.Monthly.DayOfWeek.HasValue))
                    {
                        var last = lastProcessed ?? DateTimeOffset.Now;

                        var next = last.SpecialDayOfMonth(frequency.Monthly.On.Value, frequency.Monthly.SpecialDay, frequency.Monthly.DayOfWeek).Add(time);

                        while (next < DateTimeOffset.Now || next <= last)
                        {
                            next = next.AddMonths(recurs).SpecialDayOfMonth(frequency.Monthly.On.Value, frequency.Monthly.SpecialDay, frequency.Monthly.DayOfWeek).Add(time);
                        }
                        return next;
                    }
                    else
                    {
                        var onDay = frequency.Monthly.OnDay ?? 1;
                        var last = lastProcessed ?? DateTimeOffset.Now;

                        var next = new DateTimeOffset(last.Year, last.Month, onDay, time.Hours, time.Minutes, time.Seconds, last.Offset);
                        if (next.Month == last.Month)
                        {
                            return next > last ? next : next.AddMonths(1);
                        }
                        while (next < DateTimeOffset.Now || next <= last)
                        {
                            next = next.AddMonths(recurs);
                        }
                        return next;
                    }

                }
                else
                {
                    var occursEvery = daily.OccursEvery ?? new TimeSpan(0, 0, 30); // miliseconds
                    var last = lastProcessed ?? Now;

                    var startingAt = daily.StartingAt ?? new TimeSpan(0, 0, 0);
                    var duration = daily.Duration ?? new TimeSpan(23, 59, 59);
                    if (duration > new TimeSpan(23, 59, 59))
                    {
                        duration = new TimeSpan(23, 59, 59);
                    }

                    var next = last.Add(occursEvery);


                    if (frequency.Monthly.On.HasValue && (frequency.Monthly.SpecialDay.HasValue || frequency.Monthly.DayOfWeek.HasValue))
                    {
                        // finding time range from Now

                        var day = last.SpecialDayOfMonth(frequency.Monthly.On.Value, frequency.Monthly.SpecialDay, frequency.Monthly.DayOfWeek);

                        var starting = day.Add(startingAt);

                        var ending = starting.Add(duration);

                        while (Now > ending || next > ending)
                        {
                            starting = starting.AddMonths(recurs)
                                .SpecialDayOfMonth(frequency.Monthly.On.Value, frequency.Monthly.SpecialDay, frequency.Monthly.DayOfWeek)
                                .Add(startingAt);
                            ending = starting.Add(duration);
                        }

                        while (next < Now)
                        {
                            next = next.Add(occursEvery);
                        }

                        if (next <= starting)
                        {
                            return starting;
                        }
                        else if (next <= ending)
                        {
                            return next;
                        }

                    }

                    else
                    {
                        var onDay = frequency.Monthly.OnDay ?? 1;

                        var starting = new DateTimeOffset(last.Year, last.Month, onDay, startingAt.Hours, startingAt.Minutes, startingAt.Milliseconds, last.Offset);

                        var ending = starting.Add(duration);


                        // > ending time on @date

                        while (Now > ending || next > ending)
                        {
                            starting = starting.AddMonths(recurs);
                            ending = starting.Add(duration);
                        }
                        if (next < starting && Now < starting) // if next occurs < starting time of date, return starting time of date
                        {
                            return starting;
                        }
                        while (next < Now)
                        {
                            next = next.Add(occursEvery);
                        }

                        if (next < starting) // if next occurs < starting time of date, return starting time of date
                        {
                            return starting;
                        }

                    }

                    return next;
                }
            }
            return null;
        }

        public static DateTime StartOfWeek(this DateTimeOffset dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }

        public static DateTime EndOfWeek(this DateTimeOffset dt, DayOfWeek startOfWeek)
        {
            return dt.StartOfWeek(startOfWeek).AddDays(7);
        }

        public static DateTimeOffset StartingOfWeek(this DateTimeOffset dt, IEnumerable<DayOfWeek> daysOfWeek, TimeSpan startingAt)
        {
            var startDayOfWeek = daysOfWeek.OrderBy(d => (int)d).First();
            var startOfWeek = dt.StartOfWeek(startDayOfWeek);
            return new DateTimeOffset(startOfWeek.Year, startOfWeek.Month, startOfWeek.Day, startingAt.Hours, startingAt.Minutes, startingAt.Seconds, dt.Offset);
        }

        public static DateTimeOffset EndingOfWeekByStarting(this DateTimeOffset dt, IEnumerable<DayOfWeek> daysOfWeek, TimeSpan startingAt, TimeSpan duration)
        {
            var endDayOfWeek = daysOfWeek.OrderByDescending(d => (int)d).First();
            var endOfWeek = dt.StartOfWeek(endDayOfWeek);
            var end = new DateTimeOffset(endOfWeek.Year, endOfWeek.Month, endOfWeek.Day, startingAt.Hours, startingAt.Minutes, startingAt.Seconds, dt.Offset).Add(duration);
            return end > dt ? end : end.AddDays(7);
        }

        public static DateTimeOffset EndingOfWeek(this DateTimeOffset dt, IEnumerable<DayOfWeek> daysOfWeek, TimeSpan startingAt, TimeSpan duration)
        {
            return dt.StartingOfWeek(daysOfWeek, startingAt).EndingOfWeekByStarting(daysOfWeek, startingAt, duration);
        }

        public static IEnumerable<DateTimeOffset> DaysOccursOfWeek(this DateTimeOffset dt, IEnumerable<DayOfWeek> daysOfWeek, DayOfWeek startDayOfWeek = DayOfWeek.Sunday)
        {
            //var startDayOfWeek = daysOfWeek.OrderBy(d => d.IntValue()).First();
            var startOfWeek = dt.StartOfWeek(startDayOfWeek);
            return daysOfWeek.Select(d => { var day = new DateTimeOffset(dt.StartOfWeek(d), dt.Offset); return day >= startOfWeek ? day : day.AddDays(7); });
        }

        public static DateTimeOffset SpecialDayOfMonth(this DateTimeOffset dt, DaySequence on, SpecialDay? specialDay, DayOfWeek? dayOfWeek)
        {

            if (specialDay.HasValue)
            {
                if (specialDay == SpecialDay.day)
                {
                    var day = (int)on;

                    if (day > 0)
                    {
                        return new DateTimeOffset(dt.Year, dt.Month, day, 0, 0, 0, dt.Offset);
                    }
                    else
                    {
                        return new DateTimeOffset(dt.Year, dt.Month, 1, 0, 0, 0, dt.Offset).AddMonths(1).AddDays(-1);
                    }
                }
                else if (specialDay == SpecialDay.weekDay)
                {
                    var day = (int)on;

                    if (day > 0)
                    {
                        var firstOfMonth = new DateTimeOffset(dt.Year, dt.Month, 1, 0, 0, 0, dt.Offset);
                        while (!firstOfMonth.IsWeekDay())
                        {
                            firstOfMonth = firstOfMonth.AddDays(1);
                        }
                        if (day > 1)
                        {
                            firstOfMonth = firstOfMonth.AddDays(day - 1);
                            while (!firstOfMonth.IsWeekDay())
                            {
                                firstOfMonth = firstOfMonth.AddDays(1);
                            }
                        }
                        return firstOfMonth;
                    }
                    else
                    {
                        var lastOfMonth = new DateTimeOffset(dt.Year, dt.Month, 1, 0, 0, 0, dt.Offset).AddMonths(1).AddDays(-1);
                        while (!lastOfMonth.IsWeekDay())
                        {
                            lastOfMonth = lastOfMonth.AddDays(-1);
                        }
                        return lastOfMonth;
                    }
                }
                else
                //if(specialDay == SpecialDay.weekendDay)
                {
                    var week = (int)on;

                    if (week > 0)
                    {
                        var firstOfMonth = new DateTimeOffset(dt.Year, dt.Month, 1, 0, 0, 0, dt.Offset);
                        while (!firstOfMonth.IsWeekendDay())
                        {
                            firstOfMonth = firstOfMonth.AddDays(1);
                        }
                        if (week > 1)
                        {
                            firstOfMonth = firstOfMonth.AddDays((week - 1) * 7);
                        }
                        return firstOfMonth;
                    }
                    else
                    {
                        var lastOfMonth = new DateTimeOffset(dt.Year, dt.Month, 1, 0, 0, 0, dt.Offset).AddMonths(1).AddDays(-1);
                        while (!lastOfMonth.IsWeekendDay())
                        {
                            lastOfMonth = lastOfMonth.AddDays(-1);
                        }
                        return lastOfMonth;
                    }
                }
            }
            else
            {
                dayOfWeek = dayOfWeek ?? DayOfWeek.Monday;
                var week = (int)on;

                if (week > 0)
                {
                    var firstOfMonth = new DateTimeOffset(dt.Year, dt.Month, 1, 0, 0, 0, dt.Offset);
                    while (firstOfMonth.DayOfWeek != dayOfWeek)
                    {
                        firstOfMonth = firstOfMonth.AddDays(1);
                    }
                    if (week > 1)
                    {
                        firstOfMonth = firstOfMonth.AddDays((week - 1) * 7);
                    }
                    return firstOfMonth;
                }
                else
                {
                    var lastOfMonth = new DateTimeOffset(dt.Year, dt.Month, 1, 0, 0, 0, dt.Offset).AddMonths(1).AddDays(-1);
                    while (lastOfMonth.DayOfWeek != dayOfWeek)
                    {
                        lastOfMonth = lastOfMonth.AddDays(-1);
                    }
                    return lastOfMonth;
                }
            }
        }

        public static bool IsWeekDay(this DateTimeOffset dt)
        {
            return dt.DayOfWeek != DayOfWeek.Saturday && dt.DayOfWeek != DayOfWeek.Sunday;
        }

        public static bool IsWeekendDay(this DateTimeOffset dt)
        {
            return !dt.IsWeekDay();
        }
    }
}
