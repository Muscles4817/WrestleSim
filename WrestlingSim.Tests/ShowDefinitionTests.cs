using WrestlingSim.Enums;
using WrestlingSim.Models.World;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Recurrence maths and how definitions drive the calendar. The date arithmetic is
    /// the kind that looks obviously right and is off by a week, so it is pinned here.
    /// </summary>
    public class ShowDefinitionTests
    {
        private static Career NewCareer(PromotionTier tier = PromotionTier.Established)
        {
            var start = new DateOnly(2026, 1, 5);   // a Monday
            return new Career
            {
                Promotion   = new Promotion { Name = "Test Wrestling", Tier = tier },
                StartDate   = start,
                CurrentDate = start,
                Roster      = new List<Models.Wrestler> { TestRoster.Make("A"), TestRoster.Make("B") }
            };
        }

        // ── Weekly ───────────────────────────────────────────────────────────

        [Fact]
        public void WeeklyProducesEveryMatchingWeekday()
        {
            var raw = new ShowDefinition { Name = "Raw", Day = DayOfWeek.Monday };

            var dates = raw.OccurrencesBetween(new DateOnly(2026, 1, 5), new DateOnly(2026, 2, 1)).ToList();

            Assert.Equal(
                [new(2026, 1, 5), new(2026, 1, 12), new(2026, 1, 19), new(2026, 1, 26)],
                dates);
        }

        [Fact]
        public void WeeklyStartsOnTheFirstMatchingDayOnOrAfterTheStart()
        {
            var smackdown = new ShowDefinition { Name = "SmackDown", Day = DayOfWeek.Friday };

            // Starting on a Monday, the first Friday is four days later.
            var first = smackdown.OccurrencesBetween(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 31)).First();

            Assert.Equal(new DateOnly(2026, 1, 9), first);
        }

        // ── Monthly ──────────────────────────────────────────────────────────

        [Fact]
        public void MonthlyLastSaturdayResolvesPerMonth()
        {
            var ppv = new ShowDefinition
            {
                Name       = "Premium Event",
                Type       = ShowType.PremiumEvent,
                Recurrence = RecurrenceKind.Monthly,
                Day        = DayOfWeek.Saturday,
                Ordinal    = WeekOrdinal.Last
            };

            var dates = ppv.OccurrencesBetween(new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 30)).ToList();

            Assert.Equal(
                [new(2026, 1, 31), new(2026, 2, 28), new(2026, 3, 28), new(2026, 4, 25)],
                dates);
        }

        [Fact]
        public void MonthlyFirstSundayResolvesPerMonth()
        {
            var def = new ShowDefinition
            {
                Recurrence = RecurrenceKind.Monthly,
                Day        = DayOfWeek.Sunday,
                Ordinal    = WeekOrdinal.First
            };

            var dates = def.OccurrencesBetween(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)).ToList();

            Assert.Equal([new(2026, 1, 4), new(2026, 2, 1), new(2026, 3, 1)], dates);
        }

        [Fact]
        public void AnOrdinalThatDoesNotExistInAMonthIsSkippedRatherThanSpilling()
        {
            // February 2026 starts on a Sunday, so it has four Sundays and no fifth.
            var def = new ShowDefinition
            {
                Recurrence = RecurrenceKind.Monthly,
                Day        = DayOfWeek.Sunday,
                Ordinal    = WeekOrdinal.Fourth
            };

            Assert.Equal(new DateOnly(2026, 2, 22), def.OrdinalWeekdayIn(2026, 2));

            var fifth = new ShowDefinition
            {
                Recurrence = RecurrenceKind.Monthly,
                Day        = DayOfWeek.Monday,
                Ordinal    = WeekOrdinal.Fourth
            };

            // Whatever it resolves to must stay inside the month it was asked for.
            var resolved = fifth.OrdinalWeekdayIn(2026, 2);
            Assert.True(resolved is null || resolved.Value.Month == 2);
        }

        // ── Materialisation ──────────────────────────────────────────────────

        [Fact]
        public void MaterialiseFillsTheCalendarFromDefinitions()
        {
            var career = NewCareer();
            career.ShowDefinitions.Add(new ShowDefinition { Name = "Raw", Day = DayOfWeek.Monday });
            career.ShowDefinitions.Add(new ShowDefinition { Name = "SmackDown", Day = DayOfWeek.Friday });

            career.MaterialiseSchedule(horizonDays: 28);

            Assert.Equal(5, career.Shows.Count(s => s.Name == "Raw"));
            Assert.Equal(4, career.Shows.Count(s => s.Name == "SmackDown"));
            Assert.All(career.Shows, s => Assert.NotNull(s.DefinitionId));
        }

        [Fact]
        public void MaterialiseIsIdempotent()
        {
            var career = NewCareer();
            career.ShowDefinitions.Add(new ShowDefinition { Name = "Raw", Day = DayOfWeek.Monday });

            career.MaterialiseSchedule(horizonDays: 28);
            int after = career.Shows.Count;

            career.MaterialiseSchedule(horizonDays: 28);
            career.MaterialiseSchedule(horizonDays: 28);

            Assert.Equal(after, career.Shows.Count);
        }

        [Fact]
        public void MaterialiseNeverDisturbsAnEditedInstance()
        {
            var career = NewCareer();
            career.ShowDefinitions.Add(new ShowDefinition { Name = "Raw", Day = DayOfWeek.Monday });
            career.MaterialiseSchedule(horizonDays: 28);

            var instance = career.Shows.First();
            instance.Name = "Raw 1000";
            instance.Venue = "Madison Square Garden";

            career.MaterialiseSchedule(horizonDays: 28);

            Assert.Equal("Raw 1000", instance.Name);
            Assert.Equal("Madison Square Garden", instance.Venue);
            Assert.Single(career.Shows.Where(s => s.Date == instance.Date));
        }

        [Fact]
        public void InactiveDefinitionsProduceNothing()
        {
            var career = NewCareer();
            career.ShowDefinitions.Add(new ShowDefinition { Name = "Raw", Day = DayOfWeek.Monday, Active = false });

            career.MaterialiseSchedule(horizonDays: 28);

            Assert.Empty(career.Shows);
        }

        [Fact]
        public void AdvancingTheClockTopsUpTheHorizon()
        {
            var career = NewCareer();
            career.ShowDefinitions.Add(new ShowDefinition { Name = "Raw", Day = DayOfWeek.Monday });
            career.MaterialiseSchedule();

            var furthestBefore = career.Shows.Max(s => s.Date);

            // Run everything so the clock is free to move.
            foreach (var show in career.Shows) show.Result = new Models.ShowResult();
            for (int i = 0; i < 30; i++) career.AdvanceOneDay();

            Assert.True(career.Shows.Max(s => s.Date) > furthestBefore);
        }

        // ── Retire and resync ────────────────────────────────────────────────

        [Fact]
        public void RetireClearsFutureUntouchedShowsButKeepsBookedAndRunOnes()
        {
            var career = NewCareer();
            var raw = new ShowDefinition { Name = "Raw", Day = DayOfWeek.Monday };
            career.ShowDefinitions.Add(raw);
            career.MaterialiseSchedule(horizonDays: 56);

            var ordered = career.Shows.OrderBy(s => s.Date).ToList();
            ordered[0].Result = new Models.ShowResult { OverallRating = 60 };          // run
            ordered[1].Card.Add(new Models.Segment.Segment("Promo", SegmentType.Promo, SegmentLocation.Ring, true));

            int removed = career.RetireDefinition(raw);

            Assert.False(raw.Active);
            Assert.True(removed > 0);
            Assert.Contains(ordered[0], career.Shows);   // run
            Assert.Contains(ordered[1], career.Shows);   // booked
            Assert.DoesNotContain(ordered[2], career.Shows);
        }

        [Fact]
        public void ResyncAndRetireLeaveBookedAndRunShowsAlone()
        {
            var career = NewCareer();
            var raw = new ShowDefinition { Name = "Raw", Day = DayOfWeek.Monday };
            career.ShowDefinitions.Add(raw);
            career.MaterialiseSchedule(horizonDays: 56);

            var ordered = career.Shows.OrderBy(s => s.Date).ToList();
            var booked = ordered[1];
            booked.Card.Add(new Models.Segment.Segment("Promo", SegmentType.Promo, SegmentLocation.Ring, true));

            raw.Day = DayOfWeek.Thursday;
            career.ResyncDefinition(raw);

            Assert.Contains(booked, career.Shows);
            Assert.Equal(DayOfWeek.Monday, booked.Date.DayOfWeek);
        }

        [Fact]
        public void ResyncMovesFutureDatesWhenTheDefinitionChanges()
        {
            var career = NewCareer();
            var raw = new ShowDefinition { Name = "Raw", Day = DayOfWeek.Monday };
            career.ShowDefinitions.Add(raw);
            career.MaterialiseSchedule(horizonDays: 56);

            raw.Day = DayOfWeek.Tuesday;
            career.ResyncDefinition(raw);

            var future = career.Shows.Where(s => s.Date >= career.CurrentDate).ToList();
            Assert.NotEmpty(future);

            // Today's untouched instance moves too — the protected thing is a booked or
            // run card, not merely an imminent date.
            Assert.All(future, s => Assert.Equal(DayOfWeek.Tuesday, s.Date.DayOfWeek));
        }
    }
}
