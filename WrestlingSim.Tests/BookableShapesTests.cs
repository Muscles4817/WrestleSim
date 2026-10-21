using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **Every shape the builder offers has to produce a plan that validates.**
    ///
    /// The rule this exists to enforce: if the engine can do it, the booker can book it. Three
    /// merged PRs added sides, beats, blame and a status discount — all green, none reachable
    /// from the match builder — which is a feature only a unit test could invoke.
    ///
    /// A preset that does not validate is worse than a missing one: the player picks it, fills
    /// the lineup, presses the button and gets an error with no way back to a good plan.
    /// </summary>
    public class BookableShapesTests(ITestOutputHelper output)
    {
        private static Wrestler W(string n) => TestRoster.Make(n, overness: 70);

        /// <summary>
        /// The shapes the builder's step 0 offers, as (sides, size of A, size of B).
        ///
        /// Three numbers rather than two, because handicap is the shape whose sides differ
        /// — and this data being one number short is precisely how the builder came to be
        /// unable to express a match the engine could run.
        /// </summary>
        public static TheoryData<int, int, int> Shapes => new()
        {
            { 2, 1, 1 }, { 2, 2, 2 }, { 2, 3, 3 }, { 3, 1, 1 }, { 4, 1, 1 },
            { 2, 1, 2 }, { 2, 1, 3 }, { 2, 4, 4 }
        };

        [Theory]
        [MemberData(nameof(Shapes))]
        public void EveryShapeTheBuilderOffers_HasAPresetThatValidates(int sideCount, int sideSize, int sizeB)
        {
            var structures = MatchStructureLibrary.ForShape(sideCount, sideSize, sizeB).ToList();

            output.WriteLine($"  {sideCount} sides, {sideSize} v {sizeB}: " +
                             (structures.Count == 0 ? "NO PRESETS"
                                                    : string.Join(", ", structures.Select(s => s.Name))));

            Assert.NotEmpty(structures);

            foreach (var structure in structures)
            {
                var plan = Build(sideCount, sideSize, structure, sizeB);
                var errors = plan.Validate();
                if (errors.Count > 0)
                    output.WriteLine($"    {structure.Name}: {string.Join(" | ", errors)}");

                Assert.Empty(errors);
            }
        }

        /// <summary>And each one runs, producing the winner and the pinned side it booked.</summary>
        [Theory]
        [MemberData(nameof(Shapes))]
        public void EveryPreset_RunsAndProducesTheBookedResult(int sideCount, int sideSize, int sizeB)
        {
            foreach (var structure in MatchStructureLibrary.ForShape(sideCount, sideSize, sizeB))
            {
                var plan = Build(sideCount, sideSize, structure, sizeB);
                var r = new MatchEngine(20260906).Execute(plan);

                output.WriteLine($"  {structure.Name,-22} {r.Winner!.RingName} beat " +
                                 $"{r.Loser!.RingName}, {r.StarRating:F2} stars");

                // Membership, not the starter: in a tag match the winner is whoever was
                // *legal* when the finish landed, which depends on the tags booked along the
                // way. Asserting the starter passed for every singles and multi-man preset
                // and failed for tag and trios, which is the engine being right.
                Assert.Contains(r.Winner, plan.BookedWinningSide!.Members);
                Assert.Contains(r.Loser,  plan.BookedLosingSide!.Members);
            }
        }

        /// <summary>
        /// A multi-man preset's finish must name who takes the fall. Without it the plan does
        /// not validate, so a preset that forgot would be unbookable — and the player would
        /// find out only after filling the lineup.
        /// </summary>
        [Fact]
        public void EveryMultiManPreset_NamesWhoTakesTheFall()
        {
            var multiMan = MatchStructureLibrary.All.Where(s => s.SideCount > 2).ToList();
            Assert.NotEmpty(multiMan);

            foreach (var s in multiMan)
            {
                var finish = s.Beats.Last(b => b.IsFinish);
                output.WriteLine($"  {s.Name,-22} winner {finish.Control}, pinned {finish.Against}");

                Assert.NotNull(finish.Against);
                Assert.NotEqual(finish.Control, finish.Against!.Value);
            }
        }

        /// <summary>
        /// And no multi-man preset books a disqualification or a count-out, which the format
        /// does not have — a preset that did would be rejected on the button press.
        /// </summary>
        [Fact]
        public void NoMultiManPreset_BooksAFinishTheFormatDoesNotHave()
        {
            foreach (var s in MatchStructureLibrary.All.Where(x => x.SideCount > 2))
            foreach (var beat in s.Beats)
                Assert.False(beat.Type is BeatType.FinishDQ or BeatType.FinishCountout,
                             $"{s.Name} books a {beat.Type} in a match with no disqualification.");
        }

        private static MatchPlanModel Build(int sideCount, int sideSize, MatchStructure structure,
                                            int sizeB = 0)
        {
            int SizeOf(int side) => side == 1 && sizeB > 0 ? sizeB : sideSize;

            var plan = new MatchPlanModel
            {
                Sides = Enumerable.Range(0, sideCount)
                    .Select(side => MatchSide.Of(Enumerable.Range(0, SizeOf(side))
                        .Select(i => W($"{(char)('A' + side)}{i + 1}")).ToArray()))
                    .ToList(),
                Beats = structure.Beats.Select(b => b.Clone()).ToList()
            };

            if (structure.RequiresFeud)
            {
                var book = new FeudBook();
                var feud = book.GetOrCreate(plan.Sides[0].Members[0], plan.Sides[1].Members[0]);
                feud.SetMinimumIntensity(FeudIntensity.Hot);
                plan.Feuds = book.Among(plan.AllParticipants.ToList()).ToList();
            }
            return plan;
        }
        /// <summary>
        /// A match is billed with everybody in it. A three-way announced as "A vs B" leaves
        /// out one of the three — on the card, on the result screen and in the show report —
        /// which is the shape of "the player can book it but cannot see it".
        /// </summary>
        [Theory]
        [MemberData(nameof(Shapes))]
        public void AMatchIsBilledWithEverybodyInIt(int sideCount, int sideSize, int sizeB)
        {
            var structure = MatchStructureLibrary.ForShape(sideCount, sideSize, sizeB).First();
            var booked = new BookedMatch { Plan = Build(sideCount, sideSize, structure, sizeB) };

            output.WriteLine($"  {sideCount}×{sideSize}: {booked.Name}");

            foreach (var w in booked.Plan.AllParticipants)
                Assert.Contains(w.RingName, booked.Name);

            Assert.Equal(sideCount - 1, booked.Name.Split(" vs ").Length - 1);
        }

        // ── Aiming a beat ────────────────────────────────────────────────────
        //
        // The builder used to offer "who it is against" only on the finish, so every other
        // beat a player booked was undirected and `MatchBeat.Against` — the field the whole
        // three-way commentary reads — was unreachable from the UI for all but the last
        // beat. The engine could aim a beat and the booker could not, which is the rule in
        // CLAUDE.md failing in the small.

        /// <summary>
        /// **A beat aimed at any side in the match is a legal booking, on every beat.**
        ///
        /// Booked the way the builder now writes it: an opening, a beat aimed at each side
        /// in turn, and a finish. If `Validate` rejected any of these the chip row would be
        /// offering plans the confirm button refuses.
        /// </summary>
        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        public void EveryNonFinishBeat_CanBeAimedAtAnySideButItsOwn(int sideCount)
        {
            var people = Enumerable.Range(0, sideCount)
                                   .Select(i => W($"W{i}")).ToList();

            for (int worker = 0; worker < sideCount; worker++)
            for (int target = 0; target < sideCount; target++)
            {
                if (worker == target) continue;

                var plan = new MatchPlanModel
                {
                    Sides = people.Select(w => MatchSide.Of(w)).ToList(),
                    Beats =
                    [
                        new() { Type = BeatType.HotOpening, Control = BeatControl.Even },
                        new() { Type    = BeatType.NearFall,
                                Control = MatchPlanModel.ControlFor(worker),
                                Against = MatchPlanModel.ControlFor(target) },
                        new() { Type    = BeatType.FinishClean,
                                Control = MatchPlanModel.ControlFor(0),
                                Against = MatchPlanModel.ControlFor(sideCount - 1) }
                    ]
                };

                var errors = plan.Validate();
                if (errors.Count > 0) output.WriteLine($"  {worker}->{target}: {string.Join(" | ", errors)}");
                Assert.Empty(errors);

                // And it reaches the engine, rather than validating and then being ignored.
                var r = new MatchEngine(20260906).Execute(plan);
                string line = string.Join(" ", r.BeatResults
                    .Single(x => x.BeatType == BeatType.NearFall).Commentary);

                Assert.Contains($"W{target}", line);
            }
        }

        /// <summary>
        /// **A beat aimed at somebody who is not in the match is refused, not narrated.**
        ///
        /// Without this it falls through to the engine's rotation and quietly describes
        /// somebody else — the plan is wrong and the play-by-play is plausible, which is the
        /// worst combination.
        /// </summary>
        [Fact]
        public void ABeatAimedOutsideTheMatch_IsRefused()
        {
            var plan = ThreeWay(new() { Type = BeatType.NearFall,
                                        Control = BeatControl.WrestlerA,
                                        Against = BeatControl.SideD });

            var errors = plan.Validate();
            output.WriteLine($"  {string.Join(" | ", errors)}");

            Assert.Contains(errors, e => e.Contains("not in this match"));
        }

        /// <summary>
        /// **And a beat aimed at the side working it is refused.** Nobody runs a spot on
        /// themselves; that is the sentence two review rounds were spent removing.
        ///
        /// Reachable by going backwards through the chip rows rather than forwards: the
        /// "against" options hide the controlling side, so aim a beat at Bravo and then hand
        /// Bravo the control. `SetControl` drops the target when that happens, and this is
        /// the backstop for a plan built any other way.
        /// </summary>
        [Fact]
        public void ABeatAimedAtItsOwnWorker_IsRefused()
        {
            var plan = ThreeWay(new() { Type = BeatType.NearFall,
                                        Control = BeatControl.WrestlerB,
                                        Against = BeatControl.WrestlerB });

            var errors = plan.Validate();
            output.WriteLine($"  {string.Join(" | ", errors)}");

            Assert.Contains(errors, e => e.Contains("aimed at the side working it"));
        }

        /// <summary>
        /// **A multi-man beat needs a third party in the match.**
        ///
        /// The five of them had no gate and no validation rule, so they were offered enabled
        /// in a singles match, where a disposal spot narrates *"Alpha puts Bravo down hard on
        /// the outside — for now, this is one on one"* about a match that was already one on
        /// one. Nothing threw and the numbers were sane; it was a beat the booker could pick
        /// that meant nothing, which is the mirror image of the rule in CLAUDE.md.
        ///
        /// The tag formula has had exactly this gate all along — nothing in it works without
        /// somebody on the apron — so this is that rule pointed the other way.
        /// </summary>
        [Theory]
        [InlineData(BeatType.DisposalSpot)]
        [InlineData(BeatType.PinBreak)]
        [InlineData(BeatType.SpiteBreak)]
        [InlineData(BeatType.IgnoredOpportunity)]
        [InlineData(BeatType.MutualDestruction)]
        public void AMultiManBeat_IsRefusedInATwoSidedMatch(BeatType type)
        {
            var singles = new MatchPlanModel
            {
                SideA = MatchSide.Of(W("Alpha")),
                SideB = MatchSide.Of(W("Bravo")),
                Beats =
                [
                    new() { Type = BeatType.HotOpening, Control = BeatControl.Even },
                    new() { Type = type, Control = BeatControl.WrestlerA },
                    new() { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }
                ]
            };

            var errors = singles.Validate();
            output.WriteLine($"  singles/{type}: {string.Join(" | ", errors)}");
            Assert.Contains(errors, e => e.Contains("needs a third party"));

            // And the same beat in a three-way is a perfectly good booking — the gate must
            // refuse the shape, not the beat.
            var threeWay = ThreeWay(new() { Type = type, Control = BeatControl.WrestlerA });
            Assert.Empty(threeWay.Validate());
        }

        /// <summary>A three-way carrying one beat under test, otherwise valid.</summary>
        private static MatchPlanModel ThreeWay(MatchBeat middle) => new()
        {
            Sides = [MatchSide.Of(W("Alpha")), MatchSide.Of(W("Bravo")), MatchSide.Of(W("Charlie"))],
            Beats =
            [
                new() { Type = BeatType.HotOpening, Control = BeatControl.Even },
                middle,
                new() { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA,
                        Against = BeatControl.SideC }
            ]
        };
    }
}
