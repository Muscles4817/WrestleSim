using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.Person;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Part 2 of the living-world plan: careers that move. Mechanisms and comparisons — who
    /// grows more than whom, and why — not anybody's fate.
    /// </summary>
    public class GrowthTests
    {
        private static readonly DateOnly Day = new(2026, 1, 5);

        private static Wrestler Real(string ringName) =>
            DataLoaders.LoadEmbeddedWrestlers().First(w => w.RingName == ringName);

        private static Wrestler Youngest() =>
            DataLoaders.LoadEmbeddedWrestlers().Where(w => w.BornIn is not null).OrderByDescending(w => w.BornIn).First();

        /// <summary>A year of months; <paramref name="matches"/> a month, against <paramref name="against"/>.</summary>
        private static void Year(Wrestler w, int matches, Wrestler? against = null, DateOnly? from = null)
        {
            var start = from ?? Day;
            for (int m = 1; m <= 12; m++)
            {
                for (int k = 0; k < matches; k++) Growth.RecordMatch(w, [against ?? w]);
                Growth.Step(w, start.AddMonths(m));
            }
        }

        [Fact]
        public void NothingChangesOnDayOne()
        {
            var career = new Career { Promotion = new Promotion(), StartDate = Day, CurrentDate = Day, Roster = DataLoaders.LoadEmbeddedWrestlers() };
            var before = DataLoaders.LoadEmbeddedWrestlers();
            Growth.Begin(career);

            for (int i = 0; i < before.Count; i++)
                foreach (var g in Growth.All)
                    Assert.Equal(Growth.Level(before[i], g), Growth.Level(career.Roster[i], g), 9);
        }

        [Fact]
        public void ACareerSetInAnotherYearStartsWithEverybodyThatAge()
        {
            var later = new DateOnly(2032, 1, 5);
            var career = new Career { Promotion = new Promotion(), StartDate = later, CurrentDate = later, Roster = DataLoaders.LoadEmbeddedWrestlers() };
            Growth.Begin(career);
            var orton = career.Roster.First(w => w.RingName == "Randy Orton");
            var then = Real("Randy Orton");

            double body = 1 - orton.Physical.Agility / (double)then.Physical.Agility;
            double head = 1 - orton.Mental.Psychology / (double)then.Mental.Psychology;
            Assert.True(body > 0.05, "Six years on, the body has gone some way.");
            Assert.True(head < body / 3, "The head much less.");
        }

        [Fact]
        public void SomebodyYoungWhoWorksImproves_AndSomebodyWhoDoesNotStaysPut()
        {
            var worked = Youngest();
            var idle = Youngest();
            worked.Development = Growth.Read(worked, Day.Year);
            idle.Development = Growth.Read(idle, Day.Year);
            double craft = Growth.Level(idle, SkillGroup.Craft);

            for (int y = 0; y < 3; y++)
            {
                Year(worked, matches: 4, from: Day.AddYears(y));
                Year(idle, matches: 0, from: Day.AddYears(y));
            }

            Assert.True(Growth.Level(worked, SkillGroup.Craft) > craft + 4,
                $"{craft:0.0} -> {Growth.Level(worked, SkillGroup.Craft):0.0} in three busy years");
            Assert.Equal(craft, Growth.Level(idle, SkillGroup.Craft), 6);
        }

        [Fact]
        public void TheFasterLearnerGetsFurther()
        {
            var quick = Youngest();
            var slow = Youngest();
            quick.Development = Growth.Read(quick, Day.Year);
            slow.Development = Growth.Read(slow, Day.Year);
            quick.Development.Speed[(int)SkillGroup.Craft] = 1.4;
            slow.Development.Speed[(int)SkillGroup.Craft] = 0.6;

            Year(quick, 4);
            Year(slow, 4);

            Assert.True(Growth.Level(quick, SkillGroup.Craft) > Growth.Level(slow, SkillGroup.Craft));
        }

        [Fact]
        public void YouLearnMoreFromSomebodyBetter()
        {
            var a = Youngest();
            var b = Youngest();
            a.Development = Growth.Read(a, Day.Year);
            b.Development = Growth.Read(b, Day.Year);

            for (int y = 0; y < 3; y++)
            {
                Year(a, 2, against: Real("Randy Orton"), from: Day.AddYears(y));
                Year(b, 2, against: Youngest(), from: Day.AddYears(y));
            }

            Assert.True(Growth.Level(a, SkillGroup.Craft) > Growth.Level(b, SkillGroup.Craft));
        }

        [Fact]
        public void AgeTakesTheBodyWhetherOrNotTheyWork()
        {
            var veteran = Real("Randy Orton");
            veteran.Development = Growth.Read(veteran, Day.Year);
            double body = Growth.Level(veteran, SkillGroup.Athletic);

            for (int y = 0; y < 5; y++) Year(veteran, 4, from: Day.AddYears(y));

            Assert.True(Growth.Level(veteran, SkillGroup.Athletic) < body - 3);
        }

        [Fact]
        public void ConditioningIsTheGym_ItGrowsWithoutMatches()
        {
            var w = Youngest();
            w.Development = Growth.Read(w, Day.Year);
            w.Development.Ceiling[(int)SkillGroup.Conditioning] = 99;   // room to grow
            double before = Growth.Level(w, SkillGroup.Conditioning);

            Year(w, matches: 0);

            Assert.True(Growth.Level(w, SkillGroup.Conditioning) > before);
        }

        [Fact]
        public void AHurtMonthGrowsNothingAndCostsTheBody()
        {
            var w = Youngest();
            w.Development = Growth.Read(w, Day.Year);
            w.Development.Ceiling[(int)SkillGroup.Craft] = 99;
            double craft = Growth.Level(w, SkillGroup.Craft);
            double body = Growth.Level(w, SkillGroup.Athletic);

            for (int k = 0; k < 4; k++) Growth.RecordMatch(w, [w]);
            Growth.RecordHurt(w);
            var moved = Growth.Step(w, Day.AddMonths(1));

            Assert.Equal(craft, Growth.Level(w, SkillGroup.Craft), 6);
            Assert.True(moved[(int)SkillGroup.Athletic] < 0);
        }

        [Fact]
        public void WhatTheyDoInTheMatchIsWhatTheyGetBetterAt()
        {
            var w = Real("Randy Orton");   // not a high-flyer
            w.Development = Growth.Read(w, Day.Year);
            var result = new MatchEngineResult
            {
                Winner = w, Loser = w,
                BeatResults = [new BeatResult { BeatType = BeatType.HighSpot, Worker = w }, new BeatResult { BeatType = BeatType.HighSpot, Worker = w }]
            };

            Growth.RecordMatch(w, [Real("Kofi Kingston")], result);

            Assert.Equal(1.0, w.Development.Worked[(int)SkillGroup.Athletic], 6);
            Assert.Equal(0.0, w.Development.Worked[(int)Growth.StyleGroup(w.Style)], 6);
        }

        [Fact]
        public void WithoutBeatsAMatchIsWorkedInTheirOwnStyle()
        {
            var w = Real("Randy Orton");
            w.Development = Growth.Read(w, Day.Year);
            Growth.RecordMatch(w, [Real("Kofi Kingston")]);
            Assert.Equal(1.0, w.Development.Worked[(int)Growth.StyleGroup(w.Style)], 6);
        }

        [Fact]
        public void RealPeopleAreReadFromTheirOwnNumbers_FurtherForTheirYearsReadsFaster()
        {
            var w = Youngest();
            var ahead = Youngest();
            ahead.Mental.Psychology += 15; ahead.Mental.RingIQ += 15; ahead.Mental.Selling += 15;

            var d = Growth.Read(w, Day.Year);
            var dAhead = Growth.Read(ahead, Day.Year);

            Assert.True(dAhead.Speed[(int)SkillGroup.Craft] > d.Speed[(int)SkillGroup.Craft]);
            Assert.Equal(d.Speed, Growth.Read(Youngest(), Day.Year).Speed);
            Assert.All(Growth.All, g => Assert.True(d.Ceiling[(int)g] >= Growth.Level(w, g) - 1e-9));
        }

        [Fact]
        public void AStyleSaysNothingAboutAGroupItNeverTrained()
        {
            var flyer = DataLoaders.LoadEmbeddedWrestlers().First(w => w.Style == WrestlingStyle.HighFlyer);
            Assert.True(Growth.Judged(flyer, SkillGroup.Athletic));
            Assert.False(Growth.Judged(flyer, SkillGroup.Mat));
            Assert.True(Growth.Judged(flyer, SkillGroup.Craft));
        }

        [Fact]
        public void TheClockStepsEverybodyOnTheFirstOfTheMonth_AndASaveKeepsIt()
        {
            var day = new DateOnly(2026, 1, 25);
            var career = new Career
            {
                Promotion = new Promotion { Name = "Test", Tier = PromotionTier.Independent },
                StartDate = day, CurrentDate = day, Roster = DataLoaders.LoadEmbeddedWrestlers()
            };
            Growth.Begin(career);
            for (int i = 0; i < 40; i++) career.AdvanceOneDay();

            Assert.All(career.Roster, w => Assert.True(w.Development!.History.Count >= 2));
            var orton = career.Roster.First(w => w.RingName == "Randy Orton");
            orton.Mental.Psychology = 61;   // something the database does not say

            var back = SaveSerializer.FromJson(SaveSerializer.ToJson(career), DataLoaders.LoadEmbeddedWrestlers());
            var again = back.Roster.First(w => w.RingName == "Randy Orton");
            Assert.Equal(61, again.Mental.Psychology);
            Assert.Equal(orton.Development!.Speed, again.Development!.Speed);
            Assert.Equal(orton.Development.History.Count, again.Development.History.Count);
        }

        [Fact]
        public void AnOldSaveIsAgedToItsYearOnLoad()
        {
            var day = new DateOnly(2031, 3, 1);
            var career = new Career
            {
                Promotion = new Promotion { Name = "Test", Tier = PromotionTier.Independent },
                StartDate = day, CurrentDate = day, Roster = DataLoaders.LoadEmbeddedWrestlers()
            };
            // Written as a save from before careers moved would have been: no development at all.
            var json = SaveSerializer.ToJson(career);
            var back = SaveSerializer.FromJson(json, DataLoaders.LoadEmbeddedWrestlers());

            var orton = back.Roster.First(w => w.RingName == "Randy Orton");
            Assert.NotNull(orton.Development);
            Assert.True(orton.Physical.Agility < Real("Randy Orton").Physical.Agility);
        }

        [Fact]
        public void ThePlayerKnowsMoreTheLongerSomebodyWorksForThem()
        {
            var career = new Career
            {
                Promotion = new Promotion { Name = "Test", Tier = PromotionTier.Independent },
                StartDate = Day, CurrentDate = Day, Roster = DataLoaders.LoadEmbeddedWrestlers()
            };
            Growth.Begin(career);
            var w = career.Roster.First();

            Assert.All(Scouting.Of(career, w).Groups, g => Assert.Null(g.Learns));

            career.MakeRegular(w);
            career.CurrentDate = Day.AddMonths(14);
            Assert.Contains(Scouting.Of(career, w).Groups, g => g.Learns is not null);
        }
    }
}
