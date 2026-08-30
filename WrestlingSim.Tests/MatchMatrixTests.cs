using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Sweeps the whole booking space — every structure against every match type against
    /// every matchup on the shipped roster — and asserts on the shape of the results.
    ///
    /// This is the audit that found the original problems, kept as a permanent test. It is
    /// deliberately about distributions rather than individual matches: the failure mode
    /// being guarded against is not "this match rated wrong", it is "everything rates the
    /// same", which no single-match test can see.
    /// </summary>
    public class MatchMatrixTests(ITestOutputHelper output)
    {
        private static readonly List<Wrestler> Roster = DataLoaders.LoadEmbeddedWrestlers();
        private const int RunsPerCell = 6;

        private sealed record Cell(
            string Structure, MatchType Type, string A, string B, string Feud, double Stars,
            double Tech, double Story, double Peak, double Avg, double FinishQuality);

        /// <summary>
        /// Executes the full matrix once. Kept in one place so several tests can assert on
        /// different properties of the same sweep without re-running it per assertion.
        /// </summary>
        private static List<Cell> Sweep(int runsPerCell = RunsPerCell)
        {
            var cells = new List<Cell>();

            foreach (var st in MatchStructureLibrary.All)
            foreach (var feudMode in new[] { "None", "Nuclear" })
            {
                if (st.RequiresFeud && feudMode == "None") continue;

                foreach (var a in Roster)
                foreach (var b in Roster)
                {
                    if (ReferenceEquals(a, b)) continue;

                    Feud? feud = null;
                    if (feudMode != "None")
                    {
                        feud = new Feud { WrestlerA = a, WrestlerB = b };
                        feud.SetMinimumIntensity(FeudIntensity.Nuclear);
                        feud.AddTag(FeudHistoryTag.FamilyInvolved);
                        feud.AddTag(FeudHistoryTag.ManagerConflict);
                    }

                    foreach (var type in Enum.GetValues<MatchType>())
                    {
                        for (int rep = 0; rep < runsPerCell; rep++)
                        {
                            int seed = HashCode.Combine(st.Name, feudMode, a.RealName, b.RealName, type, rep)
                                       & 0x7FFFFFFF;

                            var r = new MatchEngine(seed).Execute(new MatchPlan
                            {
                                WrestlerA = a, WrestlerB = b, MatchType = type, Feud = feud,
                                Beats = st.Beats.Select(x => x.Clone()).ToList()
                            });

                            cells.Add(new Cell(st.Name, type, a.RingName, b.RingName, feudMode,
                                r.StarRating, r.TechnicalScore, r.StorytellingScore,
                                r.CrowdPeakEnergy, r.CrowdAverageEnergy, r.FinishQuality));
                        }
                    }
                }
            }

            return cells;
        }

        // ── Everything executes ──────────────────────────────────────────────

        [Fact]
        public void EveryCombination_ExecutesWithoutError_AndProducesALegalRating()
        {
            var cells = Sweep();
            output.WriteLine($"  {cells.Count:N0} matches executed across " +
                             $"{MatchStructureLibrary.All.Count} structures × " +
                             $"{Enum.GetValues<MatchType>().Length} types × {Roster.Count} wrestlers");

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
        public void EveryStructure_IsBookableAndDistinct()
        {
            var cells = Sweep();
            var byStructure = cells.GroupBy(c => c.Structure)
                                   .ToDictionary(g => g.Key, g => g.Average(c => c.Stars));

            foreach (var kv in byStructure.OrderBy(k => k.Value))
                output.WriteLine($"  {kv.Key,-20} {kv.Value:F3}");

            Assert.Equal(MatchStructureLibrary.All.Count, byStructure.Count);

            // Structures should be meaningfully different from each other, but no structure
            // should be so dominant that it is the only correct answer.
            double spread = byStructure.Values.Max() - byStructure.Values.Min();
            output.WriteLine($"  structure spread: {spread:F3}");

            Assert.True(spread > 0.35, $"Structures barely differ from each other ({spread:F3}).");
            Assert.True(spread < 2.0,
                $"One structure dominates the rest by {spread:F3} stars — structure choice is the whole game.");
        }

        // ── The distribution has to have shape ───────────────────────────────

        [Fact]
        public void Ratings_SpanAWideRange_AndDoNotPileUpAtTheCeiling()
        {
            var cells = Sweep();
            var stars = cells.Select(c => c.Stars).OrderBy(s => s).ToList();

            double min = stars.First(), max = stars.Last();
            double mean = stars.Average();
            double p05 = stars[(int)(stars.Count * 0.05)];
            double p95 = stars[(int)(stars.Count * 0.95)];
            double perfect = 100.0 * stars.Count(s => s >= 4.99) / stars.Count;

            output.WriteLine($"  n={stars.Count:N0}  min {min:F2}  p05 {p05:F2}  mean {mean:F2}  p95 {p95:F2}  max {max:F2}");
            output.WriteLine($"  perfect (>=4.99): {perfect:F2}%");

            Assert.True(max - min > 1.5,
                $"The engine should produce a wide variety of results, got {min:F2}–{max:F2}");
            Assert.True(p95 - p05 > 0.9,
                $"The middle 90% of results should still span a real range, got {p05:F2}–{p95:F2}");
            Assert.True(perfect < 1.0,
                $"{perfect:F2}% of all matches rated a perfect 5.00 — the ceiling is being hit routinely.");
            Assert.InRange(mean, 2.8, 4.3);
        }

        [Fact]
        public void CrowdPeak_IsNotAConstant()
        {
            // The single clearest symptom of the original engine: crowd peak was pinned at
            // exactly 100 in 92.8% of all matches, so 35% of the score carried no signal.
            var cells = Sweep();
            double pinned = 100.0 * cells.Count(c => c.Peak >= 99.99) / cells.Count;
            var peaks = cells.Select(c => c.Peak).ToList();

            output.WriteLine($"  peak pinned at 100: {pinned:F1}%   range {peaks.Min():F1}–{peaks.Max():F1}");

            Assert.True(pinned < 15.0,
                $"Crowd peak hit the 100 ceiling in {pinned:F1}% of matches; it is close to a constant again.");
            Assert.True(peaks.Max() - peaks.Min() > 30,
                $"Crowd peak should vary widely across the matrix, got {peaks.Min():F1}–{peaks.Max():F1}");
        }

        [Fact]
        public void MatchType_ChangesEveryStructure()
        {
            var cells = Sweep();

            // For each structure, the four match types must not collapse onto one value.
            foreach (var g in cells.GroupBy(c => c.Structure))
            {
                var byType = g.GroupBy(c => c.Type).ToDictionary(t => t.Key, t => t.Average(c => c.Stars));
                double spread = byType.Values.Max() - byType.Values.Min();

                output.WriteLine($"  {g.Key,-20} " +
                                 string.Join("  ", byType.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value:F2}")) +
                                 $"   spread {spread:F3}");

                Assert.True(spread > 0.05,
                    $"On {g.Key} all four match types produced effectively the same rating ({spread:F3}).");
            }
        }

        [Fact]
        public void Matchup_MattersAsMuchAsStructure()
        {
            // The original complaint: structure swamped everything, and who was in the match
            // was worth 0.29 stars against structure's 1.65. These should be comparable.
            var cells = Sweep();

            var byStructure = cells.GroupBy(c => c.Structure).Select(g => g.Average(c => c.Stars)).ToList();
            var byMatchup   = cells.GroupBy(c => (c.A, c.B)).Select(g => g.Average(c => c.Stars)).ToList();

            double structureSpread = byStructure.Max() - byStructure.Min();
            double matchupSpread   = byMatchup.Max() - byMatchup.Min();

            output.WriteLine($"  structure spread {structureSpread:F3}   matchup spread {matchupSpread:F3}");
            output.WriteLine($"  ratio {structureSpread / matchupSpread:F2}:1 (was 5.7:1 before the performer model)");

            Assert.True(matchupSpread > 0.45,
                $"Who is in the match should be worth real rating, got {matchupSpread:F3} stars.");
            Assert.True(structureSpread / matchupSpread < 3.0,
                $"Structure still outweighs the roster by {structureSpread / matchupSpread:F1}:1; " +
                "booking is drowning out the wrestlers.");
        }

        [Fact]
        public void Feuds_ImproveEveryStructureTheyApplyTo()
        {
            var cells = Sweep();

            foreach (var g in cells.Where(c => !MatchStructureLibrary.Find(c.Structure)!.RequiresFeud)
                                   .GroupBy(c => c.Structure))
            {
                double none    = g.Where(c => c.Feud == "None").Average(c => c.Stars);
                double nuclear = g.Where(c => c.Feud == "Nuclear").Average(c => c.Stars);

                output.WriteLine($"  {g.Key,-20} none {none:F3} -> nuclear {nuclear:F3}  ({nuclear - none:+0.000;-0.000})");

                Assert.True(nuclear > none + 0.10,
                    $"A nuclear feud should lift {g.Key} by more than 0.10 stars, got {nuclear - none:F3}");
            }
        }

        [Fact]
        public void NoCombination_ProducesADegenerateResult()
        {
            var cells = Sweep();

            // Nothing in the legal booking space should bottom out at zero or max out at 5.
            var atFloor   = cells.Where(c => c.Stars <= 0.01).ToList();
            var atCeiling = cells.Where(c => c.Stars >= 4.995).ToList();

            foreach (var c in atCeiling.Take(5))
                output.WriteLine($"  CEILING {c.Structure}/{c.Type}/{c.Feud} {c.A} vs {c.B}");
            foreach (var c in atFloor.Take(5))
                output.WriteLine($"  FLOOR   {c.Structure}/{c.Type}/{c.Feud} {c.A} vs {c.B}");

            Assert.Empty(atFloor);
            Assert.True(atCeiling.Count < cells.Count * 0.005,
                $"{atCeiling.Count} of {cells.Count} combinations maxed out the scale.");
        }
    }
}
