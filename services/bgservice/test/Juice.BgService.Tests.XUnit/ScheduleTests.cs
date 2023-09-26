using FluentAssertions;
using Juice.BgService.Scheduled;
using Xunit.Abstractions;

namespace Juice.BgService.Tests.XUnit
{
    public class ScheduleTests
    {
        private ITestOutputHelper _output;

        public ScheduleTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Daily_occurs_once()
        {
            var daily = new DailyFrequency
            {
                OccursOnceAt = new TimeSpan(10, 0, 0)
            };

            var now = DateTimeOffset.Now;
            if (now.Hour > 10)
            {
                now = now.AddDays(1);
            }
            var nextOccurs = daily.NextOccursAt(null);

            nextOccurs.Should().Be(now.Date.AddHours(10));
            var nextOccurs1 = daily.NextOccursAt(nextOccurs);

            nextOccurs1.Should().Be(now.Date.AddHours(10).AddDays(1));
        }

        [Fact]
        public void Daily_recurring()
        {
            var daily = new DailyFrequency
            {
                OccursEvery = new TimeSpan(1, 0, 0)
            };

            var now = DateTimeOffset.Now;
            var last = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
            for (var i = 0; i < 10; i++)
            {
                var nextOccurs = daily.NextOccursAt(last);
                nextOccurs.Should().Be(last.AddHours(1));
                last = nextOccurs;
            }
        }

        [Fact]
        public void Daily_should_occurs_next_day()
        {
            var daily = new DailyFrequency
            {
                OccursEvery = new TimeSpan(1, 0, 0),
                StartingAt = new TimeSpan(7, 0, 0),
                Duration = new TimeSpan(10, 0, 0)
            };

            var now = new DateTimeOffset(DateTime.Now.Ticks, TimeSpan.Zero);
            var last = new DateTimeOffset(now.Year, now.Month, now.Day, 16, 30, 0, now.Offset);
            var nextOccurs = daily.NextOccursAt(last);
            nextOccurs.Should().Be(new DateTimeOffset(now.Year, now.Month, now.Day, 7, 0, 0, now.Offset).AddDays(1));
        }

        [Fact]
        public void Weekly_should_occurs_once_on_Thu_7AM_every_2weeks_starting_from_2020_01_02()
        {
            var weekly = new WeeklyFrequency
            {
                OccursOnceAt = new TimeSpan(7, 0, 0),
                OnDays = new[] { DayOfWeek.Thursday },
                StartOfWeek = DayOfWeek.Monday,
                RecursEvery = 2
            };

            var last = new DateTimeOffset(2020, 1, 2, 7, 0, 0, TimeSpan.Zero);

            var nextDate = weekly.NextOccursDate(last.Date);
            nextDate.Should().Be(new DateTime(2020, 1, 16));
            for (var i = 0; i < 10; i++)
            {
                var nextOccurs = weekly.NextOccursAt(last);
                nextOccurs.Should().Be(last.AddDays(14));
                last = nextOccurs;
            }
        }

        [Fact]
        public void Special_day_of_month()
        {
            var date = new DateTime(2020, 1, 1);
            for (var i = 0; i < 12; i++)
            {
                var lastWeekday = date.SpecialDayOfMonth(DaySequence.Last, SpecialDay.Weekday);
                _output.WriteLine(lastWeekday.ToString("yyyy-MM-dd ddd"));
                lastWeekday.IsWeekday().Should().BeTrue();
                var nextDayIsWeekend = !lastWeekday.AddDays(1).IsWeekday();
                var nextDayIsNextMonth = lastWeekday.AddDays(1).Month != lastWeekday.Month;
                (nextDayIsWeekend || nextDayIsNextMonth).Should().BeTrue();
                date = date.AddMonths(1);
            }
        }

        [Fact]
        public void Monthly_should_occurs_on_last_workday_of_every_month()
        {
            var monthly = new MonthlyFrequency
            {
                OccursEvery = new TimeSpan(1, 0, 0),
                On = DaySequence.Last,
                SpecialDay = SpecialDay.Weekday,
                StartingAt = new TimeSpan(8, 0, 0),
                Duration = new TimeSpan(2, 0, 0)
            };
            var last = new DateTimeOffset(2020, 1, 2, 8, 0, 0, TimeSpan.Zero);

            last.Date.SpecialDayOfMonth(DaySequence.Last, SpecialDay.Weekday).Should().Be(new DateTime(2020, 1, 31));
            monthly.NextOccursDate(last.Date).Should().Be(new DateTime(2020, 1, 31));

            var nextOccurs = monthly.NextOccursAt(last);
            nextOccurs.Should().Be(new DateTimeOffset(2020, 1, 31, 8, 0, 0, TimeSpan.Zero));

            last = nextOccurs;
            nextOccurs = monthly.NextOccursAt(last);
            nextOccurs.Should().Be(new DateTimeOffset(2020, 1, 31, 9, 0, 0, TimeSpan.Zero));

            last = nextOccurs;
            nextOccurs = monthly.NextOccursAt(last);
            nextOccurs.Should().Be(new DateTimeOffset(2020, 1, 31, 10, 0, 0, TimeSpan.Zero));

            last = nextOccurs;

            var nextDate = monthly.NextOccursDate(last.Date);
            nextDate.Should().Be(new DateTime(2020, 2, 28));

            var (start, end) = monthly.GetDailyOccursRange(nextDate, last.Offset);
            start.Should().Be(new DateTimeOffset(2020, 2, 28, 8, 0, 0, TimeSpan.Zero));
            end.Should().Be(new DateTimeOffset(2020, 2, 28, 10, 0, 0, TimeSpan.Zero));

            nextOccurs = monthly.NextOccursAt(last);
            nextOccurs.Should().Be(new DateTimeOffset(2020, 2, 28, 8, 0, 0, TimeSpan.Zero));
        }
    }
}
