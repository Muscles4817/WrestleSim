using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Segment;
using static WrestlingSim.UI.ConsoleUi;

namespace WrestlingSim.UI
{
    /// <summary>
    /// Guided segment booking — the segment-side counterpart of MatchBookingFlow.
    /// Pick a template, cast it, then edit the action list beat by beat.
    /// </summary>
    public static class SegmentBookingFlow
    {
        // ── Entry points ─────────────────────────────────────────────────────

        /// <summary>Books a standalone segment, runs it, and banks the feud heat.</summary>
        public static void Run(List<Wrestler> roster, FeudBook feudBook)
        {
            ConsoleUi.Clear();
            DrawHeader("BOOK A SEGMENT");

            var segment = Build(roster);
            if (segment == null) return;

            var result = new SegmentSimulator().Simulate(segment);
            var updates = feudBook.RecordSegment(
                segment.Participants, result.HeatGenerated, result.HistoryTags);

            DisplayResult(result, updates);
            Pause("Press any key to return to the main menu...");
        }

        /// <summary>
        /// Builds a segment without running it, for placing on a show card.
        /// Returns null if the player backs out.
        /// </summary>
        public static Segment? Build(List<Wrestler> roster)
        {
            var template = SelectTemplate(roster.Count);

            Segment segment;
            if (template == null)
            {
                var scratch = BuildFromScratch(roster);
                if (scratch == null) return null;
                segment = scratch;
            }
            else
            {
                var cast = CastTemplate(template, roster);
                if (cast == null) return null;

                string dialogue = template.UsesDialogue
                    ? Ask("Dialogue (Enter for a default line)")
                    : "";

                segment = template.Create(cast, dialogue);
            }

            while (true)
            {
                ActionEditor(segment, roster);

                var errors = segment.Validate();
                if (errors.Count == 0) return segment;

                Console.WriteLine();
                foreach (var e in errors)
                    WriteLine($"  ✖  {e}", ConsoleColor.Red);
                Pause("Fix the segment and try again — press any key...");
            }
        }

        // ── Template selection ───────────────────────────────────────────────

        private static SegmentTemplate? SelectTemplate(int rosterSize)
        {
            Rule("SEGMENT TYPE", 40);
            Console.WriteLine();

            var available = SegmentTemplateLibrary.Bookable(rosterSize).ToList();
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
                Write($"{t.MinParticipants}{(t.AllowsExtraParticipants ? "+" : "")} people", ConsoleColor.DarkGray);
                Console.WriteLine();
                WriteLine("       " + Truncate(t.Description, 66), ConsoleColor.DarkGray);
                n++;
            }

            Console.WriteLine();
            Write($"  [{n,2}] ", ConsoleColor.DarkGray);
            Write(Fit("Build from scratch", 28), ConsoleColor.White);
            WriteLine("pick every action yourself", ConsoleColor.DarkGray);

            Console.Write("\n  Select: ");
            if (int.TryParse(Console.ReadLine(), out int c) && c >= 1 && c <= available.Count)
            {
                var chosen = available[c - 1];
                if (!string.IsNullOrWhiteSpace(chosen.BookerTip))
                    WriteLine($"\n  ⓘ  {chosen.BookerTip}", ConsoleColor.DarkCyan);
                return chosen;
            }

            return null; // build from scratch
        }

        private static List<Wrestler>? CastTemplate(SegmentTemplate template, List<Wrestler> roster)
        {
            var cast = new List<Wrestler>();

            foreach (var role in template.ParticipantRoles)
            {
                var pick = SelectWrestler(role, roster, cast);
                if (pick == null) return null;
                cast.Add(pick);
            }

            while (template.AllowsExtraParticipants && cast.Count < roster.Count)
            {
                if (!YesNo($"Add another {template.ExtraParticipantRole.ToLowerInvariant()}?")) break;

                var pick = SelectWrestler(template.ExtraParticipantRole, roster, cast);
                if (pick == null) break;
                cast.Add(pick);
            }

            return cast;
        }

        // ── From scratch ─────────────────────────────────────────────────────

        private static Segment? BuildFromScratch(List<Wrestler> roster)
        {
            Rule("CUSTOM SEGMENT", 40);

            string name = Ask("Segment name (Enter for a default)");

            Rule("FORMAT", 36);
            var type = ChooseEnum<SegmentType>("Format");
            if (type == null) return null;

            Rule("LOCATION", 36);
            var location = ChooseEnum<SegmentLocation>("Location");
            if (location == null) return null;

            bool scripted = YesNo("Scripted? (unscripted is rawer but can botch)", defaultYes: true);

            var segment = new Segment(
                string.IsNullOrWhiteSpace(name) ? $"{type} Segment" : name,
                type.Value, location.Value, scripted);

            // A segment needs a cast before it can have actions.
            while (true)
            {
                var pick = SelectWrestler("Participant", roster, segment.Participants);
                if (pick == null) break;
                segment.AddParticipant(pick);
                if (!YesNo("Add another participant?")) break;
            }

            if (segment.Participants.Count == 0)
            {
                Warn("A segment needs at least one participant.");
                return null;
            }

            return segment;
        }

