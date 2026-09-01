using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Match-count decay per pairing — docs/wrestling-reference/20-storylines-and-feuds.md
    /// §9.1, with freshness recovering over time per 17-heat-and-getting-over.md §4.2.
    ///
    /// The rule the whole feature exists to enforce: the fourth match between the same two
    /// people is worth substantially less than the first, and only absence brings it back.
    /// </summary>
    public class MatchCountDecayTests
    {
        private static readonly DateOnly Day0 = new(2025, 1, 6);

        private static Feud FeudOf(Wrestler a, Wrestler b) =>
            new() { WrestlerA = a, WrestlerB = b, Intensity = FeudIntensity.None };

        // ── The curve ────────────────────────────────────────────────────────

        [Fact]
        public void TheCurveFollowsTheReferenceTable()
        {
            // Doc 20 §9.1: 1st 100%, 2nd 85–95%, 3rd 90–110% as a blow-off,
            // 4th and beyond 50–70% and falling.
            Assert.Equal(1.00, Feud.FamiliarityFor(1), 3);

            Assert.InRange(Feud.FamiliarityFor(2), 0.85, 0.95);
            Assert.InRange(Feud.FamiliarityFor(3, blowOff: true), 0.90, 1.10);

            Assert.InRange(Feud.FamiliarityFor(4), 0.50, 0.70);
            Assert.InRange(Feud.FamiliarityFor(5), 0.50, 0.70);
        }

        [Fact]
        public void TheFourthMatchIsWorthSubstantiallyLessThanTheFirst()
        {
            Assert.True(Feud.FamiliarityFor(4) <= Feud.FamiliarityFor(1) * 0.7);
        }

        [Fact]
        public void PastTheFourthItKeepsFallingToAFloor()
        {
            Assert.True(Feud.FamiliarityFor(5) < Feud.FamiliarityFor(4));
            Assert.True(Feud.FamiliarityFor(6) < Feud.FamiliarityFor(5));

            // It bottoms out rather than reaching zero — a match nobody wants still
            // happens in front of somebody.
            Assert.Equal(Feud.FamiliarityFor(9), Feud.FamiliarityFor(40), 3);
            Assert.True(Feud.FamiliarityFor(40) > 0.3);
        }

        [Fact]
        public void AHotThirdMeetingReadsAsTheBlowOffAndAColdOneDoesNot()
        {
            Assert.True(Feud.FamiliarityFor(3, blowOff: true) > Feud.FamiliarityFor(3, blowOff: false));
        }

        [Fact]
        public void PartlyForgottenSeriesLandBetweenTheRows()
        {
            double between = Feud.FamiliarityFor(3.5);
            Assert.InRange(between, Feud.FamiliarityFor(4), Feud.FamiliarityFor(3));
        }

        // ── Counting meetings ────────────────────────────────────────────────

        [Fact]
        public void EachMatchAdvancesTheMeetingNumber()
        {
            var feud = FeudOf(TestRoster.Babyface, TestRoster.Heel);

            Assert.Equal(1.0, feud.NextMeetingNumber(Day0), 3);

            feud.RecordMatch(Day0);
            Assert.Equal(2.0, feud.NextMeetingNumber(Day0), 3);

            feud.RecordMatch(Day0.AddDays(30));
            feud.RecordMatch(Day0.AddDays(60));

            Assert.Equal(4.0, feud.NextMeetingNumber(Day0.AddDays(90)), 3);
            Assert.Equal(3, feud.MatchCount);
        }

        [Fact]
        public void AFeudRunAtANormalPaceForgetsNothingBetweenItsOwnMatches()
        {
            // Weekly TV into a monthly blow-off must not be allowed to launder its own
            // repetition — hence the grace period.
            var feud = FeudOf(TestRoster.Babyface, TestRoster.Heel);

            feud.RecordMatch(Day0);
            feud.RecordMatch(Day0.AddDays(28));
            feud.RecordMatch(Day0.AddDays(56));

            Assert.Equal(4.0, feud.NextMeetingNumber(Day0.AddDays(84)), 3);
            Assert.InRange(feud.Familiarity(Day0.AddDays(84)), 0.50, 0.70);
        }

        // ── Recovery over time ───────────────────────────────────────────────

        [Fact]
        public void TimeOffBringsThePairingBack()
        {
            var feud = FeudOf(TestRoster.Babyface, TestRoster.Heel);
            feud.RecordMatch(Day0);
            feud.RecordMatch(Day0.AddDays(28));
            feud.RecordMatch(Day0.AddDays(56));

            double immediately = feud.Familiarity(Day0.AddDays(84));
            double eightMonthsOn = feud.Familiarity(Day0.AddDays(56 + 243));
            double threeYearsOn = feud.Familiarity(Day0.AddDays(56 + 1095));

            Assert.True(eightMonthsOn > immediately);
            Assert.True(threeYearsOn > eightMonthsOn);

            // The brief's case: a rematch eight months later is not a fourth match.
            Assert.True(eightMonthsOn > Feud.FamiliarityFor(3));

            // Left long enough it is as good as new.
            Assert.Equal(1.0, threeYearsOn, 3);
        }

        [Fact]
        public void RecoveryOnlyStartsAfterTheGracePeriod()
        {
            var feud = FeudOf(TestRoster.Babyface, TestRoster.Heel);
            feud.RecordMatch(Day0);

            Assert.Equal(2.0, feud.NextMeetingNumber(Day0.AddDays(Feud.FreshnessGraceDays)), 3);
            Assert.True(feud.NextMeetingNumber(Day0.AddDays(Feud.FreshnessGraceDays + 30)) < 2.0);
        }

        [Fact]
        public void RecoveredTimeIsBankedWhenTheyMeetAgain()
        {
            // Once the crowd has forgotten a meeting it stays forgotten: the clock
            // restarts from the new match, it does not keep counting off the old one.
            var feud = FeudOf(TestRoster.Babyface, TestRoster.Heel);
            feud.RecordMatch(Day0);
            feud.RecordMatch(Day0.AddDays(20));
            feud.RecordMatch(Day0.AddDays(40));

            var comeback = Day0.AddDays(40 + 700);
            double remembered = feud.MeetingsRemembered(comeback);
            feud.RecordMatch(comeback);

            Assert.Equal(remembered + 1, feud.MeetingsRemembered(comeback), 3);
            Assert.Equal(4, feud.MatchCount);
        }

        [Fact]
        public void WithNoWorldClockNothingIsForgotten()
        {
            // Exhibition mode has no calendar, so a sandbox rematch cannot age its way
            // back to fresh.
            var feud = FeudOf(TestRoster.Babyface, TestRoster.Heel);
            feud.RecordMatch(null);
            feud.RecordMatch(null);
            feud.RecordMatch(null);

            Assert.Equal(4.0, feud.NextMeetingNumber(null), 3);
            Assert.Null(feud.LastMatchDate);
        }

        // ── The engine ───────────────────────────────────────────────────────

        private static MatchPlanModel Plan(Wrestler a, Wrestler b) => new()
        {
            WrestlerA = a,
            WrestlerB = b,
            Beats     = MatchStructureLibrary.Find("TV Formula")!.Beats.Select(x => x.Clone()).ToList()
        };

        [Fact]
        public void AStalePairingRatesLowerThanTheSamePlanRunFresh()
        {
            // Same wrestlers, same beats, same seed. The only difference is how sick of
            // it the room is.
            var a = TestRoster.Make("Fresh Freddie", overness: 80, charisma: 4.0);
            var b = TestRoster.Make("Stale Stanley", overness: 78, charisma: 3.8);

            double fresh = new MatchEngine(seed: 99).Execute(Plan(a, b), Feud.FamiliarityFor(1)).StarRating;
            double fourth = new MatchEngine(seed: 99).Execute(Plan(a, b), Feud.FamiliarityFor(4)).StarRating;

            Assert.True(fourth < fresh - 0.4,
                $"Expected a clear fall-off; got {fresh:F2}★ fresh vs {fourth:F2}★ on the fourth.");
        }

        [Fact]
        public void StalenessCostsTheRoomAndNotTheWork()
        {
            // The design decision this feature turns on: two good hands having their
            // fifth match still work well, the audience simply cares less.
            var a = TestRoster.Make("Fresh Freddie", overness: 80, charisma: 4.0);
            var b = TestRoster.Make("Stale Stanley", overness: 78, charisma: 3.8);

            var fresh = new MatchEngine(seed: 7).Execute(Plan(a, b), 1.0);
            var stale = new MatchEngine(seed: 7).Execute(Plan(a, b), Feud.FamiliarityFor(5));

            Assert.Equal(fresh.TechnicalScore, stale.TechnicalScore, 3);
            Assert.Equal(fresh.StorytellingScore, stale.StorytellingScore, 3);
            Assert.True(stale.CrowdAverageEnergy < fresh.CrowdAverageEnergy);
        }

        [Fact]
        public void TheResultReportsWhyTheRoomWasFlat()
        {
            var a = TestRoster.Babyface;
            var b = TestRoster.Heel;

            Assert.Null(new MatchEngine(seed: 3).Execute(Plan(a, b), 1.0).StalenessNote);
            Assert.NotNull(new MatchEngine(seed: 3).Execute(Plan(a, b), Feud.FamiliarityFor(4)).StalenessNote);
        }

        // ── The status economy ───────────────────────────────────────────────

        [Fact]
        public void AStaleWinTransfersLessStatus()
        {
            var winner = TestRoster.Make("Climbing Colin", overness: 55);
            var loser = TestRoster.Make("Established Eddie", overness: 85);

            var fresh = HeatEconomy.ForMatch(winner, loser, 4.0, FinishWeight.Decisive);
            var stale = HeatEconomy.ForMatch(winner, loser, 4.0, FinishWeight.Decisive,
                                             Feud.FamiliarityFor(5));

            Assert.True(stale.Winner.OvernessDelta < fresh.Winner.OvernessDelta);
            Assert.True(stale.Loser.OvernessDelta > fresh.Loser.OvernessDelta); // less far to fall
            Assert.True(stale.Winner.MomentumDelta < fresh.Winner.MomentumDelta);
        }

        // ── Wiring ───────────────────────────────────────────────────────────

        private static Show ShowOn(DateOnly date, params ICardItem[] items) =>
            new() { Name = "Test Show", Date = date.ToDateTime(TimeOnly.MinValue), Card = items.ToList(), TotalDurationMinutes = 180 };

        private static BookedMatch Booked(Wrestler a, Wrestler b) =>
            new() { Plan = Plan(a, b), StructureName = "TV Formula" };

        [Fact]
        public void RunningAShowStampsThePairingWithTheShowDate()
        {
            var a = TestRoster.Babyface;
            var b = TestRoster.Heel;
            var book = new FeudBook();

            new ShowSimulator(book, seed: 5).Simulate(ShowOn(Day0, Booked(a, b)));

            var feud = book.Find(a, b)!;
            Assert.Equal(Day0, feud.LastMatchDate);
            Assert.Equal(1, feud.MatchCount);
            Assert.Equal(1.0, feud.MeetingsRemembered(Day0), 3);
        }

        [Fact]
        public void TheSamePairingRunEveryMonthFallsOffAcrossFiveShows()
        {
            // The end-to-end shape of the rule: book the same two men on five consecutive
            // monthly cards and the ratings and the status swing both come down.
            var a = TestRoster.Make("Repeat Ricky", overness: 80, charisma: 4.0);
            var b = TestRoster.Make("Again Alan", overness: 78, charisma: 3.8);
            var book = new FeudBook();

            var ratings = new List<double>();
            for (int i = 0; i < 5; i++)
            {
                var result = new ShowSimulator(book, seed: 11).Simulate(
                    ShowOn(Day0.AddDays(28 * i), Booked(a, b)));
                ratings.Add(result.Items[0].MatchResult!.StarRating);
            }

            Assert.True(ratings[3] < ratings[0] * 0.8,
                $"Fourth meeting should fall away: {string.Join(", ", ratings.Select(r => r.ToString("F2")))}");
            Assert.True(ratings[4] < ratings[3]);
            Assert.Equal(5, book.Find(a, b)!.MatchCount);
        }

        [Fact]
        public void APairingNotBookedForAYearRatesBetterThanOneBookedStraightBack()
        {
            var book = new FeudBook();
            var a = TestRoster.Make("Rested Roy", overness: 80, charisma: 4.0);
            var b = TestRoster.Make("Rested Rita", overness: 78, charisma: 3.8);

            for (int i = 0; i < 3; i++)
                new ShowSimulator(book, seed: 21).Simulate(ShowOn(Day0.AddDays(28 * i), Booked(a, b)));

            var straightBack = new ShowSimulator(book, seed: 21)
                .Simulate(ShowOn(Day0.AddDays(84), Booked(a, b)))
                .Items[0].MatchResult!.StarRating;

            // Same three-match history, but rested for a year before the fourth.
            var rested = new FeudBook();
            var c = TestRoster.Make("Rested Roy", overness: 80, charisma: 4.0);
            var d = TestRoster.Make("Rested Rita", overness: 78, charisma: 3.8);
            for (int i = 0; i < 3; i++)
                new ShowSimulator(rested, seed: 21).Simulate(ShowOn(Day0.AddDays(28 * i), Booked(c, d)));

            var afterAYear = new ShowSimulator(rested, seed: 21)
                .Simulate(ShowOn(Day0.AddDays(56 + 365), Booked(c, d)))
                .Items[0].MatchResult!.StarRating;

            Assert.True(afterAYear > straightBack,
                $"A year off should help: {afterAYear:F2}★ rested vs {straightBack:F2}★ straight back.");
        }

        // ── Persistence ──────────────────────────────────────────────────────

        private static List<Wrestler> SaveRoster() =>
        [
            TestRoster.Make("Alpha One", overness: 80),
            TestRoster.Make("Beta Two", overness: 60)
        ];

        private static Career CareerWith(List<Wrestler> roster) => new()
        {
            Promotion   = new Promotion { Name = "Decay Wrestling", Tier = PromotionTier.National },
            StartDate   = Day0,
            CurrentDate = Day0,
            Roster      = roster
        };

        [Fact]
        public void FreshnessSurvivesARoundTrip()
        {
            var roster = SaveRoster();
            var career = CareerWith(roster);

            var feud = career.FeudBook.GetOrCreate(roster[0], roster[1]);
            feud.RecordMatch(Day0);
            feud.RecordMatch(Day0.AddDays(30));
            feud.RecordMatch(Day0.AddDays(60));

            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career), SaveRoster());
            var after = loaded.FeudBook.Find(
                loaded.FindWrestler("alpha-one")!, loaded.FindWrestler("beta-two")!)!;

            Assert.Equal(Day0.AddDays(60), after.LastMatchDate);
            Assert.Equal(3, after.MatchCount);
            Assert.Equal(feud.MeetingsRemembered(Day0.AddDays(90)),
                         after.MeetingsRemembered(Day0.AddDays(90)), 3);
            Assert.Equal(feud.Familiarity(Day0.AddDays(90)), after.Familiarity(Day0.AddDays(90)), 3);
        }

        [Fact]
        public void ASaveFromBeforeDecayTreatsItsMatchesAsRemembered()
        {
            // Old saves carry MatchCount and nothing else. Reading those meetings as
            // still-remembered is the conservative call — the alternative hands every
            // existing career a free reset on pairings it has already run into the ground.
            string json = SaveSerializer.ToJson(CareerWith(SaveRoster()))
                .Replace("\"Feuds\":[]",
                    "\"Feuds\":[{\"WrestlerA\":\"alpha-one\",\"WrestlerB\":\"beta-two\"," +
                    "\"Heat\":34,\"MatchCount\":3,\"History\":[]}]");

            // Guard the fixture: a rename of the DTO fields would otherwise leave this
            // test quietly asserting nothing.
            Assert.Contains("\"MatchCount\":3", json);

            var loaded = SaveSerializer.FromJson(json, SaveRoster());
            var feud = loaded.FeudBook.Find(
                loaded.FindWrestler("alpha-one")!, loaded.FindWrestler("beta-two")!)!;

            Assert.Equal(3, feud.MatchCount);
            Assert.Equal(3.0, feud.MeetingsRemembered(Day0), 3);
            Assert.Null(feud.LastMatchDate);
        }
    }
}
