using WrestlingSim.Engine;
using WrestlingSim.Models;
using static WrestlingSim.UI.ConsoleUi;

namespace WrestlingSim.UI
{
    /// <summary>
    /// Card assembly. Matches and segments go on the same card in any order, spending
    /// from a shared runtime budget, and everything on it feeds the same FeudBook.
    /// </summary>
    public static class ShowBookingFlow
    {
        public static void Run(List<Wrestler> roster, FeudBook feudBook)
        {
            ConsoleUi.Clear();
            DrawHeader("BOOK A SHOW");

            var show = new Show
            {
                Name                 = Fallback(Ask("Show name"), "Unnamed Show"),
                Location             = Fallback(Ask("Location"), "Unknown Arena"),
                Date                 = DateTime.Now,
                TotalDurationMinutes = Math.Clamp(AskInt("Runtime in minutes (Enter = 180)", 180), 20, 480)
            };

            if (!CardEditor(show, roster, feudBook)) return;

            if (show.Card.Count == 0)
            {
                Warn("An empty card cannot be run.");
                return;
            }

            var result = new ShowSimulator(feudBook).Simulate(show);
            DisplayResults(show, result);
            Pause("Press any key to return to the main menu...");
        }

        // ── Card editor ──────────────────────────────────────────────────────

        /// <summary>Returns false if the player abandons the show.</summary>
        private static bool CardEditor(Show show, List<Wrestler> roster, FeudBook feudBook)
        {
            while (true)
            {
                ConsoleUi.Clear();
                DrawHeader("CARD SHEET");

                WriteLine($"\n  {show.Name}  —  {show.Location}", ConsoleColor.Cyan);
                DisplayCard(show);
                DisplayBudget(show);
                DisplayHotFeuds(feudBook);

                Write("\n  [M]atch   [S]egment   [U]p   [D]own   [R]emove   [G]o   e[X]it", ConsoleColor.Cyan);
                Console.Write("\n\n  > ");

                switch ((Console.ReadLine() ?? "").Trim().ToUpperInvariant())
                {
                    case "M":
                        var match = MatchBookingFlow.BuildMatch(roster, feudBook);
                        if (match != null) show.Card.Add(match);
                        break;

                    case "S":
                        var segment = SegmentBookingFlow.Build(roster);
                        if (segment != null) show.Card.Add(segment);
                        break;

                    case "U": Move(show, -1); break;
                    case "D": Move(show, +1); break;
                    case "R": Remove(show);   break;

                    case "G":
                        if (show.IsOverrunning &&
                            !YesNo($"The card runs {show.BookedMinutes - show.TotalDurationMinutes} min long " +
                                   "and will be penalised. Run it anyway?"))
                            break;
                        return true;

                    case "X":
                        if (YesNo("Abandon this show?")) return false;
                        break;
                }
            }
        }

