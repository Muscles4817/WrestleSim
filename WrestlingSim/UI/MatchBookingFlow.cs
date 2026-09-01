using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using static WrestlingSim.UI.ConsoleUi;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.UI
{
    public static class MatchBookingFlow
    {
        // ── Entry points ─────────────────────────────────────────────────────

        /// <summary>Books a standalone match, runs it, and banks the feud heat.</summary>
        public static void Run(List<Wrestler> wrestlers, FeudBook feudBook)
        {
            ConsoleUi.Clear();
            DrawHeader("BOOK A SINGLES MATCH");

            var booked = BuildMatch(wrestlers, feudBook);
            if (booked == null) return;

            // Read the pairing's freshness before this match is added to it — see
            // docs/wrestling-reference/20-storylines-and-feuds.md §9.1. The console sandbox
            // has no world clock, so no date is passed and nothing is ever forgotten.
            var pairing = feudBook.GetOrCreate(booked.Plan.WrestlerA, booked.Plan.WrestlerB);
            var result = new MatchEngine().Execute(booked.Plan, pairing.Familiarity(null));

            var update = feudBook.Record(
                booked.Plan.WrestlerA, booked.Plan.WrestlerB,
                heat: result.StarRating * 2.0,
                tags: new[] { FeudHistoryTag.PriorMatch });
            update.Feud.RecordMatch(null);

            DisplayResults(result, booked.Plan.WrestlerA, booked.Plan.WrestlerB);
            SegmentBookingFlow.DisplayFeudUpdates(new[] { update });
            Pause("Press any key to return to the main menu...");
        }

        /// <summary>
        /// Builds a match without running it, for placing on a show card.
        /// Returns null if the player backs out.
        /// </summary>
        public static BookedMatch? BuildMatch(List<Wrestler> wrestlers, FeudBook feudBook)
        {
            var a = SelectWrestler("WRESTLER A", wrestlers);
            if (a == null) return null;

            var b = SelectWrestler("WRESTLER B", wrestlers, exclude: a);
            if (b == null) return null;

            var matchType = SelectMatchType();
            var feud      = ResolveFeud(a, b, feudBook);
            var (beats, structureName) = SelectStructure(feud);

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
                if (errors.Count == 0)
                    return new BookedMatch { Plan = plan, StructureName = structureName };

                Console.WriteLine();
                foreach (var e in errors)
                    WriteLine($"  ✖  {e}", ConsoleColor.Red);
                Pause("Fix the plan and try again — press any key...");
            }
        }

        // ── Wrestler selection ───────────────────────────────────────────────

        private static Wrestler? SelectWrestler(string label, List<Wrestler> wrestlers, Wrestler? exclude = null)
        {
            var pool = wrestlers.Where(w => w != exclude).ToList();

            Rule(label, 40);
            for (int i = 0; i < pool.Count; i++)
            {
                var w = pool[i];
                Write($"  [{i + 1,2}] ", ConsoleColor.DarkGray);
                Write(Fit(w.RingName, 22), ConsoleColor.White);
                WriteLine($"Over {w.OvernessDisplay,3}  Skill {w.RingSkills.GetOverallSkill():F1}  Cha {w.Charisma:F1}",
                          ConsoleColor.DarkGray);
            }

            Console.Write("\n  Select (0 to cancel): ");
            if (int.TryParse(Console.ReadLine(), out int n) && n >= 1 && n <= pool.Count)
                return pool[n - 1];

            return null;
        }

        // ── Match type ───────────────────────────────────────────────────────

        private static MatchType SelectMatchType()
        {
            Rule("MATCH TYPE", 34);
            return ChooseEnum<MatchType>("Select (Enter = Standard)") ?? MatchType.Standard;
        }

        // ── Feud ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads the feud these two have actually built through booked segments and
        /// matches. Falls back to declaring one by hand when there is no history yet.
        /// </summary>
        private static Feud? ResolveFeud(Wrestler a, Wrestler b, FeudBook feudBook)
        {
            Rule("FEUD", 34);

            var existing = feudBook.Find(a, b);
            if (existing != null && existing.Intensity > FeudIntensity.None)
            {
                WriteLine($"\n  {a.RingName} and {b.RingName} have history:", ConsoleColor.Cyan);
                WriteLine($"    Intensity : {existing.Intensity}  ({existing.Heat:F0} heat)", ConsoleColor.White);
                WriteLine($"    Matches   : {existing.MatchCount}", ConsoleColor.DarkGray);
                WriteLine($"    Freshness : {existing.Familiarity(null) * 100:F0}% — meeting {existing.NextMeetingNumber(null):F1}",
                          existing.Familiarity(null) < 0.8 ? ConsoleColor.Yellow : ConsoleColor.DarkGray);
                WriteLine($"    History   : {(existing.History.Count > 0 ? string.Join(", ", existing.History) : "none")}",
                          ConsoleColor.DarkGray);

                if (existing.HeatToNextTier is > 0 and var toNext)
                    WriteLine($"    {toNext:F0} more heat to the next tier.", ConsoleColor.DarkGray);

                Console.WriteLine();
                if (YesNo("Use this feud?", defaultYes: true)) return existing;
            }
            else
            {
                WriteLine($"\n  {a.RingName} and {b.RingName} have no booked history.", ConsoleColor.DarkGray);
                WriteLine("  Book segments between them to build a feud properly.", ConsoleColor.DarkGray);
                Console.WriteLine();
            }

            if (!YesNo("Declare a feud by hand instead?")) return null;

            var intensities = new[] { FeudIntensity.Cold, FeudIntensity.Building, FeudIntensity.Hot, FeudIntensity.Nuclear };
            Console.WriteLine("\n  Feud intensity:");
            for (int i = 0; i < intensities.Length; i++)
            {
                Write($"  [{i + 1}] ", ConsoleColor.DarkGray);
                WriteLine(intensities[i].ToString(), ConsoleColor.White);
            }
            Console.Write("  Select (Enter = Building): ");
            var intensity = FeudIntensity.Building;
            if (int.TryParse(Console.ReadLine(), out int ic) && ic >= 1 && ic <= intensities.Length)
                intensity = intensities[ic - 1];

            var allTags = Enum.GetValues<FeudHistoryTag>().ToList();
            Console.WriteLine("\n  History tags (comma-separated numbers, 0 for none):");
            for (int i = 0; i < allTags.Count; i++)
            {
                Write($"  [{i + 1}] ", ConsoleColor.DarkGray);
                WriteLine(allTags[i].ToString(), ConsoleColor.White);
            }
            Console.Write("  Select: ");

            var history = (Console.ReadLine() ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out int n) && n >= 1 && n <= allTags.Count
                    ? (FeudHistoryTag?)allTags[n - 1] : null)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .ToList();

            // Declared feuds go into the book too, so later segments build on them.
            var feud = feudBook.GetOrCreate(a, b);
            feud.SetMinimumIntensity(intensity);
            foreach (var tag in history) feud.AddTag(tag);

            return feud;
        }

        // ── Structure selection ──────────────────────────────────────────────

        private static (List<MatchBeat> Beats, string Name) SelectStructure(Feud? feud)
        {
            var all = MatchStructureLibrary.All;

            Rule("MATCH STRUCTURE", 29);
            Console.WriteLine();

            for (int i = 0; i < all.Count; i++)
            {
                var s         = all[i];
                bool feudWarn = s.RequiresFeud && feud == null;

                Write($"  [{i + 1}] ", ConsoleColor.DarkGray);
                Write(Fit(s.Name, 22), feudWarn ? ConsoleColor.DarkGray : ConsoleColor.White);
                Write($"{s.Beats.Count} beats", ConsoleColor.DarkGray);
                if (feudWarn) Write("  ⚠ needs feud", ConsoleColor.DarkYellow);
                Console.WriteLine();
                WriteLine("       " + Truncate(s.Description, 66), ConsoleColor.DarkGray);
                Console.WriteLine();
            }

            Write($"  [{all.Count + 1}] ", ConsoleColor.DarkGray);
            Write("Build from scratch    ", ConsoleColor.White);
            WriteLine("2 beats — opening + finish pre-loaded", ConsoleColor.DarkGray);

            Console.Write("\n  Select structure: ");
            if (int.TryParse(Console.ReadLine(), out int c) && c >= 1 && c <= all.Count)
            {
                // Clone: the library's beats are shared singletons, so editing a plan
                // built from a preset would otherwise mutate the preset itself.
                return (all[c - 1].Beats.Select(b => b.Clone()).ToList(), all[c - 1].Name);
            }

            return (
            [
                BeatLibrary.Find("Standard Collar-and-Elbow")!.ToMatchBeat(BeatControl.Even),
                BeatLibrary.Find("Clean Victory")!.ToMatchBeat(BeatControl.WrestlerA),
            ], "Custom");
        }

        // ── Beat editor ──────────────────────────────────────────────────────

        private static void BeatEditor(List<MatchBeat> beats, Wrestler a, Wrestler b, Feud? feud)
        {
            while (true)
            {
                ConsoleUi.Clear();
                DrawHeader("BEAT EDITOR");
                Console.WriteLine();
                WriteLine($"  {a.RingName}  (A)     vs     {b.RingName}  (B)", ConsoleColor.Cyan);

                if (feud != null)
                    WriteLine($"  Feud: {feud.Intensity} ({feud.Heat:F0} heat)" +
                              (feud.History.Count > 0 ? $" — {string.Join(", ", feud.History)}" : ""),
                              ConsoleColor.DarkYellow);

                DisplayPlan(beats, a, b);

                int minutes = 2 + beats.Sum(x => x.DurationMinutes);
                WriteLine($"  Estimated runtime: ~{minutes} min", ConsoleColor.DarkGray);

                Write("\n  [A]dd   [R]emove   [C]hange control   [I]ntensity   [G]o", ConsoleColor.Cyan);
                Console.Write("\n\n  > ");

                switch ((Console.ReadLine() ?? "").Trim().ToUpperInvariant())
                {
                    case "A": AddBeat(beats, a, b, feud);   break;
                    case "R": RemoveBeat(beats, a, b);      break;
                    case "C": ChangeControl(beats, a, b);   break;
                    case "I": ChangeIntensity(beats, a, b); break;
                    case "G": return;
                }
            }
        }

        private static void AddBeat(List<MatchBeat> beats, Wrestler a, Wrestler b, Feud? feud)
        {
            Rule("ADD A BEAT", 34);

            var available = BeatLibrary.Available(feud).ToList();
            var indexed   = new List<BeatTemplate>();
            string? lastCat = null;
            int n = 1;

            foreach (var t in available)
            {
                if (t.Category != lastCat)
                {
                    WriteLine($"\n  {t.Category.ToUpperInvariant()}", ConsoleColor.Yellow);
                    lastCat = t.Category;
                }
                Write($"  [{n,2}] ", ConsoleColor.DarkGray);
                Write(Fit(t.Name, 28), ConsoleColor.White);
                WriteLine(Truncate(t.Description, 42), ConsoleColor.DarkGray);
                indexed.Add(t);
                n++;
            }

            Console.Write("\n  Select (0 to cancel): ");
            if (!int.TryParse(Console.ReadLine(), out int c) || c < 1 || c > indexed.Count)
                return;

            var template = indexed[c - 1];
            if (!string.IsNullOrWhiteSpace(template.BookerTip))
                WriteLine($"\n  ⓘ  {template.BookerTip}", ConsoleColor.DarkCyan);

            var control = SelectControl(a, b);

            // Insert before the last beat (keeps the finish at the end)
            int at = Math.Max(0, beats.Count - 1);
            beats.Insert(at, template.ToMatchBeat(control));
        }

        private static void RemoveBeat(List<MatchBeat> beats, Wrestler a, Wrestler b)
        {
            Rule("REMOVE A BEAT", 31);
            DisplayPlan(beats, a, b);
            Console.Write("  Beat number (0 to cancel): ");
            if (!int.TryParse(Console.ReadLine(), out int n) || n == 0) return;

            int idx = n - 1;
            if (idx < 0 || idx >= beats.Count)               { Warn("Invalid selection."); return; }
            if (beats[idx].IsOpening && beats.Count(x => x.IsOpening) <= 1) { Warn("Cannot remove the only opening beat."); return; }
            if (beats[idx].IsFinish  && beats.Count(x => x.IsFinish)  <= 1) { Warn("Cannot remove the only finish beat.");  return; }

            beats.RemoveAt(idx);
        }

        private static void ChangeControl(List<MatchBeat> beats, Wrestler a, Wrestler b)
        {
            Rule("CHANGE CONTROL", 30);
            DisplayPlan(beats, a, b);
            Console.Write("  Beat number (0 to cancel): ");
            if (!int.TryParse(Console.ReadLine(), out int n) || n == 0) return;

            int idx = n - 1;
            if (idx < 0 || idx >= beats.Count) { Warn("Invalid selection."); return; }

            beats[idx].Control = SelectControl(a, b);
        }

        /// <summary>
        /// Intensity and duration were previously fixed at the template defaults with
        /// no way to reach the overrides ToMatchBeat already supported.
        /// </summary>
        private static void ChangeIntensity(List<MatchBeat> beats, Wrestler a, Wrestler b)
        {
            Rule("INTENSITY / DURATION", 24);
            DisplayPlan(beats, a, b);
            Console.Write("  Beat number (0 to cancel): ");
            if (!int.TryParse(Console.ReadLine(), out int n) || n == 0) return;

            int idx = n - 1;
            if (idx < 0 || idx >= beats.Count) { Warn("Invalid selection."); return; }

            Rule("INTENSITY", 30);
            var intensity = ChooseEnum<BeatIntensity>("Intensity (0 = leave alone)");
            if (intensity.HasValue) beats[idx].Intensity = intensity.Value;

            Rule("DURATION", 30);
            var duration = ChooseEnum<BeatDuration>("Duration (0 = leave alone)");
            if (duration.HasValue) beats[idx].Duration = duration.Value;
        }

        private static BeatControl SelectControl(Wrestler? a = null, Wrestler? b = null)
        {
            Console.WriteLine("\n  Control:");
            Write("  [1] ", ConsoleColor.DarkGray);
            WriteLine(a != null ? $"WrestlerA — {a.RingName}" : "WrestlerA", ConsoleColor.White);
            Write("  [2] ", ConsoleColor.DarkGray);
            WriteLine(b != null ? $"WrestlerB — {b.RingName}" : "WrestlerB", ConsoleColor.White);
            WriteLine("  [3] Even", ConsoleColor.DarkGray);
            WriteLine("  [4] Contested (rapid back-and-forth)", ConsoleColor.DarkGray);
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
            WriteLine($"  {"#",3}  {"TYPE",-typeW}  {"CONTROL",-18}  INTENSITY", ConsoleColor.DarkGray);
            WriteLine("  " + new string('─', 70), ConsoleColor.DarkGray);

            for (int i = 0; i < beats.Count; i++)
            {
                var beat = beats[i];
                Write($"  {i + 1,3}  ", ConsoleColor.DarkGray);

                var colour = beat.IsFinish  ? ConsoleColor.Yellow
                           : beat.IsOpening ? ConsoleColor.Cyan
                           : ConsoleColor.White;

                Write(Fit(BeatTypeName(beat.Type), typeW), colour);
                Write("  " + Fit(ControlLabel(beat.Control, a, b), 18), ConsoleColor.DarkGray);
                WriteLine($"  {beat.Intensity} / {beat.Duration}", ConsoleColor.DarkGray);
            }

            WriteLine("  " + new string('─', 70), ConsoleColor.DarkGray);
        }

        // ── Results display ──────────────────────────────────────────────────

        public static void DisplayResults(MatchEngineResult result, Wrestler a, Wrestler b)
        {
            ConsoleUi.Clear();
            DrawHeader("MATCH RESULT");

            Console.WriteLine();
            WriteLine($"  {a.RingName}  vs  {b.RingName}", ConsoleColor.White);
            Console.WriteLine();

            WriteLine($"  WINNER        {result.Winner.RingName}", ConsoleColor.Yellow);
            WriteLine($"  RATING        {result.StarDisplay}", ConsoleColor.White);
            Console.WriteLine();

            WriteLine($"  Technical     {result.Bar(result.TechnicalScore,    60)}  {result.TechnicalScore:F0} / 60", ConsoleColor.DarkGray);
            WriteLine($"  Storytelling  {result.Bar(result.StorytellingScore, 80)}  {result.StorytellingScore:F0} / 80", ConsoleColor.DarkGray);
            WriteLine($"  Crowd         {result.Bar(result.CrowdAverageEnergy)}  {result.CrowdAverageEnergy:F0} / 100", ConsoleColor.DarkGray);

            Console.WriteLine();
            WriteLine("  ── PLAY BY PLAY " + new string('─', 40), ConsoleColor.DarkGray);
            Console.WriteLine();

            foreach (var line in result.PlayByPlay)
            {
                if (line.StartsWith('['))              WriteLine($"  {line}", ConsoleColor.Yellow);
                else if (line.StartsWith("  ▶"))       WriteLine($"  {line}", ConsoleColor.DarkGray);
                else if (!string.IsNullOrWhiteSpace(line)) WriteLine($"  {line}", ConsoleColor.White);
                else Console.WriteLine();
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
    }
}
