using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **The stage the singles structures did not have.**
    ///
    /// Doc 18 §2.3 draws the face-in-peril structure as seven stages —
    /// SHINE → CUT-OFF → HEAT → **HOPE SPOTS** → COMEBACK → **FINISHING STRETCH** → FINISH
    /// — says "the hope spots are essential — they keep the audience from giving up during
    /// the heat", and then says of the whole thing: "this structure … is the reason 'face in
    /// peril' is in this repo's `MatchStructureLibrary`."
    ///
    /// The structure named after it implemented four of the seven. It had no shine, no
    /// cut-off, no hope spots — there was no `HopeSpot` beat type at all — and no finishing
    /// stretch: the comeback ran straight into the pin with zero near falls. The tag
    /// formula had the full treatment (Southern Tag, thirteen beats, two peril/near-tag
    /// cycles); the singles formula doc 18 calls *the dominant structure in wrestling* had
    /// five beats.
    ///
    /// So the tests below are in two halves: the beat itself, and a shape test over the
    /// library that would have caught the gap in the first place.
    /// </summary>
    public class HopeSpotTests(ITestOutputHelper output)
    {
        private static readonly int Seed = StableSeed.From("hope-spots");

        private static Wrestler W(string n, int over = 78) =>
            TestRoster.Make(n, overness: over, charisma: 3.5, skill: 3.5);

        private static MatchBeat B(string template, BeatControl control) =>
            BeatLibrary.Find(template)!.ToMatchBeat(control);

        /// <summary>A heat section with <paramref name="hopeSpots"/> flurries in it.</summary>
        private static MatchPlan Peril(Wrestler face, Wrestler heel, int hopeSpots)
        {
            var beats = new List<MatchBeat>
            {
                B("Standard Collar-and-Elbow", BeatControl.Even),
                B("Shine",                     BeatControl.WrestlerA),
                B("Cut-Off",                   BeatControl.WrestlerB),
                B("Power Beatdown",            BeatControl.WrestlerB)
            };

            for (int i = 0; i < hopeSpots; i++)
            {
                beats.Add(B("Hope Spot",      BeatControl.WrestlerA));
                beats.Add(B("Wear-Down Hold", BeatControl.WrestlerB));
            }

            beats.Add(B("Hot Comeback",   BeatControl.WrestlerA));
            beats.Add(B("Shock Kickout",  BeatControl.WrestlerA));
            beats.Add(B("Clean Victory",  BeatControl.WrestlerA));

            return new MatchPlan
            {
                Sides = [new MatchSide { Members = [face] }, new MatchSide { Members = [heel] }],
                Beats = beats
            };
        }

        // ── The beat ─────────────────────────────────────────────────────────

        /// <summary>
        /// **A hope spot takes the room up; a denied tag takes it down.**
        ///
        /// The two beats do the same structural job — wind the tension tighter without
        /// releasing it — and they are opposite in the one place it can be measured. A hope
        /// spot is offence that lands before it is cut off, so the crowd pops. A near tag is
        /// a reach that fails, so the crowd groans. Both read as Tension, because in neither
        /// case has anything been let go.
        ///
        /// If this pair ever agreed in sign, one of the two beats would be redundant.
        /// </summary>
        [Fact]
        public void AHopeSpotLiftsTheRoomAndADeniedTagDrainsIt()
        {
            var face = W("Face"); var heel = W("Heel");

            var singles = new MatchPlan
            {
                Sides = [new MatchSide { Members = [face] }, new MatchSide { Members = [heel] }],
                Beats =
                [
                    B("Standard Collar-and-Elbow", BeatControl.Even),
                    B("Power Beatdown",            BeatControl.WrestlerB),
                    B("Hope Spot",                 BeatControl.WrestlerA),
                    B("Hot Comeback",              BeatControl.WrestlerA),
                    B("Clean Victory",             BeatControl.WrestlerA)
                ]
            };

            var tag = new MatchPlan
            {
                Sides =
                [
                    new MatchSide { Members = [face, W("Partner")] },
                    new MatchSide { Members = [heel, W("Other")] }
                ],
                Beats =
                [
                    B("Standard Collar-and-Elbow", BeatControl.Even),
                    B("Cut-Off",                   BeatControl.WrestlerB),
                    B("Face in Peril",             BeatControl.WrestlerB),
                    B("Near Tag",                  BeatControl.WrestlerB),
                    B("Hot Tag",                   BeatControl.WrestlerA),
                    B("Clean Victory",             BeatControl.WrestlerA)
                ]
            };

            var hope = new MatchEngine(Seed).Execute(singles)
                .BeatResults.Single(r => r.BeatType == BeatType.HopeSpot);
            var denied = new MatchEngine(Seed).Execute(tag)
                .BeatResults.Single(r => r.BeatType == BeatType.NearTag);

            output.WriteLine($"  hope spot  {hope.CrowdEnergyDelta:+0.0;-0.0}  {hope.Reaction}");
            output.WriteLine($"  near tag   {denied.CrowdEnergyDelta:+0.0;-0.0}  {denied.Reaction}");

            Assert.True(hope.CrowdEnergyDelta > 0, "offence that lands should lift the room");
            Assert.True(denied.CrowdEnergyDelta < 0, "a reach that fails should drain it");
            Assert.Equal(ReactionKind.Tension, hope.Reaction);
            Assert.Equal(ReactionKind.Tension, denied.Reaction);
        }

        /// <summary>
        /// **Hope spots charge the comeback, and stop paying at two.**
        ///
        /// The pure function, asserted as itself. Same shape and same argument as
        /// <c>HotTagCharge</c>: a third flurry that goes nowhere is the crowd learning the
        /// flurries go nowhere, which is the opposite of what the beat is for.
        /// </summary>
        [Fact]
        public void HopeSpotsChargeTheComeback_AndStopPayingAtTwo()
        {
            double none  = MatchEngine.ComebackCharge(0);
            double one   = MatchEngine.ComebackCharge(1);
            double two   = MatchEngine.ComebackCharge(2);
            double five  = MatchEngine.ComebackCharge(5);

            output.WriteLine($"  0 {none:F2}   1 {one:F2}   2 {two:F2}   5 {five:F2}");

            Assert.Equal(1.0, none, 3);
            Assert.True(one > none && two > one);
            Assert.Equal(two, five, 3);
        }

        /// <summary>
        /// **A comeback nobody was made to wait for lands smaller.**
        ///
        /// End to end, and asserted on the comeback beat rather than the match rating so
        /// that nothing downstream can be doing the work. Doc 18's claim is that the hope
        /// spots are what keep the crowd there for the payoff, and this is that claim as a
        /// number.
        /// </summary>
        [Fact]
        public void AComebackWithHopeSpotsBehindItPopsHarder()
        {
            var face = W("Face"); var heel = W("Heel");

            double Pop(int hopeSpots) =>
                new MatchEngine(Seed).Execute(Peril(face, heel, hopeSpots))
                    .BeatResults.Single(r => r.BeatType == BeatType.Comeback).CrowdEnergyDelta;

            double cold = Pop(0);
            double built = Pop(2);

            output.WriteLine($"  comeback with no hope spots {cold:F1}");
            output.WriteLine($"  comeback with two           {built:F1}");

            Assert.True(built > cold * 1.15,
                $"two hope spots should be worth a real pop: {built:F1} vs {cold:F1}");
        }

        /// <summary>
        /// **And a fourth flattens the hole it was meant to be digging.**
        ///
        /// The cost, and the reason hope spots are not a free bonus. A flurry claws back
        /// deficit, and the comeback's earned bonus is read off exactly that deficit — so
        /// past the point where the charge saturates, every further hope spot is spending
        /// the heat it is supposed to be relieving. A wrestler who keeps getting flurries
        /// was never really in peril.
        ///
        /// Nothing was added to the engine to make this true. It falls out of the two
        /// mechanisms that were already there, which is the reason to prefer it to a rule.
        /// </summary>
        [Fact]
        public void PastTheSaturationPointEveryHopeSpotSpendsTheHeat()
        {
            var face = W("Face"); var heel = W("Heel");

            double Deficit(int hopeSpots)
            {
                var r = new MatchEngine(Seed).Execute(Peril(face, heel, hopeSpots));
                // The hole the comeback is paid out of, read at the beat before it.
                int i = r.BeatResults.ToList().FindIndex(x => x.BeatType == BeatType.Comeback);
                return Math.Abs(r.BeatResults[i - 1].AdvantageAfter);
            }

            double two  = Deficit(2);
            double five = Deficit(5);

            output.WriteLine($"  deficit going into the comeback — two hope spots {two:F1}, five {five:F1}");
            Assert.True(five < two,
                $"more flurries should mean a shallower hole, got {five:F1} against {two:F1}");
        }

        /// <summary>
        /// The charge is spent when the comeback lands, so the second heat section of a
        /// two-cycle match has to buy its own payoff rather than banking the first one's.
        /// </summary>
        [Fact]
        public void TheSecondComebackCannotBankTheFirstsHopeSpots()
        {
            var face = W("Face"); var heel = W("Heel");

            var plan = new MatchPlan
            {
                Sides = [new MatchSide { Members = [face] }, new MatchSide { Members = [heel] }],
                Beats =
                [
                    B("Standard Collar-and-Elbow", BeatControl.Even),
                    B("Cut-Off",                   BeatControl.WrestlerB),
                    B("Power Beatdown",            BeatControl.WrestlerB),
                    B("Hope Spot",                 BeatControl.WrestlerA),
                    B("Hope Spot",                 BeatControl.WrestlerA),
                    B("Hot Comeback",              BeatControl.WrestlerA),   // spends them
                    B("Cut-Off",                   BeatControl.WrestlerB),
                    B("Power Beatdown",            BeatControl.WrestlerB),
                    B("Hot Comeback",              BeatControl.WrestlerA),   // pays for itself
                    B("Clean Victory",             BeatControl.WrestlerA)
                ]
            };

            var comebacks = new MatchEngine(Seed).Execute(plan)
                .BeatResults.Where(r => r.BeatType == BeatType.Comeback).ToList();

            output.WriteLine($"  first comeback  {comebacks[0].CrowdEnergyDelta:F1}");
            output.WriteLine($"  second comeback {comebacks[1].CrowdEnergyDelta:F1}");

            Assert.Equal(2, comebacks.Count);
            Assert.Contains(comebacks[1].Commentary,
                c => c.Contains("never given a reason to believe"));
        }

        // ── The shape of the library ─────────────────────────────────────────

        /// <summary>
        /// **Every structure that claims the face-in-peril name has to have its stages.**
        ///
        /// The test that would have caught this. Doc 18 §2.3 names seven stages and calls
        /// the hope spots essential; the library's own `Face-in-Peril` had four of them and
        /// nothing said so, because nothing in the suite ever compared a structure against
        /// the document it was named after.
        ///
        /// Deliberately not applied to every structure. A spotfest is "sequential high
        /// spots, weakest psychology" and a brawl is "little structure, escalating
        /// violence" (doc 18 §2.4) — forcing the peril shape onto those would be making
        /// them all the same match, which is the opposite of the point.
        /// </summary>
        [Theory]
        [InlineData("Face-in-Peril")]
        [InlineData("Technical Showcase")]
        [InlineData("Big Match")]
        [InlineData("Epic")]
        public void TheFaceInPerilStructuresHaveAllOfDoc18sStages(string name)
        {
            var beats = MatchStructureLibrary.Find(name)!.Beats;
            var types = beats.Select(b => b.Type).ToList();

            bool Has(params BeatType[] any) => types.Any(any.Contains);

            output.WriteLine($"  {name}: {string.Join(" → ", types)}");

            Assert.True(Has(BeatType.HotOpening, BeatType.SlowOpening, BeatType.StandardOpening),
                $"{name} has no opening");
            Assert.True(Has(BeatType.Shine), $"{name} has no shine");
            Assert.True(Has(BeatType.Cutoff), $"{name} has no cut-off");
            Assert.True(Has(BeatType.HeatSegment, BeatType.RestHold), $"{name} has no heat");
            Assert.True(Has(BeatType.HopeSpot), $"{name} has no hope spot — doc 18 §2.3 calls them essential");
            Assert.True(Has(BeatType.Comeback), $"{name} has no comeback");
            Assert.True(types.Count(t => t == BeatType.NearFall) >= 1,
                $"{name} has no finishing stretch — the comeback runs straight into the pin");
            Assert.True(beats.Last().IsFinish, $"{name} does not end with a finish");
        }

        /// <summary>
        /// **And the library covers doc 18 §3.1's length bands.**
        ///
        /// Before this there was nothing above sixteen minutes in singles: the longest
        /// template was called "Big Match Epic" and sat squarely in the *big match* band,
        /// so a booker laying out the main event of the biggest show of the year had no
        /// template for it. Doc 18 §2.4 defines an epic as "25–40 minutes, multiple false
        /// finishes"; §3.1 gives the bands.
        /// </summary>
        [Fact]
        public void TheLibraryReachesFromTelevisionLengthToEpic()
        {
            int Minutes(string n) => MatchStructureLibrary.Find(n)!.Beats.Sum(b => b.DurationMinutes);

            foreach (var n in new[] { "TV Formula", "Face-in-Peril", "Big Match", "Epic" })
                output.WriteLine($"  {n,-18} {Minutes(n),3} min, " +
                                 $"{MatchStructureLibrary.Find(n)!.Beats.Count} beats");

            Assert.InRange(Minutes("TV Formula"), 5, 10);       // doc: 5–8, TV standard
            Assert.InRange(Minutes("Face-in-Peril"), 10, 16);   // doc: 10–15, the workhorse
            Assert.InRange(Minutes("Big Match"), 16, 25);       // doc: 15–25, two cycles
            Assert.InRange(Minutes("Epic"), 25, 40);            // doc: 25–40, false finishes

            // An epic is defined by its false finishes as much as its length.
            Assert.True(MatchStructureLibrary.Find("Epic")!.Beats
                .Count(b => b.Type == BeatType.NearFall) >= 3);
        }
    }
}
