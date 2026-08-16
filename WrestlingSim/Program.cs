using WrestlingSim.Engine;
using WrestlingSim.Models;
using WrestlingSim.UI;
using static WrestlingSim.UI.ConsoleUi;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        List<Wrestler> wrestlers = DataLoaders.LoadWrestlers("Wrestlers.json");

        if (wrestlers == null || wrestlers.Count < 2)
        {
            Console.WriteLine("Not enough wrestlers loaded.");
            return;
        }

        // One feud book for the session. Segments and matches both write into it,
        // and the match booker reads it back — this is what makes booking cumulative.
        var feudBook = new FeudBook();

        bool running = true;
        while (running)
        {
            MainMenu.Render();
            string choice = Console.ReadLine() ?? "";

            switch (choice.Trim())
            {
                case "1": MatchBookingFlow.Run(wrestlers, feudBook);   break;
                case "2": SegmentBookingFlow.Run(wrestlers, feudBook); break;
                case "3": ShowBookingFlow.Run(wrestlers, feudBook);    break;
                case "4": MainMenu.RenderWrestlers(wrestlers);         break;
                case "5": ViewFeuds(feudBook);                         break;
                case "6": running = false;                             break;
                default:
                    Console.WriteLine("\n  Invalid option. Press any key...");
                    ConsoleUi.AnyKey();
                    break;
            }
        }
    }

    // ─── Feud book ───────────────────────────────────────────────────────────────

    static void ViewFeuds(FeudBook feudBook)
    {
        ConsoleUi.Clear();
        DrawHeader("FEUDS");

        var feuds = feudBook.All;

        if (feuds.Count == 0)
        {
            WriteLine("\n  No feuds yet.", ConsoleColor.DarkGray);
            WriteLine("  Book segments between wrestlers to build one — a betrayal or a", ConsoleColor.DarkGray);
            WriteLine("  weapon shot generates the most heat.", ConsoleColor.DarkGray);
            Pause("Press any key to return to the main menu...");
            return;
        }

        Console.WriteLine();
        foreach (var f in feuds)
        {
            WriteLine($"  {f.WrestlerA.RingName} vs {f.WrestlerB.RingName}", ConsoleColor.White);
            WriteLine($"    {f.Intensity,-9} {Bar(f.Heat, 50)}  {f.Heat:F0} heat", ConsoleColor.Yellow);
            WriteLine($"    Matches   : {f.MatchCount}", ConsoleColor.DarkGray);
            WriteLine($"    History   : {(f.History.Count > 0 ? string.Join(", ", f.History) : "none")}",
                      ConsoleColor.DarkGray);

            if (f.HeatToNextTier is > 0 and var toNext)
                WriteLine($"    {toNext:F0} more heat to the next tier.", ConsoleColor.DarkGray);

            Console.WriteLine();
        }

        WriteLine("  Feud intensity raises starting crowd energy and unlocks beats:", ConsoleColor.DarkGray);
        WriteLine("    Building+  →  Feud Erupts, Outside Party", ConsoleColor.DarkGray);
        WriteLine("    Tags gate individual beats — ManagerConflict or FamilyInvolved", ConsoleColor.DarkGray);
        WriteLine("    unlock the Outside Party pull-in.", ConsoleColor.DarkGray);

        Pause("Press any key to return to the main menu...");
    }
}
