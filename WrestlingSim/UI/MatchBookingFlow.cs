using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.UI
{
    public static class MatchBookingFlow
    {
        // ── Entry point ──────────────────────────────────────────────────────

        public static void Run(List<Wrestler> wrestlers)
        {
            Console.Clear();
            DrawHeader("BOOK A SINGLES MATCH");

            var a         = SelectWrestler("WRESTLER A", wrestlers);
            var b         = SelectWrestler("WRESTLER B", wrestlers, exclude: a);
            var matchType = SelectMatchType();
            var feud      = SetupFeud(a, b);
            var beats     = SelectStructure(feud);

            while (true)
            {
                BeatEditor(beats, a, b, feud);

                var plan = new MatchPlan
                {
                    WrestlerA = a,
                    WrestlerB = b,
                    MatchType = matchType,
                    Feud      = feud,
                    Beats     = beats
                };

                var errors = plan.Validate();
                if (errors.Count > 0)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    foreach (var e in errors)
                        Console.WriteLine($"  ✖  {e}");
                    Console.ResetColor();
                    Console.WriteLine("\n  Fix the plan and try again — press any key...");
                    Console.ReadKey(true);
                    continue;
                }

                var result = new MatchEngine().Execute(plan);
                DisplayResults(result, a, b);
                Pause();
                return;
            }
        }

        // ── Wrestler selection ───────────────────────────────────────────────

        private static Wrestler SelectWrestler(string label, List<Wrestler> wrestlers, Wrestler? exclude = null)
        {
            var pool = wrestlers.Where(w => w != exclude).ToList();

            Console.WriteLine($"\n  ── {label} " + new string('─', Math.Max(1, 40 - label.Length)));
            for (int i = 0; i < pool.Count; i++)
            {
                var w = pool[i];
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{i + 1,2}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(Fit(w.RingName, 22));
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"Pop {w.Popularity,3}  Skill {w.RingSkills.GetOverallSkill():F1}  Cha {w.Charisma:F1}");
                Console.ResetColor();
            }

            while (true)
            {
                Console.Write("\n  Select: ");
                if (int.TryParse(Console.ReadLine(), out int n) && n >= 1 && n <= pool.Count)
                    return pool[n - 1];
                Console.WriteLine("  Invalid — try again.");
            }
        }

        // ── Match type ───────────────────────────────────────────────────────

        private static MatchType SelectMatchType()
        {
            var types = Enum.GetValues<MatchType>().ToList();

            Console.WriteLine("\n  ── MATCH TYPE " + new string('─', 28));
            for (int i = 0; i < types.Count; i++)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{i + 1}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(types[i]);
                Console.ResetColor();
            }
            Console.Write("\n  Select (Enter = Standard): ");

            if (int.TryParse(Console.ReadLine(), out int c) && c >= 1 && c <= types.Count)
                return types[c - 1];
            return MatchType.Standard;
        }

        // ── Feud setup ───────────────────────────────────────────────────────

        private static Feud? SetupFeud(Wrestler a, Wrestler b)
        {
            Console.WriteLine("\n  ── FEUD SETUP " + new string('─', 28));
            Console.Write($"  Active feud between {a.RingName} and {b.RingName}? (y/n): ");
            if (!(Console.ReadLine() ?? "").TrimStart().StartsWith("y", StringComparison.OrdinalIgnoreCase))
                return null;

            // Intensity
            var intensities = new[] { FeudIntensity.Cold, FeudIntensity.Building, FeudIntensity.Hot, FeudIntensity.Nuclear };
            Console.WriteLine("\n  Feud intensity:");
            for (int i = 0; i < intensities.Length; i++)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{i + 1}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(intensities[i]);
                Console.ResetColor();
            }
            Console.Write("  Select (Enter = Building): ");
            var intensity = FeudIntensity.Building;
            if (int.TryParse(Console.ReadLine(), out int ic) && ic >= 1 && ic <= intensities.Length)
                intensity = intensities[ic - 1];

            // History tags
            var allTags = Enum.GetValues<FeudHistoryTag>().ToList();
            Console.WriteLine("\n  History tags (comma-separated numbers, 0 for none):");
            for (int i = 0; i < allTags.Count; i++)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{i + 1}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(allTags[i]);
                Console.ResetColor();
            }
            Console.Write("  Select: ");

            var history = (Console.ReadLine() ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out int n) && n >= 1 && n <= allTags.Count
                    ? (FeudHistoryTag?)allTags[n - 1] : null)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .ToList();

            return new Feud { WrestlerA = a, WrestlerB = b, Intensity = intensity, History = history };
        }

        // ── Structure selection ──────────────────────────────────────────────

        private static List<MatchBeat> SelectStructure(Feud? feud)
        {
            var all = MatchStructureLibrary.All;

            Console.WriteLine("\n  ── MATCH STRUCTURE " + new string('─', 23));
            Console.WriteLine();

            for (int i = 0; i < all.Count; i++)
            {
                var s         = all[i];
                bool feudWarn = s.RequiresFeud && feud == null;

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{i + 1}] ");
                Console.ForegroundColor = feudWarn ? ConsoleColor.DarkGray : ConsoleColor.White;
                Console.Write(Fit(s.Name, 22));
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"{s.Beats.Count} beats");
                if (feudWarn)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write("  ⚠ needs feud");
                }
                Console.ResetColor();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("       " + Truncate(s.Description, 66));
                Console.ResetColor();
                Console.WriteLine();
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"  [{all.Count + 1}] ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("Build from scratch    ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("2 beats — opening + finish pre-loaded");
            Console.ResetColor();

            Console.Write("\n  Select structure: ");
            if (int.TryParse(Console.ReadLine(), out int c) && c >= 1 && c <= all.Count)
                return all[c - 1].Beats.ToList();

            return
            [
                BeatLibrary.Find("Standard Collar-and-Elbow")!.ToMatchBeat(BeatControl.Even),
                BeatLibrary.Find("Clean Victory")!.ToMatchBeat(BeatControl.WrestlerA),
            ];
        }

        // ── Beat editor ──────────────────────────────────────────────────────

        private static void BeatEditor(List<MatchBeat> beats, Wrestler a, Wrestler b, Feud? feud)
        {
            while (true)
            {
                Console.Clear();
                DrawHeader("BEAT EDITOR");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  {a.RingName}  (A)     vs     {b.RingName}  (B)");
                Console.ResetColor();

                DisplayPlan(beats, a, b);

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("  [A]dd   [R]emove   [C]hange control   [G]o");
                Console.ResetColor();
                Console.Write("\n\n  > ");

                switch ((Console.ReadLine() ?? "").Trim().ToUpperInvariant())
                {
                    case "A": AddBeat(beats, a, b, feud);  break;
                    case "R": RemoveBeat(beats, a, b);     break;
                    case "C": ChangeControl(beats, a, b);  break;
                    case "G": return;
                }
            }
        }

        private static void AddBeat(List<MatchBeat> beats, Wrestler a, Wrestler b, Feud? feud)
        {
            Console.WriteLine("\n  ── ADD A BEAT " + new string('─', 28));

            var available = BeatLibrary.Available(feud).ToList();
            var indexed   = new List<BeatTemplate>();
            string? lastCat = null;
            int n = 1;

            foreach (var t in available)
            {
                if (t.Category != lastCat)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  {t.Category.ToUpperInvariant()}");
                    Console.ResetColor();
                    lastCat = t.Category;
                }
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{n,2}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(Fit(t.Name, 28));
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(Truncate(t.Description, 42));
                Console.ResetColor();
                indexed.Add(t);
                n++;
            }

            Console.Write("\n  Select (0 to cancel): ");
            if (!int.TryParse(Console.ReadLine(), out int c) || c < 1 || c > indexed.Count)
                return;

            var control = SelectControl(a, b);

            // Insert before the last beat (keeps the finish at the end)
            int at = Math.Max(0, beats.Count - 1);
            beats.Insert(at, indexed[c - 1].ToMatchBeat(control));
        }

        private static void RemoveBeat(List<MatchBeat> beats, Wrestler a, Wrestler b)
        {
            Console.WriteLine("\n  ── REMOVE A BEAT " + new string('─', 25));
            DisplayPlan(beats, a, b);
            Console.Write("  Beat number (0 to cancel): ");
            if (!int.TryParse(Console.ReadLine(), out int n) || n == 0) return;

            int idx = n - 1;
            if (idx < 0 || idx >= beats.Count)               { Warn("Invalid selection.");                      return; }
            if (beats[idx].IsOpening && beats.Count(x => x.IsOpening) <= 1) { Warn("Cannot remove the only opening beat."); return; }
            if (beats[idx].IsFinish  && beats.Count(x => x.IsFinish)  <= 1) { Warn("Cannot remove the only finish beat.");  return; }

            beats.RemoveAt(idx);
        }

        private static void ChangeControl(List<MatchBeat> beats, Wrestler a, Wrestler b)
        {
            Console.WriteLine("\n  ── CHANGE CONTROL " + new string('─', 24));
            DisplayPlan(beats, a, b);
            Console.Write("  Beat number (0 to cancel): ");
            if (!int.TryParse(Console.ReadLine(), out int n) || n == 0) return;

            int idx = n - 1;
            if (idx < 0 || idx >= beats.Count) { Warn("Invalid selection."); return; }

            beats[idx].Control = SelectControl(a, b);
        }

        private static BeatControl SelectControl(Wrestler? a = null, Wrestler? b = null)
        {
            Console.WriteLine("\n  Control:");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  [1] "); Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(a != null ? $"WrestlerA — {a.RingName}" : "WrestlerA");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  [2] "); Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(b != null ? $"WrestlerB — {b.RingName}" : "WrestlerB");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  [3] Even");
            Console.WriteLine("  [4] Contested (rapid back-and-forth)");
            Console.ResetColor();
            Console.Write("  Select (Enter = Even): ");

            return (Console.ReadLine() ?? "").Trim() switch
            {
                "1" => BeatControl.WrestlerA,
                "2" => BeatControl.WrestlerB,
                "4" => BeatControl.Contested,
                _   => BeatControl.Even
            };
        }

        // ── Plan display ─────────────────────────────────────────────────────

        private static void DisplayPlan(List<MatchBeat> beats, Wrestler? a, Wrestler? b)
        {
            const int typeW = 24;
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {"#",3}  {"TYPE",-typeW}  CONTROL");
            Console.WriteLine("  " + new string('─', 54));
            Console.ResetColor();

            for (int i = 0; i < beats.Count; i++)
            {
                var beat = beats[i];
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  {i + 1,3}  ");

                if (beat.IsFinish)       Console.ForegroundColor = ConsoleColor.Yellow;
                else if (beat.IsOpening) Console.ForegroundColor = ConsoleColor.Cyan;
                else                     Console.ForegroundColor = ConsoleColor.White;

                Console.Write(Fit(BeatTypeName(beat.Type), typeW));
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  {ControlLabel(beat.Control, a, b)}");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  " + new string('─', 54));
            Console.ResetColor();
        }

        // ── Results display ──────────────────────────────────────────────────

        private static void DisplayResults(MatchEngineResult result, Wrestler a, Wrestler b)
        {
            Console.Clear();
            DrawHeader("MATCH RESULT");

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  {a.RingName}  vs  {b.RingName}");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  WINNER        {result.Winner.RingName}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  RATING        {result.StarDisplay}");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  Technical     {result.Bar(result.TechnicalScore,    60)}  {result.TechnicalScore:F0} / 60");
            Console.WriteLine($"  Storytelling  {result.Bar(result.StorytellingScore, 80)}  {result.StorytellingScore:F0} / 80");
            Console.WriteLine($"  Crowd         {result.Bar(result.CrowdAverageEnergy)}  {result.CrowdAverageEnergy:F0} / 100");
            Console.ResetColor();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  ── PLAY BY PLAY " + new string('─', 40));
            Console.ResetColor();
            Console.WriteLine();

            foreach (var line in result.PlayByPlay)
            {
                if (line.StartsWith('['))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  {line}");
                }
                else if (line.StartsWith("  ▶"))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  {line}");
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"  {line}");
                }
                else
                {
                    Console.WriteLine();
                }
                Console.ResetColor();
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string BeatTypeName(BeatType type) => type switch
        {
            BeatType.HotOpening          => "Hot Opening",
            BeatType.SlowOpening         => "Slow Opening",
            BeatType.StandardOpening     => "Opening",
            BeatType.HeatSegment         => "Heat Segment",
            BeatType.Comeback            => "Comeback",
            BeatType.RestHold            => "Rest Hold",
            BeatType.HighSpot            => "High Spot",
            BeatType.CrowdBrawl          => "Crowd Brawl",
            BeatType.PsychologicalWarfare => "Psychological Warfare",
            BeatType.RevengeSpot         => "Revenge Spot",
            BeatType.FeudalEscalation    => "Feudal Escalation",
            BeatType.ThirdPartyPullIn    => "Third Party Pull-In",
            BeatType.AlliesRejected      => "Goes It Alone",
            BeatType.NearFall            => "Near Fall",
            BeatType.FinishClean         => "Finish: Clean",
            BeatType.FinishRollup        => "Finish: Roll-Up",
            BeatType.FinishSubmission    => "Finish: Submission",
            BeatType.FinishDQ            => "Finish: DQ",
            BeatType.FinishCountout      => "Finish: Count-Out",
            BeatType.FinishInterference  => "Finish: Interference",
            BeatType.FinishSuperFinisher => "Finish: Super Finisher",
            _                            => type.ToString()
        };

        private static string ControlLabel(BeatControl control, Wrestler? a, Wrestler? b) => control switch
        {
            BeatControl.WrestlerA => a != null ? $"A — {a.RingName}" : "WrestlerA",
            BeatControl.WrestlerB => b != null ? $"B — {b.RingName}" : "WrestlerB",
            BeatControl.Even      => "Even",
            BeatControl.Contested => "Contested",
            _                     => control.ToString()
        };

        private static string Fit(string s, int len) =>
            s.Length > len ? s[..(len - 1)] + "…" : s.PadRight(len);

        private static string Truncate(string s, int max) =>
            s.Length > max ? s[..(max - 1)] + "…" : s;

        private static void DrawHeader(string title)
        {
            const int W = 56;
            int pad = (W - title.Length) / 2;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ╔" + new string('═', W) + "╗");
            Console.WriteLine("  ║" + new string(' ', pad) + title + new string(' ', W - pad - title.Length) + "║");
            Console.WriteLine("  ╚" + new string('═', W) + "╝");
            Console.ResetColor();
        }

        private static void Warn(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n  {msg}");
            Console.ResetColor();
            Console.ReadKey(true);
        }

        private static void Pause()
        {
            Console.WriteLine("\n  Press any key to return to the main menu...");
            Console.ReadKey(true);
        }
    }
}
