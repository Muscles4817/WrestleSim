using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The tag half of <see cref="MatchMatrixTests"/>.
    ///
    /// This exists because of a review finding, and the finding is worth recording. When
    /// tag structures were added, `MatchMatrixTests` was narrowed to `ForSideSize(1)` —
    /// correctly, since a tag structure cannot be booked one-on-one — with a comment
    /// claiming the equivalent sweep for tag structures lived elsewhere. It did not. The
    /// entire new code path shipped with no distributional audit at all, which is exactly
    /// the failure mode `MatchMatrixTests` was written to catch: not "this match rated
    /// wrong" but "everything rates the same", which no single-match test can see.
    ///
    /// Same shape as the singles sweep: every tag structure against every match type
    /// against real roster pairings, asserting on the distribution rather than on any one
    /// result.
    /// </summary>
    public class TagMatrixTests(ITestOutputHelper output)
    {
        private static readonly List<Wrestler> Roster = DataLoaders.LoadEmbeddedWrestlers();
        private const int RunsPerCell = 4;

        private sealed record Cell(
            string Structure, MatchType Type, string SideA, string SideB, string Feud,
            double Stars, double Tech, double Story, double Peak, double Avg, double FinishQuality);

        /// <summary>
        /// Teams built from adjacent roster members, so the sweep spans the real spread of
        /// overness and skill rather than a hand-picked pair.
        /// </summary>
        private static List<(Wrestler, Wrestler)> Teams()
        {
            var teams = new List<(Wrestler, Wrestler)>();
            for (int i = 0; i + 1 < Roster.Count; i += 2)
                teams.Add((Roster[i], Roster[i + 1]));
            return teams;
        }

        private static List<Cell> Sweep(int runsPerCell = RunsPerCell)
        {
            var cells = new List<Cell>();
            var teams = Teams();

            foreach (var st in MatchStructureLibrary.ForSideSize(2))
            foreach (var feudMode in new[] { "None", "Nuclear" })
            {
                if (st.RequiresFeud && feudMode == "None") continue;

                foreach (var (a1, a2) in teams)
                foreach (var (b1, b2) in teams)
                {
                    if (ReferenceEquals(a1, b1)) continue;

                    var feud = feudMode == "None"
                        ? null
                        : BuildFeud(a1, b1);

                    foreach (MatchType type in Enum.GetValues<MatchType>())
                    {
                        for (int rep = 0; rep < runsPerCell; rep++)
                        {
                            int seed = StableSeed.From(
                                st.Name, feudMode, a1.RealName, b1.RealName, type, rep);

                            var plan = new MatchPlanModel
                            {
                                SideA     = MatchSide.Of(a1, a2),
                                SideB     = MatchSide.Of(b1, b2),
                                MatchType = type,
                                Feud      = feud,
                                Beats     = st.Beats.Select(x => x.Clone()).ToList()
                            };

                            var errors = plan.Validate();
                            Assert.True(errors.Count == 0,
                                $"{st.Name} did not validate as a tag match: {string.Join("; ", errors)}");

                            var r = new MatchEngine(seed).Execute(plan);

                            cells.Add(new Cell(st.Name, type,
                                $"{a1.RingName} & {a2.RingName}", $"{b1.RingName} & {b2.RingName}",
                                feudMode, r.StarRating, r.TechnicalScore, r.StorytellingScore,
                                r.CrowdPeakEnergy, r.CrowdAverageEnergy, r.FinishQuality));
                        }
                    }
                }
            }

            return cells;
        }

        private static Feud BuildFeud(Wrestler a, Wrestler b)
        {
            var feud = new Feud { WrestlerA = a, WrestlerB = b, Intensity = FeudIntensity.None };
            feud.SetMinimumIntensity(FeudIntensity.Nuclear);
            return feud;
        }

        [Fact]
        public void EveryCombination_ExecutesWithoutError_AndProducesALegalRating()
        {
            var cells = Sweep();
            output.WriteLine($"  {cells.Count:N0} tag matches executed across " +
                             $"{MatchStructureLibrary.ForSideSize(2).Count()} structures × " +
                             $"{Enum.GetValues<MatchType>().Length} types × {Teams().Count} teams");

            Assert.NotEmpty(cells);
            Assert.All(cells, c =>
            {
                Assert.InRange(c.Stars, 0.0, 5.0);
                Assert.InRange(c.Peak, 0.0, 100.0);
                Assert.InRange(c.Avg, 0.0, 100.0);
                Assert.InRange(c.FinishQuality, 0.0, 100.0);
                Assert.True(double.IsFinite(c.Tech) && c.Tech >= 0, $"Bad technical score {c.Tech}");
                Assert.True(double.IsFinite(c.Story) && c.Story >= 0, $"Bad storytelling score {c.Story}");
            });
        }

        [Fact]
        public void EveryTagStructure_IsDistinct()
        {
            var cells = Sweep();
            var byStructure = cells.GroupBy(c => c.Structure)
                                   .ToDictionary(g => g.Key, g => g.Average(c => c.Stars));

            foreach (var kv in byStructure.OrderBy(k => k.Value))
                output.WriteLine($"  {kv.Key,-20} {kv.Value:F3}");

            Assert.Equal(MatchStructureLibrary.ForSideSize(2).Count(), byStructure.Count);

            double spread = byStructure.Values.Max() - byStructure.Values.Min();
            output.WriteLine($"  structure spread: {spread:F3}");

            Assert.True(spread > 0.25,
                $"Tag structures barely differ from each other ({spread:F3}) — the formula is not " +
                "doing anything the sprint is not.");
            Assert.True(spread < 2.0,
                $"One tag structure dominates by {spread:F3} stars — structure choice is the whole game.");
        }

        [Fact]
        public void Ratings_SpanAWideRange_AndDoNotPileUpAtTheCeiling()
        {
            var cells = Sweep();
            var stars = cells.Select(c => c.Stars).OrderBy(x => x).ToList();

            double p05 = stars[(int)(stars.Count * 0.05)];
            double p50 = stars[stars.Count / 2];
            double p95 = stars[(int)(stars.Count * 0.95)];

            output.WriteLine($"  p05 {p05:F2}  median {p50:F2}  p95 {p95:F2}  max {stars[^1]:F2}");

            Assert.True(p95 - p05 > 1.0,
                $"Tag ratings only span {p95 - p05:F2} stars from the 5th to the 95th percentile.");
            Assert.True(stars.Count(x => x >= 4.75) < stars.Count * 0.05,
                "Too many tag matches are pinned near five stars.");
        }

        [Fact]
        public void CrowdPeak_IsNotAConstant()
        {
            var cells = Sweep();
            var peaks = cells.Select(c => c.Peak).ToList();

            double spread = peaks.Max() - peaks.Min();
            output.WriteLine($"  crowd peak {peaks.Min():F1} – {peaks.Max():F1} (spread {spread:F1})");

            Assert.True(spread > 20,
                $"Crowd peak barely moves across the whole tag matrix ({spread:F1}).");
        }

        [Fact]
        public void TheMatchupMatters_NotJustTheStructure()
        {
            var cells = Sweep();

            double structureSpread = cells.GroupBy(c => c.Structure)
                                          .Select(g => g.Average(c => c.Stars))
                                          .Pipe(v => v.Max() - v.Min());

            double matchupSpread = cells.GroupBy(c => (c.SideA, c.SideB))
                                        .Select(g => g.Average(c => c.Stars))
                                        .Pipe(v => v.Max() - v.Min());

            output.WriteLine($"  structure spread {structureSpread:F3}, matchup spread {matchupSpread:F3}");

            Assert.True(matchupSpread > structureSpread,
                "Who is in the match should matter at least as much as which structure it is — " +
                $"got matchup {matchupSpread:F3} vs structure {structureSpread:F3}.");
        }

        [Fact]
        public void DeclaringAMatchType_MovesEveryTagStructure()
        {
            // The gap review found: none of the tag beats appeared in the engine's
            // match-type coherence sets, so declaring a type on a tag structure was a pure
            // penalty — a Southern Tag, the most story-driven structure in the library, was
            // docked for being called Storytelling.
            var cells = Sweep();

            foreach (var st in cells.GroupBy(c => c.Structure))
            {
                var byType = st.GroupBy(c => c.Type)
                               .ToDictionary(g => g.Key, g => g.Average(c => c.Stars));

                string best = byType.OrderByDescending(k => k.Value).First().Key.ToString();
                output.WriteLine($"  {st.Key,-16} " +
                                 string.Join("  ", byType.Select(k => $"{k.Key} {k.Value:F2}")) +
                                 $"   → best: {best}");

                double spread = byType.Values.Max() - byType.Values.Min();
                Assert.True(spread > 0.05,
                    $"Match type does nothing to {st.Key} ({spread:F3}).");
            }

            // And a declared type must be able to beat Standard somewhere, or declaring one
            // is never worth doing.
            var storytellingWins = cells
                .Where(c => c.Structure == "Southern Tag")
                .GroupBy(c => c.Type)
                .ToDictionary(g => g.Key, g => g.Average(c => c.Stars));

            Assert.True(storytellingWins[MatchType.Storytelling] > storytellingWins[MatchType.Technical],
                "The Southern Tag is a story structure. Calling it Storytelling should beat " +
                "calling it Technical.");
        }
    }

    internal static class EnumerableExtensions
    {
        public static TResult Pipe<T, TResult>(this IEnumerable<T> source, Func<List<T>, TResult> f) =>
            f(source.ToList());
    }
}
