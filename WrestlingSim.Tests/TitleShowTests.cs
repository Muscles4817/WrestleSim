using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using Xunit;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Championships as they actually run: booked onto a card, resolved by the show
    /// simulator, and felt by the people in the match.
    /// </summary>
    public class TitleShowTests
    {
        private static readonly DateOnly ShowDay = new(2025, 6, 7);

        /// <summary>A two-beat plan whose finish is exactly the one asked for.</summary>
        private static BookedMatch Match(
            Wrestler a, Wrestler b, BeatType finish, BeatControl winner, Title? title = null) =>
            new()
            {
                Plan = new MatchPlanModel
                {
                    WrestlerA    = a,
                    WrestlerB    = b,
                    TitleAtStake = title,
                    Beats =
                    [
                        new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                        new MatchBeat { Type = finish, Control = winner }
                    ]
                }
            };

        private static Show ShowOf(params ICardItem[] items) => new()
        {
            Name                 = "Title Night",
            Date                 = ShowDay.ToDateTime(TimeOnly.MinValue),
            Card                 = items.ToList(),
            TotalDurationMinutes = 180
        };

        private static Title BeltHeldBy(TitleRegistry registry, Wrestler champion, double standing = 60)
        {
            var title = registry.Create("World Championship", TitleTier.World, Division.Mens, new DateOnly(2025, 1, 1));
            title.Standing = standing;
            title.Lineage.Add(new TitleReign
            {
                Champion    = champion,
                ReignNumber = 1,
                Won         = new DateOnly(2025, 1, 1)
            });
            return title;
        }

        // ── Booking a title match ────────────────────────────────────────────

        [Fact]
        public void ACleanWinInATitleMatchMovesTheBelt()
        {
            var champion   = TestRoster.Make("Champ", overness: 80);
            var challenger = TestRoster.Make("Challenger", overness: 76);
            var registry = new TitleRegistry();
            var title = BeltHeldBy(registry, champion);

            var show = ShowOf(Match(challenger, champion, BeatType.FinishClean, BeatControl.WrestlerA, title));
            var result = new ShowSimulator(new FeudBook(), seed: 7, titles: registry).Simulate(show);

            Assert.Same(challenger, title.Champion);
            Assert.Equal(2, title.ReignCount);
            Assert.Contains(result.TitleUpdates, u => u.Event == TitleEvent.Changed);
            Assert.Equal("Title Night", title.CurrentReign!.WonAt);
            Assert.Equal(ShowDay, title.CurrentReign.Won);
        }

        [Fact]
        public void ADisqualificationInATitleMatchDoesNotMoveTheBelt()
        {
            var champion   = TestRoster.Make("Champ", overness: 80);
            var challenger = TestRoster.Make("Challenger", overness: 76);
            var registry = new TitleRegistry();
            var title = BeltHeldBy(registry, champion);

            // The challenger wins the match. The belt does not follow.
            var show = ShowOf(Match(challenger, champion, BeatType.FinishDQ, BeatControl.WrestlerA, title));
            var result = new ShowSimulator(new FeudBook(), seed: 7, titles: registry).Simulate(show);

            Assert.Same(champion, title.Champion);
            Assert.Equal(1, title.ReignCount);
            Assert.Contains(result.TitleUpdates, u => u.Event == TitleEvent.RetainedOnATechnicality);
        }

        [Fact]
        public void ACountOutInATitleMatchDoesNotMoveTheBelt()
        {
            var champion   = TestRoster.Make("Champ", overness: 80);
            var challenger = TestRoster.Make("Challenger", overness: 76);
            var registry = new TitleRegistry();
            var title = BeltHeldBy(registry, champion);

            var show = ShowOf(Match(challenger, champion, BeatType.FinishCountout, BeatControl.WrestlerA, title));
            new ShowSimulator(new FeudBook(), seed: 3, titles: registry).Simulate(show);

            Assert.Same(champion, title.Champion);
        }

        [Fact]
        public void ASuccessfulDefenceIsCountedAndDated()
        {
            var champion   = TestRoster.Make("Champ", overness: 80);
            var challenger = TestRoster.Make("Challenger", overness: 76);
            var registry = new TitleRegistry();
            var title = BeltHeldBy(registry, champion);

            var show = ShowOf(Match(champion, challenger, BeatType.FinishClean, BeatControl.WrestlerA, title));
            new ShowSimulator(new FeudBook(), seed: 5, titles: registry).Simulate(show);

            Assert.Equal(1, title.CurrentDefences);
            Assert.Equal(ShowDay, title.CurrentReign!.LastDefended);
        }

        // ── The non-title loss, spotted by the registry ──────────────────────

        [Fact]
        public void AChampionLosingANonTitleMatchCostsTheBelt()
        {
            var champion = TestRoster.Make("Champ", overness: 80);
            var other    = TestRoster.Make("Other", overness: 76);
            var registry = new TitleRegistry();
            var title = BeltHeldBy(registry, champion);
            double before = title.Standing;

            // No title on the line — the standard booking shortcut.
            var show = ShowOf(Match(other, champion, BeatType.FinishClean, BeatControl.WrestlerA));
            var result = new ShowSimulator(new FeudBook(), seed: 5, titles: registry).Simulate(show);

            Assert.Same(champion, title.Champion);
            Assert.True(title.Standing < before, $"{before:F2} → {title.Standing:F2}");
            Assert.Contains(result.TitleUpdates, u => u.Event == TitleEvent.NonTitleLoss);
        }

        [Fact]
        public void AChampionWinningANonTitleMatchCostsNothing()
        {
            var champion = TestRoster.Make("Champ", overness: 80);
            var other    = TestRoster.Make("Other", overness: 60);
            var registry = new TitleRegistry();
            var title = BeltHeldBy(registry, champion);
            double before = title.Standing;

            var show = ShowOf(Match(champion, other, BeatType.FinishClean, BeatControl.WrestlerA));
            new ShowSimulator(new FeudBook(), seed: 5, titles: registry).Simulate(show);

            Assert.Equal(before, title.Standing, 6);
        }

        // ── Prestige changes what the match is worth ─────────────────────────

        [Fact]
        public void APrestigiousBeltOnTheLineRaisesTheCrowd()
        {
            var a = TestRoster.Make("A", overness: 70);
            var b = TestRoster.Make("B", overness: 70);

            var registry = new TitleRegistry();
            var big = BeltHeldBy(registry, a, standing: 95);

            var withTitle = new MatchEngine(seed: 11).Execute(
                Match(a, b, BeatType.FinishClean, BeatControl.WrestlerA, big).Plan);
            var without = new MatchEngine(seed: 11).Execute(
                Match(a, b, BeatType.FinishClean, BeatControl.WrestlerA).Plan);

            Assert.True(withTitle.CrowdAverageEnergy > without.CrowdAverageEnergy,
                $"{withTitle.CrowdAverageEnergy:F1} should beat {without.CrowdAverageEnergy:F1}");
            Assert.True(withTitle.FinalScore > without.FinalScore);
        }

        [Fact]
        public void WinningTheBeltTransfersStatusOnTopOfTheWin()
        {
            var champion   = TestRoster.Make("Champ", overness: 80);
            var challenger = TestRoster.Make("Challenger", overness: 60);
            var registry = new TitleRegistry();
            var title = BeltHeldBy(registry, champion, standing: 85);

            var show = ShowOf(Match(challenger, champion, BeatType.FinishClean, BeatControl.WrestlerA, title));
            var result = new ShowSimulator(new FeudBook(), seed: 9, titles: registry).Simulate(show);

            Assert.Contains(result.StatusChanges,
                c => c.Wrestler == challenger && c.Reason.StartsWith("Won the "));
        }

        // ── Validation ───────────────────────────────────────────────────────

        [Fact]
        public void ABeltCannotBeDefendedInAMatchItsChampionIsNotIn()
        {
            var champion = TestRoster.Make("Champ", overness: 80);
            var registry = new TitleRegistry();
            var title = BeltHeldBy(registry, champion);

            var plan = Match(TestRoster.Make("X"), TestRoster.Make("Y"),
                BeatType.FinishClean, BeatControl.WrestlerA, title).Plan;

            Assert.Contains(plan.Validate(), e => e.Contains("holds it"));
        }

        [Fact]
        public void ARetiredBeltCannotBeDefended()
        {
            var champion = TestRoster.Make("Champ", overness: 80);
            var registry = new TitleRegistry();
            var title = BeltHeldBy(registry, champion);
            registry.Retire(title, ShowDay);

            var plan = Match(champion, TestRoster.Make("Y"),
                BeatType.FinishClean, BeatControl.WrestlerA, title).Plan;

            Assert.Contains(plan.Validate(), e => e.Contains("retired"));
        }

        [Fact]
        public void AVacantBeltCanBeContestedByAnyone()
        {
            var registry = new TitleRegistry();
            var title = registry.Create("Vacant Belt", TitleTier.World, Division.Mens, new DateOnly(2025, 1, 1));

            var plan = Match(TestRoster.Make("X"), TestRoster.Make("Y"),
                BeatType.FinishClean, BeatControl.WrestlerA, title).Plan;

            Assert.Empty(plan.Validate());
        }

        // ── The clock ────────────────────────────────────────────────────────

        [Fact]
        public void AdvancingTheClockErodesAnUndefendedTitle()
        {
            var champion = TestRoster.Make("Champ", overness: 70);
            var career = new Career
            {
                Promotion   = new Promotion { Name = "Drift Wrestling" },
                StartDate   = new DateOnly(2025, 1, 1),
                CurrentDate = new DateOnly(2025, 1, 1),
                Roster      = [champion]
            };

            var title = career.Titles.Create("World Championship", TitleTier.World, Division.Mens, career.StartDate);
            title.Standing = 60;
            title.Lineage.Add(new TitleReign { Champion = champion, ReignNumber = 1, Won = career.StartDate });

            for (int i = 0; i < 150; i++) career.AdvanceOneDay();

            Assert.True(title.Standing < 55,
                $"150 days with no defence should show, got {title.Standing:F1}");
        }
    }
}