        private static void DisplayCard(Show show)
        {
            Console.WriteLine();
            WriteLine($"  {"#",3}  {"ITEM",-40}  {"KIND",-8}  MIN", ConsoleColor.DarkGray);
            WriteLine("  " + new string('─', 62), ConsoleColor.DarkGray);

            if (show.Card.Count == 0)
                WriteLine("       (nothing booked yet — press M or S)", ConsoleColor.DarkGray);

            for (int i = 0; i < show.Card.Count; i++)
            {
                var item = show.Card[i];
                Write($"  {i + 1,3}  ", ConsoleColor.DarkGray);

                var colour = item.Kind == CardItemKind.Match ? ConsoleColor.White : ConsoleColor.Magenta;
                Write(Fit(item.Name, 40), colour);

                // Flag the rule the player is most likely to trip over.
                bool fatigued = i > 0 && show.Card[i - 1].Kind == item.Kind;
                Write("  " + Fit(item.Kind.ToString(), 8), fatigued ? ConsoleColor.DarkYellow : ConsoleColor.DarkGray);
                WriteLine($"{item.DurationMinutes,4}", ConsoleColor.DarkGray);
            }

            WriteLine("  " + new string('─', 62), ConsoleColor.DarkGray);

            if (show.Card.Count > 1)
            {
                WriteLine("  Position: #1 is the opener (×1.2), the last slot is the main event (×1.5).", ConsoleColor.DarkGray);
                WriteLine("  Two of the same kind back to back take a ×0.85 fatigue penalty (shown amber).", ConsoleColor.DarkGray);
            }
        }

        private static void DisplayBudget(Show show)
        {
            Console.WriteLine();
            var colour = show.IsOverrunning ? ConsoleColor.Red
                       : show.RemainingMinutes < 20 ? ConsoleColor.DarkYellow
                       : ConsoleColor.DarkGray;

            WriteLine($"  Runtime  {Bar(show.BookedMinutes, show.TotalDurationMinutes)}  " +
                      $"{show.BookedMinutes} / {show.TotalDurationMinutes} min" +
                      (show.IsOverrunning
                          ? $"   OVER by {show.BookedMinutes - show.TotalDurationMinutes}"
                          : $"   {show.RemainingMinutes} left"),
                      colour);
        }

        private static void DisplayHotFeuds(FeudBook feudBook)
        {
            var feuds = feudBook.All.Take(4).ToList();
            if (feuds.Count == 0) return;

            Console.WriteLine();
            WriteLine("  Live feuds:", ConsoleColor.DarkGray);
            foreach (var f in feuds)
                WriteLine($"    {f.WrestlerA.RingName} vs {f.WrestlerB.RingName}  —  {f.Intensity} ({f.Heat:F0})",
                          ConsoleColor.DarkYellow);
        }

        private static void Move(Show show, int direction)
        {
            int n = AskInt("Move which item (0 to cancel)", 0);
            int idx = n - 1;
            if (idx < 0 || idx >= show.Card.Count) return;

            int to = idx + direction;
            if (to < 0 || to >= show.Card.Count) { Warn("Already at the end of the card."); return; }

            (show.Card[idx], show.Card[to]) = (show.Card[to], show.Card[idx]);
        }

        private static void Remove(Show show)
        {
            int n = AskInt("Remove which item (0 to cancel)", 0);
            int idx = n - 1;
            if (idx < 0 || idx >= show.Card.Count) return;
            show.Card.RemoveAt(idx);
        }

        // ── Results ──────────────────────────────────────────────────────────

        private static void DisplayResults(Show show, ShowResult result)
        {
            ConsoleUi.Clear();
            DrawHeader("SHOW RESULT");

            WriteLine($"\n  {show.Name}  —  {show.Location}", ConsoleColor.White);
            Console.WriteLine();

            WriteLine($"  OVERALL       {Bar(result.OverallRating, 100)}  {result.OverallRating:F1} / 100",
                      ConsoleColor.Yellow);
            WriteLine($"  Runtime       {result.BookedMinutes} / {result.BudgetMinutes} min", ConsoleColor.DarkGray);

            if (result.OverrunPenalty > 0)
                WriteLine($"  ✖  Ran long — overall cut by {result.OverrunPenalty * 100:F0}%.", ConsoleColor.Red);

            WriteLine($"  Crowd at the final bell: {result.FinalCrowdMood:F1} / 10", ConsoleColor.DarkGray);

            Console.WriteLine();
            WriteLine("  ── CARD " + new string('─', 48), ConsoleColor.DarkGray);
            Console.WriteLine();

            foreach (var item in result.Items)
            {
                var colour = item.Kind == CardItemKind.Match ? ConsoleColor.White : ConsoleColor.Magenta;
                Write("  " + Fit(item.Label, 34), colour);
                Write($"{item.Score,6:F1}", ConsoleColor.Yellow);

                if (item.StarRating.HasValue)
                    Write($"   {item.StarRating.Value:F2}★", ConsoleColor.DarkYellow);

                Console.WriteLine();

                foreach (var note in item.Notes)
                    WriteLine($"       {note}", ConsoleColor.DarkGray);
            }

            SegmentBookingFlow.DisplayFeudUpdates(result.FeudUpdates);
        }

        private static string Fallback(string value, string standIn) =>
            string.IsNullOrWhiteSpace(value) ? standIn : value;
    }
}
