using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Factories;
using WrestlingSim.Models;
using WrestlingSim.Models.Segment;
using WrestlingSim.UI;
using MatchType = WrestlingSim.Enums.MatchType;

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

        bool running = true;
        while (running)
        {
            MainMenu.Render();
            string choice = Console.ReadLine();

            switch (choice)
            {
                case "1": BookMatch(wrestlers);       break;
                case "2": BookShow(wrestlers);        break;
                case "3": MainMenu.RenderWrestlers(wrestlers); break;
                case "4": running = false;            break;
                default:
                    Console.WriteLine("\n  Invalid option. Press any key...");
                    Console.ReadKey(true);
                    break;
            }
        }
    }

    // ─── Book a Match ────────────────────────────────────────────────────────────

    static void BookMatch(List<Wrestler> wrestlers)
    {
        Console.Clear();
        Console.WriteLine("\n  === BOOK A MATCH ===\n");
        PrintRoster(wrestlers);

        Wrestler a = GetWrestlerByName("First wrestler", wrestlers);
        Wrestler b = GetWrestlerByName("Second wrestler", wrestlers);
        int count  = GetCount("How many simulations?");
        MatchType type = GetMatchStyle();

        Console.WriteLine($"\n  {a.RingName} vs {b.RingName}  |  {type}  |  {count} sim(s)\n");

        var match = new Match(a, b, type);
        var sim   = new MatchSimulator();

        for (int i = 1; i <= count; i++)
        {
            Console.WriteLine($"  --- Simulation {i} ---");
            sim.Simulate(match);
        }

        Pause();
    }

    // ─── Book a Show ─────────────────────────────────────────────────────────────

    static void BookShow(List<Wrestler> wrestlers)
    {
        Console.Clear();
        Console.WriteLine("\n  === BOOK A SHOW ===\n");

        Console.Write("  Show name : ");
        string showName = Console.ReadLine() ?? "Unnamed Show";
        Console.Write("  Location  : ");
        string location = Console.ReadLine() ?? "Unknown Arena";

        var show = new Show
        {
            Name         = showName,
            Date         = DateTime.Now,
            Location     = location,
            AudienceSize = 10000
        };

        PrintRoster(wrestlers);

        int matchCount = GetCount("\n  How many matches?");
        for (int i = 0; i < matchCount; i++)
        {
            Console.WriteLine($"\n  --- Match {i + 1} ---");
            Wrestler w1  = GetWrestlerByName("First wrestler", wrestlers);
            Wrestler w2  = GetWrestlerByName("Second wrestler", wrestlers);
            MatchType mt = GetMatchStyle();
            show.Card.Add(new Match(w1, w2, mt));
        }

        int segCount = GetCount("\n  How many segments?");
        for (int i = 0; i < segCount; i++)
        {
            Console.WriteLine($"\n  --- Segment {i + 1} ---");
            Wrestler main = GetWrestlerByName("Main wrestler", wrestlers);
            show.Card.Add(SegmentFactory.CreatePromo(main, $"Promo by {main.RingName}"));
        }

        var result = new ShowSimulator(new MatchSimulator(), new SegmentSimulator()).SimulateShow(show);

        Console.WriteLine($"\n  === Results: {show.Name} ===");
        Console.WriteLine($"  Overall Rating : {result.OverallRating:F1}");
        Console.WriteLine();
        foreach (var (label, score) in result.Breakdown)
            Console.WriteLine($"    {label,-12}  {score:F1}");

        Pause();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    static void PrintRoster(List<Wrestler> wrestlers)
    {
        Console.WriteLine("  Roster:");
        foreach (var w in wrestlers)
            Console.WriteLine($"    - {w.RingName}");
        Console.WriteLine();
    }

    static Wrestler GetWrestlerByName(string prompt, List<Wrestler> wrestlers)
    {
        while (true)
        {
            Console.Write($"  {prompt}: ");
            string input = Console.ReadLine() ?? "";
            var match = wrestlers.FirstOrDefault(
                w => w.RingName.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            Console.WriteLine("  Not found — picking randomly.");
            return wrestlers[Random.Shared.Next(wrestlers.Count)];
        }
    }

    static int GetCount(string prompt)
    {
        while (true)
        {
            Console.Write($"{prompt}: ");
            if (int.TryParse(Console.ReadLine(), out int n) && n > 0) return n;
            Console.WriteLine("  Please enter a positive number.");
        }
    }

    static MatchType GetMatchStyle()
    {
        Console.WriteLine("\n  Match styles: " + string.Join(", ", Enum.GetNames<MatchType>()));
        Console.Write("  Select style : ");
        string input = Console.ReadLine() ?? "";
        if (Enum.TryParse<MatchType>(input, ignoreCase: true, out var t)) return t;
        Console.WriteLine("  Defaulting to Standard.");
        return MatchType.Standard;
    }

    static void Pause()
    {
        Console.WriteLine("\n  Press any key to return to the main menu...");
        Console.ReadKey(true);
    }
}
