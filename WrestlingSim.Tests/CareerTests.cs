using WrestlingSim.Enums;
using WrestlingSim.Models.World;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The clock and the calendar. A career is the only place time exists, so these cover
    /// the rules that make scheduling a commitment rather than a note.
    /// </summary>
    public class CareerTests
    {
        private static Career NewCareer(PromotionTier tier = PromotionTier.Established)
        {
            var start = new DateOnly(2025, 1, 6);
            return new Career
            {
                Promotion   = new Promotion { Name = "Test Wrestling", Tier = tier },
                StartDate   = start,
                CurrentDate = start,
                Roster      = new List<Models.Wrestler> { TestRoster.Make("A"), TestRoster.Make("B") }
            };
        }

        [Fact]
        public void AdvanceOneDay_MovesTheClock()
        {
            var career = NewCareer();

            Assert.True(career.AdvanceOneDay());
            Assert.Equal(new DateOnly(2025, 1, 7), career.CurrentDate);
        }

        [Fact]
        public void AdvanceOneDay_RefusesToStepOverAShowThatIsDue()
        {
            var career = NewCareer();
            career.Schedule("Weekly", career.CurrentDate, ShowType.Television);

            Assert.True(career.HasShowDue);
            Assert.False(career.AdvanceOneDay());
            Assert.Equal(new DateOnly(2025, 1, 6), career.CurrentDate);
        }

        [Fact]
        public void AdvanceToNextShow_StopsOnTheShowsDate()
        {
            var career = NewCareer();
            career.Schedule("Weekly", career.CurrentDate.AddDays(5), ShowType.Television);

            int moved = career.AdvanceToNextShow();

            Assert.Equal(5, moved);
            Assert.Equal(new DateOnly(2025, 1, 11), career.CurrentDate);
            Assert.True(career.HasShowDue);
        }

        [Fact]
        public void AdvanceToNextShow_WithNothingScheduled_StillAdvances()
        {
            var career = NewCareer();

            int moved = career.AdvanceToNextShow(maxDays: 10);

            Assert.Equal(10, moved);
        }

        [Fact]
        public void ARunShowNoLongerBlocksTheClock()
        {
            var career = NewCareer();
            var show = career.Schedule("Weekly", career.CurrentDate, ShowType.Television);

            Assert.False(career.AdvanceOneDay());

            show.Result = new Models.ShowResult { OverallRating = 70 };

            Assert.False(career.HasShowDue);
            Assert.True(career.AdvanceOneDay());
        }

        [Fact]
        public void ScheduleSeedsRuntimeAndAttendanceFromTheTier()
        {
            var global = NewCareer(PromotionTier.Global);
            var local  = NewCareer(PromotionTier.Local);

            var big   = global.Schedule("Mania", global.CurrentDate.AddDays(30), ShowType.PremiumEvent);
            var small = local.Schedule("Hall Show", local.CurrentDate.AddDays(30), ShowType.PremiumEvent);

            Assert.True(big.RuntimeMinutes > small.RuntimeMinutes);
            Assert.True(big.Attendance > small.Attendance);
        }

        [Fact]
        public void TiersBelowEstablishedHaveNoTelevision()
        {
            Assert.False(new Promotion { Tier = PromotionTier.SuperIndie }.HasTelevision);
            Assert.True(new Promotion { Tier = PromotionTier.Established }.HasTelevision);

            Assert.DoesNotContain(
                ShowType.Television,
                new Promotion { Tier = PromotionTier.Independent }.AvailableShowTypes);
        }

        [Fact]
        public void CancelRemovesAnUnrunShowButKeepsARunOne()
        {
            var career = NewCareer();
            var upcoming = career.Schedule("Weekly", career.CurrentDate.AddDays(7), ShowType.Television);
            var done = career.Schedule("Last Week", career.CurrentDate.AddDays(1), ShowType.Television);
            done.Result = new Models.ShowResult { OverallRating = 60 };

            career.Cancel(upcoming);
            career.Cancel(done);

            Assert.DoesNotContain(upcoming, career.Shows);
            Assert.Contains(done, career.Shows);
        }

        [Fact]
        public void WrestlerIdIsStableAndDerivedFromRealName()
        {
            var a = TestRoster.Make("Chad Gable");
            var b = TestRoster.Make("Chad Gable");

            Assert.Equal(a.Id, b.Id);
            Assert.Equal("chad-gable", a.Id);

            // A gimmick change must not move the id — it is what saves key against.
            a.ChangeName("Master Gable");
            Assert.Equal("chad-gable", a.Id);
        }
    }
}
