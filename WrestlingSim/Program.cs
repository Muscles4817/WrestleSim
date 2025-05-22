using System.IO;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using MatchType = WrestlingSim.Enums.MatchType;

class Program
{
    static void Main(string[] args)
    {
        string filePath = "wrestlers.json"; // Adjust path if needed
        List<Wrestler> wrestlers = DataLoaders.LoadWrestlers(filePath);

        if (wrestlers == null || wrestlers.Count < 2)
        {
            Console.WriteLine("Not enough wrestlers loaded to simulate a match.");
            return;
        }

        Console.WriteLine("Available Wrestlers:");
        foreach (var w in wrestlers)
        {
            Console.WriteLine($"- {w.Name}");
        }

        Wrestler GetWrestlerByName(string prompt)
        {
            while (true)
            {
                Console.Write($"\n{prompt}: ");
                string name = Console.ReadLine();
                var wrestler = wrestlers.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (wrestler != null)
                    return wrestler;

                // Add random wrestler if none specified
                Random random = new Random();
                return wrestlers[random.Next(wrestlers.Count)];

            }
        }

        int GetSimulationCount()
        {
            while (true)
            {
                Console.Write("\nHow many times do you want to simulate the match? ");
                if (int.TryParse(Console.ReadLine(), out int count) && count > 0)
                    return count;

                Console.WriteLine("Please enter a valid positive number.");
            }
        }

        MatchType GetMatchStyle()
        {
            while (true)
            {
                Console.WriteLine("Available Match Styles:");
                Console.WriteLine($"- {MatchType.Standard}");
                Console.WriteLine($"- {MatchType.Technical.ToString()}");

                Console.Write("\nWhat match style do you want? ");
                string? style = Console.ReadLine();

                if (Enum.TryParse<MatchType>(style, true, out MatchType selectedType))
                {
                    return selectedType;
                }

                Console.WriteLine("Invalid match style. Please enter 'Standard' or 'Technical'.\n");
            }
        }


        var wrestlerA = GetWrestlerByName("Enter the name of the FIRST wrestler");
        var wrestlerB = GetWrestlerByName("Enter the name of the SECOND wrestler");

        int simulations = GetSimulationCount();
        MatchType type = GetMatchStyle();

        Console.WriteLine($"\nSimulating {simulations} match(es) between {wrestlerA.Name} and {wrestlerB.Name}...\n");

        var match = new Match(wrestlerA, wrestlerB, type);
        var sim = new MatchSimulator(match);

        for (int i = 1; i <= simulations; i++)
        {
            Console.WriteLine($"--- Simulation {i} ---");
            sim.Simulate();
        }
    }
}
