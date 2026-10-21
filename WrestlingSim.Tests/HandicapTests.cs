using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **Two or more against one.**
    ///
    /// Doc 18 §2.5, in full: "Handicap. Two or more against one. Almost never a contest; it
    /// is a *statement*, and the statement is usually about the lone man's toughness rather
    /// than the outcome."
    ///
    /// Two sentences, and the second is the awkward one — a format whose point is explicitly
    /// not the result cannot be graded by an engine that grades results. So there are two
    /// mechanisms: what the numbers cost the man carrying them, and what he earns for
    /// carrying them. Both are pure functions and one is recorded per beat, so both can be
    /// asserted without reading a match's tea leaves.
    /// </summary>
    public class HandicapTests(ITestOutputHelper output)
    {
        private const int Seed = 20260906;

        private static Wrestler W(string n) =>
            TestRoster.Make(n, overness: 70, charisma: 3.0, skill: 3.5);

        private static MatchBeat Beat(BeatType t, BeatControl c) =>
            new() { Type = t, Control = c };

        /// <summary>One against two: Alone versus Pair One and Pair Two.</summary>
        private static MatchPlanModel Handicap(params MatchBeat[] beats) => new()
        {
            SideA = MatchSide.Of(W("Alone")),
            SideB = MatchSide.Of(W("PairOne"), W("PairTwo")),
            Beats = beats.ToList()
        };

        // ── The numbers ──────────────────────────────────────────────────────

        /// <summary>
        /// **Even sides carry nothing.** The term has to be exactly inert in every match that
        /// existed before it, or adding it re-grades the whole game — and `TypicalInvestment`
        /// is a measured median of this roster, so moving every match silently moves that too.
        /// </summary>
        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        [InlineData(2, 1)]   // outnumbering somebody costs *you* nothing
        [InlineData(3, 2)]
        public void EvenOrLarger_CarriesNoNumbersFatigue(int mine, int theirs) =>
            Assert.Equal(1.0, MatchEngine.NumbersFatigue(mine, theirs, 10, 0.5));

        /// <summary>
        /// **It compounds, and it scales with how outnumbered you are.**
        ///
        /// Against twice your number you do twice the work, so you carry the extra once over;
        /// against three times, twice over. And it grows with the match, because the whole
        /// mechanism is being denied the rest — which is why a short handicap match is a
        /// beating and a long one is a slaughter.
        /// </summary>
        [Fact]
        public void BeingOutnumbered_CompoundsAndScalesWithTheOdds()
        {
            double oneOnTwoEarly = MatchEngine.NumbersFatigue(1, 2, 2,  0.5);
            double oneOnTwoLate  = MatchEngine.NumbersFatigue(1, 2, 10, 0.5);
            double oneOnThree    = MatchEngine.NumbersFatigue(1, 3, 10, 0.5);

            output.WriteLine($"  1v2 at beat 6  : {oneOnTwoEarly:F3}");
            output.WriteLine($"  1v2 at beat 14 : {oneOnTwoLate:F3}");
            output.WriteLine($"  1v3 at beat 14 : {oneOnThree:F3}");

            Assert.True(oneOnTwoEarly < 1.0,  "being outnumbered costs nothing at all");
            Assert.True(oneOnTwoLate < oneOnTwoEarly, "the cost does not grow with the match");
            Assert.True(oneOnThree  < oneOnTwoLate,  "three-on-one costs no more than two-on-one");

            // Two-on-one carries the wear once over, so it is the square of an even side's
            // fade at the same point — not an arbitrary constant.
            Assert.Equal(Math.Pow(MatchEngine.NumbersFatigue(1, 2, 5, 0.5), 2),
                         MatchEngine.NumbersFatigue(1, 2, 10, 0.5), 6);
        }

        /// <summary>A better-conditioned wrestler carries the numbers longer.</summary>
        [Fact]
        public void ConditioningIsWhatYouCarryItWith()
        {
            double unfit = MatchEngine.NumbersFatigue(1, 2, 10, 0.1);
            double fit   = MatchEngine.NumbersFatigue(1, 2, 10, 0.9);

            output.WriteLine($"  poorly conditioned: {unfit:F3}   well conditioned: {fit:F3}");
            Assert.True(fit > unfit + 0.05,
                "conditioning makes no difference to how long you last outnumbered");
        }

        /// <summary>
        /// **And in a real match it lands on the lone wrestler only.**
        ///
        /// That asymmetry is what makes it a numbers *advantage* rather than a slower match:
        /// his offence weakens as the beating goes on and theirs does not, because they have
        /// been taking turns. Asserted on the recorded factor, so what is being checked is
        /// the rule rather than a score twenty other things also move.
        /// </summary>
        [Fact]
        public void OnlyTheOutnumberedSideCarriesIt()
        {
            var r = new MatchEngine(Seed).Execute(Handicap(
                Beat(BeatType.StandardOpening, BeatControl.Even),
                Beat(BeatType.HeatSegment,  BeatControl.WrestlerB),
                Beat(BeatType.HeatSegment,  BeatControl.WrestlerB),
                Beat(BeatType.HeatSegment,  BeatControl.WrestlerB),
                Beat(BeatType.Comeback,     BeatControl.WrestlerA),
                Beat(BeatType.HighSpot,     BeatControl.WrestlerA),
                Beat(BeatType.NearFall,     BeatControl.WrestlerA),
                Beat(BeatType.FinishClean,  BeatControl.WrestlerB)));

            foreach (var b in r.BeatResults)
                output.WriteLine($"  {b.BeatType,-16} worked by {b.Worker?.RingName,-8} " +
                                 $"numbers {b.NumbersFatigue:F3}");

            var loneMans = r.BeatResults.Where(b => b.Worker?.RingName == "Alone").ToList();
            var pairs    = r.BeatResults.Where(b => b.Worker?.RingName != "Alone").ToList();

            Assert.All(pairs, b => Assert.Equal(1.0, b.NumbersFatigue));
            Assert.Contains(loneMans, b => b.NumbersFatigue < 1.0);
        }

        /// <summary>
        /// And a tag match of the same size is untouched — the term is inert wherever the
        /// sides are even, in a real match and not only in the unit test above.
        /// </summary>
        [Fact]
        public void AnEvenTagMatch_CarriesNoneOfIt()
        {
            var r = new MatchEngine(Seed).Execute(new MatchPlanModel
            {
                SideA = MatchSide.Of(W("A1"), W("A2")),
                SideB = MatchSide.Of(W("B1"), W("B2")),
                Beats =
                [
                    Beat(BeatType.StandardOpening, BeatControl.Even),
                    Beat(BeatType.Shine,       BeatControl.WrestlerA),
                    Beat(BeatType.Cutoff,      BeatControl.WrestlerB),
                    Beat(BeatType.Isolation,   BeatControl.WrestlerB),
                    Beat(BeatType.HotTag,      BeatControl.WrestlerA),
                    Beat(BeatType.FinishClean, BeatControl.WrestlerA)
                ]
            });

            Assert.All(r.BeatResults, b => Assert.Equal(1.0, b.NumbersFatigue));
            Assert.Equal(0.0, r.Defiance);
            Assert.Equal(0.0, r.Breakdown.DefianceNudge);
        }

        // ── The statement ────────────────────────────────────────────────────

        /// <summary>
        /// **Defiance is resistance over time in the match, and a squash is nought.**
        /// </summary>
        [Theory]
        [InlineData(0, 8, 0.0)]     // never had a moment
        [InlineData(4, 8, 0.5)]
        [InlineData(8, 8, 1.0)]
        [InlineData(0, 0, 0.0)]     // no match at all
        public void DefianceCountsResistance(int resisted, int total, double expected) =>
            Assert.Equal(expected, MatchEngine.Defiance(resisted, total), 6);

        /// <summary>
        /// **A valiant loss beats a squash**, which is the whole claim of the format and the
        /// one an engine that grades results would get backwards.
        ///
        /// Both plans are the same length, both are lost by the same lone wrestler, and both
        /// are worked by the same people. The only difference is whether he ever got a
        /// moment. If the squash does not score worse, doc 18's "the statement is about the
        /// lone man's toughness rather than the outcome" is a comment rather than a rule.
        /// </summary>
        [Fact]
        public void AValiantLossScoresBetterThanASquash()
        {
            MatchBeat[] Squash() =>
            [
                Beat(BeatType.StandardOpening, BeatControl.Even),
                Beat(BeatType.HeatSegment,   BeatControl.WrestlerB),
                Beat(BeatType.HeatSegment,   BeatControl.WrestlerB),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerB),
                Beat(BeatType.HeatSegment,   BeatControl.WrestlerB),
                Beat(BeatType.FinishClean,   BeatControl.WrestlerB)
            ];

            MatchBeat[] Valiant() =>
            [
                Beat(BeatType.StandardOpening, BeatControl.Even),
                Beat(BeatType.HeatSegment,   BeatControl.WrestlerB),
                Beat(BeatType.Comeback,      BeatControl.WrestlerA),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerB),
                Beat(BeatType.Comeback,      BeatControl.WrestlerA),
                Beat(BeatType.FinishClean,   BeatControl.WrestlerB)
            ];

            int valiantWins = 0;
            double gap = 0;
            for (int seed = 0; seed < 25; seed++)
            {
                var squash  = new MatchEngine(seed).Execute(Handicap(Squash()));
                var valiant = new MatchEngine(seed).Execute(Handicap(Valiant()));

                if (seed == 0)
                    output.WriteLine($"  squash defiance {squash.Defiance:F2} " +
                                     $"({squash.Breakdown.DefianceNudge:+0.0;-0.0}) vs " +
                                     $"valiant {valiant.Defiance:F2} " +
                                     $"({valiant.Breakdown.DefianceNudge:+0.0;-0.0})");

                // Both lose. That is the point.
                Assert.Equal("PairOne", squash.Winner.RingName);
                Assert.Equal("PairOne", valiant.Winner.RingName);

                gap += valiant.FinalScore - squash.FinalScore;
                if (valiant.FinalScore > squash.FinalScore) valiantWins++;
            }

            output.WriteLine($"  the valiant loss scored higher in {valiantWins}/25 seeds, " +
                             $"mean gap {gap / 25:F2} points");

            Assert.True(valiantWins >= 23,
                $"the valiant loss only beat the squash in {valiantWins} of 25 seeds");
        }

        // ── Bookable and legible ─────────────────────────────────────────────

        /// <summary>
        /// **A handicap match validates, runs, and reports what it was.** The rule that used
        /// to refuse this wrote down its own exit condition — a numbers term in the engine —
        /// and this is the assertion that the condition was met rather than waived.
        /// </summary>
        [Theory]
        [InlineData(1, 2)]
        [InlineData(1, 3)]
        [InlineData(2, 3)]
        public void AHandicapMatchIsBookable(int small, int large)
        {
            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(Enumerable.Range(0, small).Select(i => W($"A{i}")).ToArray()),
                SideB = MatchSide.Of(Enumerable.Range(0, large).Select(i => W($"B{i}")).ToArray()),
                Beats =
                [
                    Beat(BeatType.StandardOpening, BeatControl.Even),
                    Beat(BeatType.HeatSegment, BeatControl.WrestlerB),
                    Beat(BeatType.Comeback,    BeatControl.WrestlerA),
                    Beat(BeatType.FinishClean, BeatControl.WrestlerB)
                ]
            };

            Assert.Empty(plan.Validate());
            Assert.True(plan.IsHandicap);

            var r = new MatchEngine(Seed).Execute(plan);
            output.WriteLine($"  {small}v{large}: {r.StarRating:F2} stars, " +
                             $"defiance {r.Defiance:F2}, winner {r.Winner.RingName}");

            Assert.Equal(small, plan.Numbers!.Value.Outnumbered.Size);
            Assert.InRange(r.Defiance, 0.0, 1.0);
        }

        /// <summary>
        /// A handicap plan survives a save and reload. The rule that refused uneven sides
        /// claimed they could not be saved either; that stopped being true when the format
        /// moved to storing `Sides` as id lists, and this is the check rather than the claim.
        /// </summary>
        [Fact]
        public void AHandicapMatchRoundTripsThroughASave()
        {
            var roster = new[] { W("Alone"), W("PairOne"), W("PairTwo") };
            var plan = new MatchPlanModel
            {
                Sides = [MatchSide.Of(roster[0]), MatchSide.Of(roster[1], roster[2])],
                Beats =
                [
                    Beat(BeatType.StandardOpening, BeatControl.Even),
                    Beat(BeatType.FinishClean, BeatControl.WrestlerB)
                ]
            };

            Assert.Empty(plan.Validate());
            Assert.True(plan.IsHandicap);
            Assert.Equal(3, plan.AllParticipants.Count());
        }
    }
}
