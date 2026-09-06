using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// More than two sides — doc 18 §2.5.
    ///
    /// The distinction the doc insists on is that trios are *not* a multi-man match: nobody
    /// has to be disposed of, because everyone not legal is on the apron by rule. A triple
    /// threat is the real thing, and what makes it real is that with three sides somebody is
    /// doing nothing, and that anyone can be pinned.
    ///
    /// This is the model layer. The engine still narrates a multi-man match as though two
    /// people were in it — see the design record for what is and is not built.
    /// </summary>
    public class MultiManTests(ITestOutputHelper output)
    {
        private const int Seed = 20260906;

        private static Wrestler W(string n, double o = 80) =>
            TestRoster.Make(n, overness: o, charisma: 4.0, skill: 4.0);

        private static MatchPlanModel ThreeWay(BeatControl winner, BeatControl? pinned)
        {
            var (a, b, c) = (W("A", 90), W("B", 85), W("C", 80));
            return new MatchPlanModel
            {
                Sides = [MatchSide.Of(a), MatchSide.Of(b), MatchSide.Of(c)],
                Beats =
                [
                    new MatchBeat { Type = BeatType.HotOpening,  Control = BeatControl.Even },
                    new MatchBeat { Type = BeatType.NearFall,    Control = BeatControl.SideC },
                    new MatchBeat { Type = BeatType.FinishClean, Control = winner, Against = pinned }
                ]
            };
        }

        [Fact]
        public void ThreeSidesIsATripleThreat_AndTwoIsNot()
        {
            Assert.Equal(MatchFormat.TripleThreat, ThreeWay(BeatControl.WrestlerA, BeatControl.SideC).Format);
            Assert.True(ThreeWay(BeatControl.WrestlerA, BeatControl.SideC).IsMultiMan);

            var singles = new MatchPlanModel { WrestlerA = W("A"), WrestlerB = W("B") };
            Assert.Equal(MatchFormat.TwoSided, singles.Format);
            Assert.False(singles.IsMultiMan);

            // A trios match is six people and still two-sided — the doc is explicit that
            // this is not a multi-man match, because nobody has to be disposed of.
            var trios = new MatchPlanModel
            {
                Sides = [MatchSide.Of(W("A1"), W("A2"), W("A3")),
                         MatchSide.Of(W("B1"), W("B2"), W("B3"))]
            };
            Assert.Equal(MatchFormat.TwoSided, trios.Format);
            Assert.False(trios.IsMultiMan);
        }

        /// <summary>
        /// **The whole point of the format**: the champion can be beaten without being beaten.
        /// A finish names a winner and, separately, whoever took the fall.
        /// </summary>
        [Fact]
        public void TheSideThatTakesTheFall_IsWhoeverTheBookingSays()
        {
            var plan = ThreeWay(winner: BeatControl.WrestlerA, pinned: BeatControl.SideC);
            Assert.Empty(plan.Validate());

            var r = new MatchEngine(Seed).Execute(plan);
            output.WriteLine($"  winner {r.Winner!.RingName}, pinned {r.Loser!.RingName}");

            Assert.Equal("A", r.Winner.RingName);
            Assert.Equal("C", r.Loser.RingName);

            // And B — who neither won nor was pinned — is in the match and in neither list.
            Assert.DoesNotContain(r.WinningSide, x => x.RingName == "B");
            Assert.DoesNotContain(r.LosingSide,  x => x.RingName == "B");
            Assert.Contains(plan.AllParticipants, x => x.RingName == "B");
        }

        /// <summary>
        /// The bug this test exists for: `LegalOf` was `side == SideA ? LegalA : LegalB`, so
        /// side C resolved to side B's wrestler and a three-way with C booked to lose
        /// reported B as the loser. Booking one thing and being told another.
        /// </summary>
        [Theory]
        [InlineData(BeatControl.SideC, "C")]
        [InlineData(BeatControl.WrestlerB, "B")]
        public void EachSideCanBeTheOneAgainst(BeatControl pinned, string expected)
        {
            var r = new MatchEngine(Seed).Execute(ThreeWay(BeatControl.WrestlerA, pinned));
            Assert.Equal(expected, r.Loser!.RingName);
        }

        [Fact]
        public void AMultiManFinish_HasToSayWhoTookTheFall()
        {
            var errors = ThreeWay(BeatControl.WrestlerA, pinned: null).Validate();
            output.WriteLine("  " + string.Join(" | ", errors));
            Assert.Contains(errors, e => e.Contains("who takes the fall"));

            // And it cannot be the winner.
            Assert.Contains(ThreeWay(BeatControl.WrestlerA, BeatControl.WrestlerA).Validate(),
                            e => e.Contains("cannot also be the one pinned"));

            // Nor a side that is not in the match.
            Assert.Contains(ThreeWay(BeatControl.WrestlerA, BeatControl.SideD).Validate(),
                            e => e.Contains("not in this match"));
        }

        /// <summary>
        /// "No disqualification, no count-out, first fall wins" is not a house rule — with
        /// three people there is no way to count two of them out at once, so the format drops
        /// the rule rather than pretending to enforce it.
        /// </summary>
        [Theory]
        [InlineData(BeatType.FinishDQ)]
        [InlineData(BeatType.FinishCountout)]
        public void AThreeWayHasNoDisqualificationAndNoCountOut(BeatType finish)
        {
            var plan = ThreeWay(BeatControl.WrestlerA, BeatControl.SideC);
            plan.Beats[^1].Type = finish;

            var errors = plan.Validate();
            output.WriteLine("  " + string.Join(" | ", errors));
            Assert.Contains(errors, e => e.Contains("no disqualification or count-out"));
        }

        [Fact]
        public void SidesMustBeEvenAcrossAllOfThem_NotJustTheFirstTwo()
        {
            var plan = new MatchPlanModel
            {
                Sides = [MatchSide.Of(W("A")), MatchSide.Of(W("B")), MatchSide.Of(W("C1"), W("C2"))],
                Beats = [new MatchBeat { Type = BeatType.FinishClean,
                                         Control = BeatControl.WrestlerA, Against = BeatControl.SideC }]
            };

            // Sides A and B match, so a check that only compared those two would pass this.
            var errors = plan.Validate();
            output.WriteLine("  " + string.Join(" | ", errors));
            Assert.Contains(errors, e => e.Contains("uneven"));
        }

        [Fact]
        public void ControlValuesAndSideIndices_RoundTrip()
        {
            for (int i = 0; i < 4; i++)
                Assert.Equal(i, MatchPlanModel.SideIndex(MatchPlanModel.ControlFor(i)));

            Assert.Null(MatchPlanModel.SideIndex(BeatControl.Even));
            Assert.Null(MatchPlanModel.SideIndex(BeatControl.Contested));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchPlanModel.ControlFor(4));
        }
    }
}
