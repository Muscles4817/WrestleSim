using WrestlingSim.Engine;
using WrestlingSim.Models;
using WrestlingSim.Enums;
using WrestlingSim.Models.MatchPlan;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Phase 1 of the tag-match build (docs/tag-matches-plan.md).
    ///
    /// The engine now reasons about sides rather than two named people. Nothing here
    /// asserts that a tag match *plays* like a tag match — there are no tag beats yet, so
    /// a 2v2 plan is deliberately still a singles match with four names in it. What these
    /// cover is that the plumbing is sound: sides resolve, the compatibility shims behave,
    /// aggregates are side-averaged rather than head-counted, and the degenerate bookings
    /// the old two-field context accepted silently are now rejected loudly.
    /// </summary>
    public class MatchSideTests
    {
        private const int Seed = 20260906;

        private static List<MatchBeat> Structure(string name) =>
            MatchStructureLibrary.Find(name)!.Beats.Select(b => b.Clone()).ToList();

        // ── Shims ────────────────────────────────────────────────────────────

        [Fact]
        public void WrestlerShims_PopulateTheSides()
        {
            var a = TestRoster.Make("A");
            var b = TestRoster.Make("B");

            var plan = new MatchPlanModel { WrestlerA = a, WrestlerB = b };

            Assert.Same(a, plan.SideA.Starter);
            Assert.Same(b, plan.SideB.Starter);
            Assert.Equal(1, plan.SideA.Size);
            Assert.Equal(1, plan.SideB.Size);
            Assert.False(plan.IsTagMatch);
        }

        [Fact]
        public void SidesAndShims_ReadBackTheSame()
        {
            var a = TestRoster.Make("A");
            var b = TestRoster.Make("B");

            var viaShim  = new MatchPlanModel { WrestlerA = a, WrestlerB = b };
            var viaSides = new MatchPlanModel { SideA = MatchSide.Of(a), SideB = MatchSide.Of(b) };

            Assert.Same(viaShim.WrestlerA, viaSides.WrestlerA);
            Assert.Same(viaShim.WrestlerB, viaSides.WrestlerB);
        }

        [Fact]
        public void TagSide_ReportsItsMembersAndName()
        {
            var a1 = TestRoster.Make("Ricochet");
            var a2 = TestRoster.Make("Ospreay");
            var side = MatchSide.Of(a1, a2);

            Assert.True(side.IsTag);
            Assert.Equal(2, side.Size);
            Assert.Equal("Ricochet & Ospreay", side.Name);
            Assert.Same(a1, side.Starter);
            Assert.Equal(new[] { a2 }, side.PartnersOf(a1));
        }

        [Fact]
        public void StartingIndex_DecidesWhoTakesTheOpeningBell()
        {
            var a1 = TestRoster.Make("Starts On Apron");
            var a2 = TestRoster.Make("Starts In Ring");

            var side = new MatchSide { Members = { a1, a2 }, StartingIndex = 1 };

            Assert.Same(a2, side.Starter);
        }

        // ── Validation ───────────────────────────────────────────────────────

        [Fact]
        public void AWrestlerOnBothSides_IsRejected()
        {
            // The old two-field context answered "which side is this person on" by
            // reference equality against WrestlerA, so this booking silently resolved
            // every beat to side A. It is not a booking anybody meant to make.
            var same = TestRoster.Make("Doppelganger");
            var plan = new MatchPlanModel
            {
                WrestlerA = same,
                WrestlerB = same,
                Beats     = Structure("TV Formula")
            };

            Assert.Contains(plan.Validate(), e => e.Contains("both sides"));
        }

        [Fact]
        public void AnEmptySide_IsRejectedBeforeAnythingElseIsRead()
        {
            var plan = new MatchPlanModel { SideA = MatchSide.Of(TestRoster.Make("Alone")) };

            var errors = plan.Validate();

            Assert.Contains(errors, e => e.Contains("Side B has nobody"));
            // Reported rather than thrown: the beat checks below would have read people
            // out of the empty side.
            Assert.DoesNotContain(errors, e => e.Contains("no beats"));
        }

        [Fact]
        public void TheSameWrestlerTwiceOnOneSide_IsRejected()
        {
            var twice = TestRoster.Make("Twice");
            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(twice, twice),
                SideB = MatchSide.Of(TestRoster.Make("B1"), TestRoster.Make("B2")),
                Beats = Structure("TV Formula")
            };

            Assert.Contains(plan.Validate(), e => e.Contains("booked twice on the same side"));
        }

        [Fact]
        public void AnOutOfRangeStartingIndex_IsReportedNotClamped()
        {
            // Starter used to clamp, so Validate() reported this plan as fine and then
            // Execute threw an ArgumentOutOfRangeException indexing Members. A method
            // whose contract is "empty list = valid to execute" has to catch it.
            var plan = new MatchPlanModel
            {
                SideA = new MatchSide { Members = { TestRoster.Make("A1"), TestRoster.Make("A2") }, StartingIndex = 5 },
                SideB = MatchSide.Of(TestRoster.Make("B1")),
                Beats = Structure("TV Formula")
            };

            Assert.Contains(plan.Validate(), e => e.Contains("starts with member 5"));
        }

        [Fact]
        public void ANegativeStartingIndex_IsAlsoReported()
        {
            var plan = new MatchPlanModel
            {
                SideA = new MatchSide { Members = { TestRoster.Make("A1") }, StartingIndex = -1 },
                SideB = MatchSide.Of(TestRoster.Make("B1")),
                Beats = Structure("TV Formula")
            };

            Assert.Contains(plan.Validate(), e => e.Contains("starts with member -1"));
        }

        [Fact]
        public void EveryPlanThatValidates_AlsoExecutes()
        {
            // The property finding 1 broke: Validate() and Execute() must agree.
            var plan = new MatchPlanModel
            {
                SideA = new MatchSide { Members = { TestRoster.Make("A1"), TestRoster.Make("A2") }, StartingIndex = 1 },
                SideB = MatchSide.Of(TestRoster.Make("B1"), TestRoster.Make("B2")),
                Beats = Structure("TV Formula")
            };

            Assert.Empty(plan.Validate());
            var result = new MatchEngine(Seed).Execute(plan);

            // Side A started its second member, so that is who is still legal at the bell.
            Assert.Equal("A2", result.Pinner.RingName);
        }

        // ── Execution ────────────────────────────────────────────────────────

        [Fact]
        public void A2v2Plan_Executes_AndReportsBothSides()
        {
            var a1 = TestRoster.Make("A1");
            var a2 = TestRoster.Make("A2");
            var b1 = TestRoster.Make("B1");
            var b2 = TestRoster.Make("B2");

            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(a1, a2),
                SideB = MatchSide.Of(b1, b2),
                Beats = Structure("TV Formula")
            };

            Assert.Empty(plan.Validate());

            var result = new MatchEngine(Seed).Execute(plan);

            Assert.True(result.WasTagMatch);
            Assert.Equal(new[] { a1, a2 }, result.WinningSide);
            Assert.Equal(new[] { b1, b2 }, result.LosingSide);

            // No tag beats exist yet, so the starters are still legal at the finish.
            Assert.Same(a1, result.Pinner);
            Assert.Same(b1, result.Pinned);
            Assert.True(result.StarRating > 0);
        }

        [Fact]
        public void APartnerWhoNeverGetsIn_StillCountsTowardTheSide()
        {
            // Side aggregates are averaged per side, so a partner the crowd does not care
            // about drags the team's reading down even without taking a beat. This is the
            // behaviour that makes a side a side rather than two separate people.
            var star    = TestRoster.Make("Star",    overness: 95, charisma: 5.0, skill: 4.5);
            var deadArm = TestRoster.Make("Dead Arm", overness: 10, charisma: 0.5, skill: 1.5);
            var b1      = TestRoster.Make("B1", overness: 60, charisma: 3.0, skill: 3.5);
            var b2      = TestRoster.Make("B2", overness: 60, charisma: 3.0, skill: 3.5);

            double WithPartner(Wrestler partner)
            {
                double total = 0;
                for (int i = 0; i < 60; i++)
                {
                    total += new MatchEngine(i * 7919).Execute(new MatchPlanModel
                    {
                        SideA = MatchSide.Of(star, partner),
                        SideB = MatchSide.Of(b1, b2),
                        Beats = Structure("TV Formula")
                    }).StarRating;
                }
                return total / 60;
            }

            var strongPartner = TestRoster.Make("Strong", overness: 95, charisma: 5.0, skill: 4.5);

            Assert.True(WithPartner(strongPartner) > WithPartner(deadArm),
                "A team is only as interesting as the pair of them — a partner nobody " +
                "cares about should cost the side something.");
        }

        [Fact]
        public void SinglesResults_AreUnchangedByTheSidesRefactor()
        {
            // The whole point of phase 1: a singles match is a match between two sides of
            // one, and must grade exactly as it did before. This pins the shape — a side
            // of one is averaged to itself, so nothing about a 1v1 reading can drift.
            var a = TestRoster.Make("A", overness: 80, charisma: 4.0, skill: 4.0);
            var b = TestRoster.Make("B", overness: 70, charisma: 3.0, skill: 3.5);

            var viaShim = new MatchEngine(Seed).Execute(new MatchPlanModel
            {
                WrestlerA = a, WrestlerB = b, Beats = Structure("Technical Showcase")
            });

            var viaSides = new MatchEngine(Seed).Execute(new MatchPlanModel
            {
                SideA = MatchSide.Of(a), SideB = MatchSide.Of(b), Beats = Structure("Technical Showcase")
            });

            Assert.Equal(viaShim.StarRating, viaSides.StarRating, 10);
            Assert.Equal(viaShim.TechnicalScore, viaSides.TechnicalScore, 10);
            Assert.Equal(viaShim.CrowdPeakEnergy, viaSides.CrowdPeakEnergy, 10);
        }

        [Fact]
        public void EveryBeatBookedForOneSide_LeavesThatSideAhead()
        {
            // Honest scope note. ControlSign changed from
            // `ReferenceEquals(control, Plan.WrestlerA)` to a side lookup, and that matters
            // — but it is *preparatory*, not a bug fixed here. In phase 1 the legal
            // performer is always the side's starter and `Plan.WrestlerA` returns exactly
            // `SideA.Starter`, so the two expressions are provably equivalent and no test
            // written now can tell them apart. (Verified: reverting ControlSign leaves the
            // whole suite green.) What this pins is the weaker property that does hold
            // today — advantage tracks the side a beat is booked for. The test that can
            // actually catch a ControlSign regression arrives with phase 2, when a hot tag
            // makes a partner legal.
            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(TestRoster.Make("A1"), TestRoster.Make("A2")),
                SideB = MatchSide.Of(TestRoster.Make("B1"), TestRoster.Make("B2")),
                Beats = Structure("TV Formula")
            };

            foreach (var beat in plan.Beats)
                beat.Control = BeatControl.WrestlerA;

            var result = new MatchEngine(Seed).Execute(plan);

            Assert.True(result.BeatResults[^1].AdvantageAfter > 0,
                "Every beat was booked for side A, so side A should be ahead at the bell.");
        }

        [Fact]
        public void BookedMatch_NamesBothTeams()
        {
            var match = new WrestlingSim.Models.BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    SideA = MatchSide.Of(TestRoster.Make("Ricky"), TestRoster.Make("Robert")),
                    SideB = MatchSide.Of(TestRoster.Make("Bobby"), TestRoster.Make("Dennis")),
                    Beats = Structure("TV Formula")
                }
            };

            Assert.Equal("Ricky & Robert vs Bobby & Dennis", match.Name);
            Assert.Equal(4, match.Wrestlers.Count);
        }
    }
}