        // ── Action editor ────────────────────────────────────────────────────

        private static void ActionEditor(Segment segment, List<Wrestler> roster)
        {
            while (true)
            {
                ConsoleUi.Clear();
                DrawHeader("SEGMENT EDITOR");

                WriteLine($"\n  {segment.Name}", ConsoleColor.Cyan);
                WriteLine($"  {segment.Type}  •  {segment.Location}  •  " +
                          $"{(segment.IsScripted ? "Scripted" : "Unscripted")}  •  ~{segment.DurationMinutes} min",
                          ConsoleColor.DarkGray);
                WriteLine("  Cast: " + string.Join(", ", segment.Participants.Select(p => p.RingName)),
                          ConsoleColor.DarkGray);

                if (segment.HistoryTags.Count > 0)
                    WriteLine("  Stamps: " + string.Join(", ", segment.HistoryTags), ConsoleColor.DarkYellow);

                DisplayActions(segment);

                Write("\n  [A]dd   [R]emove   [M]ove   [T]arget   [S]cripted   [P]articipant   [G]o", ConsoleColor.Cyan);
                Console.Write("\n\n  > ");

                switch ((Console.ReadLine() ?? "").Trim().ToUpperInvariant())
                {
                    case "A": AddAction(segment);              break;
                    case "R": RemoveAction(segment);           break;
                    case "M": MoveAction(segment);             break;
                    case "T": RetargetAction(segment);         break;
                    case "S": segment.IsScripted = !segment.IsScripted; break;
                    case "P": AddParticipant(segment, roster); break;
                    case "G": return;
                }
            }
        }

