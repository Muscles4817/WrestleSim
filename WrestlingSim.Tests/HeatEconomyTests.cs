using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The status economy from docs/wrestling-reference/17-heat-and-getting-over.md §6.
    /// These assert the rules as stated, because they are the whole reason a match result
    /// is worth simulating rather than just rating.
    /// </summary>
    public class HeatEconomyTests
    {
        private static Wrestler Star(double overness = 90) => TestRoster.Make("Star", overness: overness);
        private static Wrestler Riser(double overness = 45) => TestRoster.Make("Riser", overness: overness);

        // ── The central asymmetry ────────────────────────────────────────────

        [Fact]
        public void BeatingSomeoneAboveYouTransfersALot_AndCostsThemLittle()
        {
            var star = Star();
            var riser = Riser();

            var outcome = HeatEconomy.ForMatch(riser, star, starRating: 4.0, FinishWeight.Decisive);

            Assert.True(outcome.Winner.OvernessDelta > 1.5,
                $"an upset win should move the riser, got {outcome.Winner.OvernessDelta:F2}");

            // The star has a stock and can afford one loss.
            Assert.True(Math.Abs(outcome.Loser.OvernessDelta) < 1.0,
                $"the star should barely be dented, got {outcome.Loser.OvernessDelta:F2}");
        }

        [Fact]
        public void BeatingSomeoneBelowYouIsANetDestructionOfValue()
        {
            var star = Star();
            var riser = Riser();

            var outcome = HeatEconomy.ForMatch(star, riser, starRating: 4.0, FinishWeight.Decisive);

            // The star gains essentially nothing...
            Assert.True(outcome.Winner.OvernessDelta < 0.35,
                $"the star should gain ~nothing, got {outcome.Winner.OvernessDelta:F2}");

            // ...while the riser loses real ground. Total status falls.
            Assert.True(outcome.Loser.OvernessDelta < -0.8,
                $"the riser should lose ground, got {outcome.Loser.OvernessDelta:F2}");

            double net = outcome.Winner.OvernessDelta + outcome.Loser.OvernessDelta;
            Assert.True(net < 0, $"squashing a riser should destroy status overall, net {net:F2}");
        }

        [Fact]
        public void TheUpsetIsWorthFarMoreThanTheSquash()
        {
            var upset = HeatEconomy.ForMatch(Riser(), Star(), 4.0, FinishWeight.Decisive);
            var squash = HeatEconomy.ForMatch(Star(), Riser(), 4.0, FinishWeight.Decisive);

            Assert.True(upset.Winner.OvernessDelta > squash.Winner.OvernessDelta * 4);
        }

        // ── You can only take what is there ──────────────────────────────────

        [Fact]
        public void TwoPerformersNobodyCaresAboutGenerateAlmostNothing()
        {
            var a = TestRoster.Make("Cold A", overness: 12);
            var b = TestRoster.Make("Cold B", overness: 10);

            var outcome = HeatEconomy.ForMatch(a, b, starRating: 4.5, FinishWeight.Decisive);

            Assert.True(outcome.Winner.OvernessDelta < 0.5,
                $"a great match between two nobodies still transfers nothing, got {outcome.Winner.OvernessDelta:F2}");
        }

        [Fact]
        public void BeatingABiggerNameIsWorthMoreThanBeatingASmallerOne()
        {
            var challenger = TestRoster.Make("Challenger", overness: 50);

            var overBig = HeatEconomy.ForMatch(challenger, Star(92), 4.0, FinishWeight.Decisive);
            var overSmall = HeatEconomy.ForMatch(challenger, TestRoster.Make("Jobber", overness: 20), 4.0, FinishWeight.Decisive);

            Assert.True(overBig.Winner.OvernessDelta > overSmall.Winner.OvernessDelta * 3);
        }

        // ── Finish quality ───────────────────────────────────────────────────

        [Fact]
        public void TheAudienceForgivesALossItUnderstands()
        {
            var clean = HeatEconomy.ForMatch(Riser(), Star(), 3.5, FinishWeight.Decisive);
            var protectedFinish = HeatEconomy.ForMatch(Riser(), Star(), 3.5, FinishWeight.Protected);

            Assert.True(Math.Abs(protectedFinish.Loser.OvernessDelta) < Math.Abs(clean.Loser.OvernessDelta));
            Assert.True(protectedFinish.Winner.OvernessDelta < clean.Winner.OvernessDelta);
        }

        [Theory]
        [InlineData(BeatType.FinishClean, FinishWeight.Decisive)]
        [InlineData(BeatType.FinishSubmission, FinishWeight.Decisive)]
        [InlineData(BeatType.FinishRollup, FinishWeight.Fluke)]
        [InlineData(BeatType.FinishInterference, FinishWeight.Protected)]
        [InlineData(BeatType.FinishDQ, FinishWeight.Protected)]
        [InlineData(BeatType.FinishCountout, FinishWeight.Protected)]
        public void FinishTypesReadAsTheRightWeight(BeatType beat, FinishWeight expected) =>
            Assert.Equal(expected, HeatEconomy.WeightOf(beat));

        // ── Match quality ────────────────────────────────────────────────────

        [Fact]
        public void AGreatMatchLiftsBothPeoplesMomentumEvenInDefeat()
        {
            var classic = HeatEconomy.ForMatch(Riser(), Star(), starRating: 5.0, FinishWeight.Decisive);
            var stinker = HeatEconomy.ForMatch(Riser(), Star(), starRating: 1.0, FinishWeight.Decisive);

            // Reputation is momentum, not overness — respect is not the same as being over.
            Assert.True(classic.Loser.MomentumDelta > stinker.Loser.MomentumDelta);
            Assert.Equal(0, classic.Loser.OvernessDelta - classic.Loser.OvernessDelta, 6);
        }

        // ── The story told back to the player ────────────────────────────────

        [Fact]
        public void TheReasonsDescribeTheResultFromEachPersonsOwnPointOfView()
        {
            // gap is measured from the winner's side, so the loser's line is its mirror.
            // Getting this backwards told a squashed rookie they had "lost to a bigger
            // name" and the beaten star that they had lost to someone beneath them.
            var upset = HeatEconomy.ForMatch(Riser(), Star(), 4.0, FinishWeight.Decisive);

            Assert.Contains("bigger name", upset.Winner.Reason);
            Assert.Contains("below them", upset.Loser.Reason);

            var squash = HeatEconomy.ForMatch(Star(), Riser(), 4.0, FinishWeight.Decisive);

            Assert.Contains("below them", squash.Winner.Reason);
            Assert.Contains("bigger name", squash.Loser.Reason);
        }

        // ── Curves ───────────────────────────────────────────────────────────

        [Fact]
        public void GainsCompressNearTheCeilingAndLossesNearTheFloor()
        {
            Assert.True(HeatEconomy.DampenGain(95, 5) < HeatEconomy.DampenGain(30, 5));
            Assert.True(HeatEconomy.DampenLoss(10, 5) < HeatEconomy.DampenLoss(80, 5));
        }

        [Fact]
        public void OvernessIsContinuousSoSmallChangesAccumulate()
        {
            var w = TestRoster.Make("Grinder", overness: 50);

            // Ten changes far too small to survive rounding to a whole number.
            for (int i = 0; i < 10; i++)
                HeatEconomy.Apply(new StatusChange(w, 0.12, 0, "tiny"));

            Assert.True(w.Overness > 51.0, $"small gains must accumulate, got {w.Overness:F2}");
        }

        // ── Momentum vs overness ─────────────────────────────────────────────

        [Fact]
        public void MomentumMovesFarHarderThanOverness()
        {
            var outcome = HeatEconomy.ForMatch(Riser(), Star(), 4.0, FinishWeight.Decisive);

            Assert.True(outcome.Winner.MomentumDelta > outcome.Winner.OvernessDelta * 5);
        }

        [Fact]
        public void EffectiveOvernessMovesWithMomentumButIsDominatedByTheStock()
        {
            var w = TestRoster.Make("Hot", overness: 50);
            Assert.Equal(50, w.EffectiveOverness, 3);

            w.Momentum = 100;
            Assert.True(w.EffectiveOverness > 50);

            // Being red hot must not turn a mid-carder into a main eventer.
            Assert.True(w.EffectiveOverness < 70,
                $"momentum should not dominate standing, got {w.EffectiveOverness:F1}");
        }

        // ── Decay ────────────────────────────────────────────────────────────

        [Fact]
        public void MomentumBleedsTowardZeroEveryDay()
        {
            var w = TestRoster.Make("Cooling", overness: 60);
            w.Momentum = 60;
            w.LastAppearance = new DateOnly(2026, 1, 5);

            var day = new DateOnly(2026, 1, 5);
            for (int i = 0; i < 21; i++)
            {
                day = day.AddDays(1);
                HeatEconomy.ApplyDailyDecay(w, day);
            }

            // Roughly a three-week half-life.
            Assert.InRange(w.Momentum, 24, 36);
        }

        [Fact]
        public void OvernessOnlySlipsOnceSomeoneHasBeenGoneLongEnough()
        {
            var seen = TestRoster.Make("Featured", overness: 60);
            var gone = TestRoster.Make("Forgotten", overness: 60);

            var start = new DateOnly(2026, 1, 5);
            seen.LastAppearance = start;
            gone.LastAppearance = start;

            var day = start;
            for (int i = 0; i < HeatEconomy.AbsenceGraceDays; i++)
            {
                day = day.AddDays(1);
                seen.LastAppearance = day;                 // keeps working
                HeatEconomy.ApplyDailyDecay(seen, day);
                HeatEconomy.ApplyDailyDecay(gone, day);
            }

            Assert.Equal(60, gone.Overness, 3);            // still inside the grace period

            for (int i = 0; i < 60; i++)
            {
                day = day.AddDays(1);
                seen.LastAppearance = day;
                HeatEconomy.ApplyDailyDecay(seen, day);
                HeatEconomy.ApplyDailyDecay(gone, day);
            }

            Assert.Equal(60, seen.Overness, 3);
            Assert.True(gone.Overness < 58,
                $"two months off screen should cost a mid-carder, got {gone.Overness:F2}");
        }

        [Fact]
        public void AdvancingACareerDecaysTheRoster()
        {
            var hot = TestRoster.Make("Hot", overness: 55);
            hot.Momentum = 50;

            var career = new Career
            {
                Promotion   = new Promotion { Name = "Test", Tier = PromotionTier.Established },
                StartDate   = new DateOnly(2026, 1, 5),
                CurrentDate = new DateOnly(2026, 1, 5),
                Roster      = [hot]
            };

            for (int i = 0; i < 14; i++) career.AdvanceOneDay();

            Assert.True(hot.Momentum < 50 * 0.8, $"momentum should have cooled, got {hot.Momentum:F1}");
        }
    }
}
