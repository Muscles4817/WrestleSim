using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The beats a multi-man match has that a two-side match does not — doc 18 §2.5.
    ///
    /// The format's problem is that with three sides somebody is doing nothing, and its
    /// craft is disposing of them plausibly and bringing them back at the right moment. Its
    /// best story is a live feud between two of the three, because the grudge outranking the
    /// prize is the only reason a wrestler would throw a match away on purpose.
    /// </summary>
    public class MultiManBeatTests(ITestOutputHelper output)
    {
        private const int Seed = 20260906;

        private static Wrestler W(string n, double o = 80) =>
            TestRoster.Make(n, overness: o, charisma: 4.0, skill: 4.0);

        /// <summary>A three-way with an optional live feud between sides A and B.</summary>
        private static MatchPlanModel ThreeWay(IEnumerable<MatchBeat> beats,
                                               FeudIntensity grudge = FeudIntensity.None)
        {
            var (a, b, c) = (W("A", 85), W("B", 85), W("C", 85));
            var plan = new MatchPlanModel
            {
                Sides = [MatchSide.Of(a), MatchSide.Of(b), MatchSide.Of(c)],
                Beats = beats.ToList()
            };

            if (grudge > FeudIntensity.None)
            {
                var book = new FeudBook();
                var feud = book.GetOrCreate(a, b);
                feud.SetMinimumIntensity(grudge);
                plan.Feuds = book.Among([a, b, c]).ToList();
            }
            return plan;
        }

        private static MatchBeat Beat(BeatType t, BeatControl control, BeatControl? against = null) =>
            new() { Type = t, Control = control, Against = against };

        private static MatchBeat Finish() =>
            new() { Type = BeatType.FinishClean, Control = BeatControl.SideC,
                    Against = BeatControl.WrestlerB };

        // ── Disposal ─────────────────────────────────────────────────────────

        /// <summary>
        /// **The loop the format runs on.** Every cover in a three-way is breakable, so a
        /// near fall is not a question about the person being pinned — it is a question
        /// about whether somebody arrives, and the crowd knows the answer is usually yes.
        /// Inside a disposal window it is the real thing; outside one it is a spot with a
        /// count attached.
        /// </summary>
        [Fact]
        public void TheRuleItself_DiscountsANearFallOnlyWhenNobodyIsDisposed()
        {
            // Two sides: a near fall is a near fall, disposal or not.
            Assert.Equal(1.0, MatchEngine.MultiManNearFallFactor(false, false));
            Assert.Equal(1.0, MatchEngine.MultiManNearFallFactor(false, true));

            // Three: full value only while somebody cannot reach the cover.
            Assert.Equal(1.0, MatchEngine.MultiManNearFallFactor(true, true));
            Assert.Equal(MatchEngine.CrowdedOutNearFall,
                         MatchEngine.MultiManNearFallFactor(true, false));

            Assert.True(MatchEngine.CrowdedOutNearFall is > 0.2 and < 0.9,
                "A crowded-out near fall should be materially cheaper without being worthless.");
        }

        /// <summary>
        /// And the same thing end to end, with the confound controlled.
        ///
        /// Two plans with **identical beats in identical order**, differing only in how long
        /// the disposal lasts: a brief one has expired by the time the near fall lands, a
        /// long one has not. The first version of this test compared a plan containing a
        /// disposal against one containing a rest hold, which measures the difference
        /// between two beats — it passed with the whole rule deleted, and with the disposal
        /// recording nothing.
        /// </summary>
        [Fact]
        public void ANearFallCountsForMore_WhileSomebodyIsDisposedOf()
        {
            double NearFallPop(BeatDuration disposalLength)
            {
                var plan = ThreeWay(
                [
                    Beat(BeatType.HotOpening, BeatControl.Even),
                    new MatchBeat { Type = BeatType.DisposalSpot, Control = BeatControl.WrestlerA,
                                    Against = BeatControl.SideC, Duration = disposalLength },
                    Beat(BeatType.RestHold, BeatControl.WrestlerA),
                    Beat(BeatType.NearFall, BeatControl.WrestlerA),
                    Finish()
                ]);
                return new MatchEngine(Seed).Execute(plan)
                    .BeatResults.Single(b => b.BeatType == BeatType.NearFall).CrowdEnergyDelta;
            }

            double stillDown = NearFallPop(BeatDuration.Long);
            double backUp    = NearFallPop(BeatDuration.Brief);

            output.WriteLine($"  third party still down: {stillDown:F2}");
            output.WriteLine($"  third party back up:    {backUp:F2}");

            Assert.True(stillDown > backUp * 1.2,
                "A near fall while the third party is down should be worth materially more " +
                $"than the same near fall once they are up — got {stillDown:F2} against {backUp:F2}.");
        }

        /// <summary>
        /// And the discount is a multi-man rule, not a general one — a singles near fall is
        /// not cheapened by nobody having been thrown through a table.
        /// </summary>
        [Fact]
        public void ASinglesNearFallIsNotDiscounted()
        {
            var singles = new MatchEngine(Seed).Execute(new MatchPlanModel
            {
                WrestlerA = W("A", 85), WrestlerB = W("B", 85),
                Beats =
                [
                    Beat(BeatType.HotOpening, BeatControl.Even),
                    Beat(BeatType.RestHold, BeatControl.WrestlerA),
                    Beat(BeatType.NearFall, BeatControl.WrestlerA),
                    new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }
                ]
            });

            var beat = singles.BeatResults.Single(b => b.BeatType == BeatType.NearFall);
            output.WriteLine($"  singles near fall: {beat.CrowdEnergyDelta:F2}");
            Assert.True(beat.CrowdEnergyDelta > 0);
        }

        [Fact]
        public void ADisposalDoesNotLastForever()
        {
            var r = new MatchEngine(Seed).Execute(ThreeWay(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.DisposalSpot, BeatControl.WrestlerA, BeatControl.SideC),
                Beat(BeatType.RestHold, BeatControl.WrestlerA),
                Beat(BeatType.RestHold, BeatControl.WrestlerA),
                Beat(BeatType.RestHold, BeatControl.WrestlerA),
                Beat(BeatType.NearFall, BeatControl.WrestlerA),
                Finish()
            ]));

            // Four beats after a short disposal, the crowd has had time to wonder where
            // they went — the window has to have closed.
            var near = r.BeatResults.Single(b => b.BeatType == BeatType.NearFall);
            var early = new MatchEngine(Seed).Execute(ThreeWay(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.DisposalSpot, BeatControl.WrestlerA, BeatControl.SideC),
                Beat(BeatType.NearFall, BeatControl.WrestlerA),
                Finish()
            ])).BeatResults.Single(b => b.BeatType == BeatType.NearFall);

            output.WriteLine($"  straight after: {early.CrowdEnergyDelta:F2}");
            output.WriteLine($"  four beats on:  {near.CrowdEnergyDelta:F2}");
            Assert.True(near.CrowdEnergyDelta < early.CrowdEnergyDelta);
        }

        // ── The feud beats ───────────────────────────────────────────────────

        /// <summary>
        /// **A spite break is only a story if there is a grudge behind it.** Two people with
        /// a live feud wrecking each other's title shot is the best thing in a multi-man
        /// match; two strangers doing the same thing is one of them throwing away a win for
        /// no reason, and it should read as one.
        /// </summary>
        [Theory]
        [InlineData(FeudIntensity.None)]
        [InlineData(FeudIntensity.Building)]
        [InlineData(FeudIntensity.Hot)]
        [InlineData(FeudIntensity.Nuclear)]
        public void ASpiteBreakPaysOffTheGrudgeBehindIt(FeudIntensity grudge)
        {
            var r = new MatchEngine(Seed).Execute(ThreeWay(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.SpiteBreak, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Finish()
            ], grudge));

            var beat = r.BeatResults.Single(b => b.BeatType == BeatType.SpiteBreak);
            output.WriteLine($"  {grudge,-8} story {beat.StorytellingContribution:F2}  " +
                             $"resonance {beat.FeudalResonanceActivated}  " +
                             $"| {beat.Commentary.FirstOrDefault()}");

            Assert.Equal(grudge > FeudIntensity.None, beat.FeudalResonanceActivated);
        }

        [Fact]
        public void ASpiteBreakWithAHotFeud_IsWorthFarMoreThanOneBetweenStrangers()
        {
            double Story(FeudIntensity g) => new MatchEngine(Seed).Execute(ThreeWay(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.SpiteBreak, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Finish()
            ], g)).BeatResults.Single(b => b.BeatType == BeatType.SpiteBreak).StorytellingContribution;

            double none = Story(FeudIntensity.None), hot = Story(FeudIntensity.Nuclear);
            output.WriteLine($"  strangers {none:F2}  ·  nuclear {hot:F2}  ({hot / none:F1}×)");

            Assert.True(hot > none * 2.5,
                $"A grudge should be most of what this beat is worth — got {hot / none:F1}×.");
        }

        /// <summary>
        /// And it costs the one who does it. That is the point: it is not a good decision,
        /// it is a character decision, and a booking that never pays for it is not telling
        /// the story it thinks it is.
        /// </summary>
        [Fact]
        public void ASpiteBreakCostsTheSpiterPosition()
        {
            var r = new MatchEngine(Seed).Execute(ThreeWay(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.SpiteBreak, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Finish()
            ], FeudIntensity.Hot));

            var beat = r.BeatResults.Single(b => b.BeatType == BeatType.SpiteBreak);
            output.WriteLine($"  advantage delta {beat.AdvantageDelta:F2}");

            // Control is side A, so a positive delta would mean the spiter gained ground.
            Assert.True(beat.AdvantageDelta < 0,
                $"The spiter gave up position to do it; got {beat.AdvantageDelta:F2}.");
        }

        /// <summary>Nobody comes out of a mutual destruction on top — that is the beat.</summary>
        [Fact]
        public void MutualDestructionLeavesNobodyAhead()
        {
            var r = new MatchEngine(Seed).Execute(ThreeWay(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.MutualDestruction, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Finish()
            ], FeudIntensity.Hot));

            var beat = r.BeatResults.Single(b => b.BeatType == BeatType.MutualDestruction);
            output.WriteLine($"  {beat.Commentary.FirstOrDefault()}");
            Assert.Equal(0, beat.AdvantageDelta);
        }

        [Fact]
        public void AnIgnoredOpportunityAlsoCostsPosition_AndNeedsAGrudge()
        {
            var hot = new MatchEngine(Seed).Execute(ThreeWay(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.IgnoredOpportunity, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Finish()
            ], FeudIntensity.Hot)).BeatResults.Single(b => b.BeatType == BeatType.IgnoredOpportunity);

            var none = new MatchEngine(Seed).Execute(ThreeWay(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.IgnoredOpportunity, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Finish()
            ])).BeatResults.Single(b => b.BeatType == BeatType.IgnoredOpportunity);

            output.WriteLine($"  hot {hot.StorytellingContribution:F2} · none {none.StorytellingContribution:F2}");
            Assert.True(hot.AdvantageDelta < 0);
            Assert.True(hot.StorytellingContribution > none.StorytellingContribution * 1.5);
        }

        /// <summary>
        /// The beat has to aim at the side the booking named. `Against` resolving to the
        /// wrong side would put the grudge on the wrong pairing and silently pay out for a
        /// story that is not being told.
        /// </summary>
        [Fact]
        public void ABeatAimsAtTheSideTheBookingNamed()
        {
            // The feud is A-versus-B, but this spite break is aimed at C.
            var r = new MatchEngine(Seed).Execute(ThreeWay(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.SpiteBreak, BeatControl.WrestlerA, BeatControl.SideC),
                Finish()
            ], FeudIntensity.Nuclear));

            var beat = r.BeatResults.Single(b => b.BeatType == BeatType.SpiteBreak);
            output.WriteLine($"  {beat.Commentary.FirstOrDefault()}");

            Assert.Contains("C", beat.Commentary.First());
            Assert.False(beat.FeudalResonanceActivated,
                "A and C have no story; aiming the beat at C must not cash in A and B's.");
        }
    }
}