        private static void DisplayActions(Segment segment)
        {
            Console.WriteLine();
            WriteLine($"  {"#",3}  {"ACTION",-22}  {"PERFORMER",-18}  TARGET", ConsoleColor.DarkGray);
            WriteLine("  " + new string('─', 66), ConsoleColor.DarkGray);

            if (segment.Actions.Count == 0)
                WriteLine("       (no actions yet — press A to add one)", ConsoleColor.DarkGray);

            for (int i = 0; i < segment.Actions.Count; i++)
            {
                var a = segment.Actions[i];
                Write($"  {i + 1,3}  ", ConsoleColor.DarkGray);

                var colour = a.ActionType switch
                {
                    SegmentActionType.Betrayal => ConsoleColor.Magenta,
                    SegmentActionType.Attack   => ConsoleColor.Red,
                    SegmentActionType.RunIn    => ConsoleColor.Yellow,
                    _                          => ConsoleColor.White
                };

                Write(Fit(string.IsNullOrWhiteSpace(a.Label) ? a.ActionType.ToString() : a.Label, 22), colour);
                Write("  " + Fit(a.Performer?.RingName ?? "—", 18), ConsoleColor.White);
                WriteLine("  " + (a.Target?.RingName ?? "—"), ConsoleColor.DarkGray);
            }

            WriteLine("  " + new string('─', 66), ConsoleColor.DarkGray);
        }

        private static void AddAction(Segment segment)
        {
            Rule("ADD AN ACTION", 40);

            var all = SegmentActionLibrary.All;
            string? lastCat = null;
            int n = 1;

            foreach (var t in all)
            {
                if (t.Category != lastCat)
                {
                    WriteLine($"\n  {t.Category.ToUpperInvariant()}", ConsoleColor.Yellow);
                    lastCat = t.Category;
                }
                Write($"  [{n,2}] ", ConsoleColor.DarkGray);
                Write(Fit(t.Name, 24), ConsoleColor.White);
                WriteLine(Truncate(t.Description, 46), ConsoleColor.DarkGray);
                n++;
            }

            Console.Write("\n  Select (0 to cancel): ");
            if (!int.TryParse(Console.ReadLine(), out int c) || c < 1 || c > all.Count) return;

            var template = all[c - 1];
            if (!string.IsNullOrWhiteSpace(template.BookerTip))
                WriteLine($"\n  ⓘ  {template.BookerTip}", ConsoleColor.DarkCyan);

            var performer = SelectWrestler("Performer", segment.Participants, null);
            if (performer == null) return;

            Wrestler? target = null;
            if (template.RequiresTarget)
            {
                target = SelectWrestler("Target", segment.Participants, new List<Wrestler> { performer });
                if (target == null)
                {
                    Warn($"{template.Name} needs a target.");
                    return;
                }
            }

            string dialogue = template.ActionType is SegmentActionType.Talk or SegmentActionType.Interrupt
                ? Ask("Dialogue (optional)")
                : "";

            segment.AddAction(template.ToAction(performer, target, dialogue));

            if (template.HistoryTag.HasValue && !segment.HistoryTags.Contains(template.HistoryTag.Value))
                segment.HistoryTags.Add(template.HistoryTag.Value);
        }

        private static void RemoveAction(Segment segment)
        {
            int idx = PickActionIndex(segment, "Remove which action");
            if (idx < 0) return;
            segment.Actions.RemoveAt(idx);
        }

        private static void MoveAction(Segment segment)
        {
            int idx = PickActionIndex(segment, "Move which action");
            if (idx < 0) return;

            int to = AskInt($"Move to position (1–{segment.Actions.Count})", 0) - 1;
            if (to < 0 || to >= segment.Actions.Count) { Warn("Invalid position."); return; }

            var action = segment.Actions[idx];
            segment.Actions.RemoveAt(idx);
            segment.Actions.Insert(to, action);
        }

        private static void RetargetAction(Segment segment)
        {
            int idx = PickActionIndex(segment, "Retarget which action");
            if (idx < 0) return;

            var action = segment.Actions[idx];
            var target = SelectWrestler("New target", segment.Participants, new List<Wrestler> { action.Performer });
            if (target != null) action.Target = target;
        }

        private static void AddParticipant(Segment segment, List<Wrestler> roster)
        {
            var pick = SelectWrestler("Add participant", roster, segment.Participants);
            if (pick != null) segment.AddParticipant(pick);
        }

        private static int PickActionIndex(Segment segment, string prompt)
        {
            if (segment.Actions.Count == 0) { Warn("There are no actions yet."); return -1; }

            int n = AskInt($"{prompt} (0 to cancel)", 0);
            if (n < 1 || n > segment.Actions.Count) return -1;
            return n - 1;
        }

        // ── Shared pickers ───────────────────────────────────────────────────

        private static Wrestler? SelectWrestler(string label, IReadOnlyList<Wrestler> pool, IReadOnlyList<Wrestler>? exclude)
        {
            var choices = pool.Where(w => exclude == null || !exclude.Contains(w)).ToList();
            if (choices.Count == 0) { Warn("Nobody left to pick."); return null; }

            Rule(label.ToUpperInvariant(), 36);
            for (int i = 0; i < choices.Count; i++)
            {
                var w = choices[i];
                Write($"  [{i + 1,2}] ", ConsoleColor.DarkGray);
                Write(Fit(w.RingName, 22), ConsoleColor.White);
                WriteLine($"Pop {w.Overness,3}  Cha {w.Charisma:F1}", ConsoleColor.DarkGray);
            }

            Console.Write("\n  Select (0 to cancel): ");
            if (int.TryParse(Console.ReadLine(), out int c) && c >= 1 && c <= choices.Count)
                return choices[c - 1];

            return null;
        }

        // ── Result display ───────────────────────────────────────────────────

        public static void DisplayResult(SegmentResult result, IReadOnlyList<FeudUpdate> updates)
        {
            ConsoleUi.Clear();
            DrawHeader("SEGMENT RESULT");

            WriteLine($"\n  {result.SegmentName}", ConsoleColor.White);
            WriteLine($"  {result.Type}  •  {result.Location}", ConsoleColor.DarkGray);
            Console.WriteLine();

            WriteLine($"  Crowd reaction  {Bar(result.AudienceImpact, 10)}  {result.AudienceImpact:F1} / 10", ConsoleColor.DarkGray);
            WriteLine($"  Feud heat       {Bar(result.HeatGenerated, 15)}  +{result.HeatGenerated:F1}", ConsoleColor.DarkGray);

            if (result.Botched)
                WriteLine("\n  ✖  The segment botched.", ConsoleColor.Red);

            if (result.Injured != null)
                WriteLine($"  ✖  {result.Injured.RingName} was injured.", ConsoleColor.Red);

            if (result.OvernessChanges.Count > 0)
            {
                Console.WriteLine();
                foreach (var change in result.OvernessChanges)
                    WriteLine($"  {change.Wrestler.RingName} popularity {change.Delta:+0;-0} " +
                              $"(now {change.Wrestler.Overness})",
                              change.Delta >= 0 ? ConsoleColor.Green : ConsoleColor.Red);
            }

            WriteLine("\n  ── PLAY BY PLAY " + new string('─', 40), ConsoleColor.DarkGray);
            Console.WriteLine();
            foreach (var line in result.Commentary)
                WriteLine($"  {line}", ConsoleColor.White);

            DisplayFeudUpdates(updates);
        }

        public static void DisplayFeudUpdates(IReadOnlyList<FeudUpdate> updates)
        {
            var meaningful = updates.Where(u => u.HeatAdded > 0.05).ToList();
            if (meaningful.Count == 0) return;

            WriteLine("\n  ── FEUDS " + new string('─', 47), ConsoleColor.DarkGray);
            Console.WriteLine();

            foreach (var u in meaningful)
            {
                var f = u.Feud;
                WriteLine($"  {f.WrestlerA.RingName} vs {f.WrestlerB.RingName}  " +
                          $"+{u.HeatAdded:F1} heat  →  {u.LevelAfter} ({u.HeatAfter:F0})",
                          u.Escalated ? ConsoleColor.Yellow : ConsoleColor.DarkGray);

                if (u.Escalated)
                    WriteLine($"      ▲  Escalated from {u.PreviousLevel} to {u.LevelAfter}!", ConsoleColor.Yellow);

                if (u.NewTags.Count > 0)
                    WriteLine($"      +  {string.Join(", ", u.NewTags)}", ConsoleColor.DarkYellow);
            }
        }
    }
}
