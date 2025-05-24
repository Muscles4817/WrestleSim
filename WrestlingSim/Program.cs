using System;
using System.IO;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using MatchType = WrestlingSim.Enums.MatchType;

class Program
{
    static void Main(string[] args)
    {
        string fileName = "wrestlers.json"; // Adjust path if needed
        List<Wrestler> wrestlers = DataLoaders.LoadWrestlers(fileName);

        if (wrestlers == null || wrestlers.Count < 2)
        {
            Console.WriteLine("Not enough wrestlers loaded to simulate a match.");
            return;
        }

        Console.WriteLine("Available Wrestlers:");
        foreach (var w in wrestlers)
        {
            Console.WriteLine($"- {w.RingName}");
        }

        Wrestler GetWrestlerByName(string prompt)
        {
            while (true)
            {
                Console.Write($"\n{prompt}: ");
                string name = Console.ReadLine();
                var wrestler = wrestlers.FirstOrDefault(w => w.RingName.Equals(name, StringComparison.OrdinalIgnoreCase));
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

                return MatchType.Standard; // defaults to Standard match
                
                // Console.WriteLine("Invalid match style. Please enter 'Standard' or 'Technical'.\n");
            }
        }

        void TestNameChanger(Wrestler wrestler)
        {
            // Function to test chanign a wrestlers name - Can be deleted 
            Console.WriteLine("Changing the name of the wrestler");
            Console.WriteLine($"{wrestler.RingName}'s previous names are:");
            // Print previous names - Probably could logic this to print "Wrester has no prev names" if list is empty
            foreach (var p in wrestler.PreviousNames)
            {
                Console.WriteLine($"- {p}");
            }

            while (true)
            {
                Console.WriteLine("What would you like to change their name to? ");
                string? newName = Console.ReadLine();
                // Not sure how to check for blank string? Thing to look up
                if (newName != null && newName != "")
                {
                    wrestler.ChangeName(newName);
                    break;
                }
            }

            Console.WriteLine($"Name changed to {wrestler.RingName}.");
            Console.WriteLine($"{wrestler.RingName}'s previous names are:");
            foreach (var q in wrestler.PreviousNames)
            {
                Console.WriteLine($"- {q}");
            }

        }

        Console.WriteLine();

        var wrestlerA = GetWrestlerByName("Enter the name of the FIRST wrestler");
        var wrestlerB = GetWrestlerByName("Enter the name of the SECOND wrestler");

        int simulations = GetSimulationCount();
        MatchType type = GetMatchStyle();

        TestNameChanger(wrestlerA);

        Console.WriteLine($"\nSimulating {simulations} {type} match(es) between {wrestlerA.RingName} and {wrestlerB.RingName}...\n");

        Console.WriteLine($"Wrestler {wrestlerA.RingName}'s real name is {wrestlerA.RealName}");

        var match = new Match(wrestlerA, wrestlerB, type);
        var sim = new MatchSimulator(match);

        for (int i = 1; i <= simulations; i++)
        {
            Console.WriteLine($"--- Simulation {i} ---");
            sim.Simulate();
        }
    }
}
