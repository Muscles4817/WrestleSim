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

        /// <summary>The shapes the builder's step 0 offers, as (sides, a side).</summary>
        public static TheoryData<int, int> Shapes => new() { { 2, 1 }, { 2, 2 }, { 2, 3 }, { 3, 1 }, { 4, 1 } };

        [Theory]
        [MemberData(nameof(Shapes))]
        public void EveryShapeTheBuilderOffers_HasAPresetThatValidates(int sideCount, int sideSize)
        {
            var structures = MatchStructureLibrary.ForShape(sideCount, sideSize).ToList();

            output.WriteLine($"  {sideCount} sides × {sideSize}: " +
                             (structures.Count == 0 ? "NO PRESETS"
                                                    : string.Join(", ", structures.Select(s => s.Name))));

            Assert.NotEmpty(structures);

            foreach (var structure in structures)
            {
                var plan = Build(sideCount, sideSize, structure);
                var errors = plan.Validate();
                if (errors.Count > 0)
                    output.WriteLine($"    {structure.Name}: {string.Join(" | ", errors)}");

                Assert.Empty(errors);
            }
        }

        /// <summary>And each one runs, producing the winner and the pinned side it booked.</summary>
        [Theory]
        [MemberData(nameof(Shapes))]
        public void EveryPreset_RunsAndProducesTheBookedResult(int sideCount, int sideSize)
        {
            foreach (var structure in MatchStructureLibrary.ForShape(sideCount, sideSize))
            {
                var plan = Build(sideCount, sideSize, structure);
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

        private static MatchPlanModel Build(int sideCount, int sideSize, MatchStructure structure)
        {
            var plan = new MatchPlanModel
            {
                Sides = Enumerable.Range(0, sideCount)
                    .Select(side => MatchSide.Of(Enumerable.Range(0, sideSize)
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
        public void AMatchIsBilledWithEverybodyInIt(int sideCount, int sideSize)
        {
            var structure = MatchStructureLibrary.ForShape(sideCount, sideSize).First();
            var booked = new BookedMatch { Plan = Build(sideCount, sideSize, structure) };

            output.WriteLine($"  {sideCount}×{sideSize}: {booked.Name}");

            foreach (var w in booked.Plan.AllParticipants)
                Assert.Contains(w.RingName, booked.Name);

            Assert.Equal(sideCount - 1, booked.Name.Split(" vs ").Length - 1);
        }

    }
}
