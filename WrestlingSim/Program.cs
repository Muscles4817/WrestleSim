using System;
using System.IO;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Factories;
using WrestlingSim.Models;
using WrestlingSim.Models.Segment;
using MatchType = WrestlingSim.Enums.MatchType;

class Program
{
    static void Main(string[] args)
    {
        string fileName = "wrestlers.json"; // Adjust path if needed
        List<Wrestler> wrestlers = DataLoaders.LoadWrestlers(fileName);

        if (wrestlers == null || wrestlers.Count < 2)
        {
            Console.WriteLine("Not enough wrestlers loaded to run simulations.");
            return;
        }

        bool running = true;

        while (running)
        {
            Console.Clear();
            Console.WriteLine("=== WRESTLING SIMULATOR ===");
            Console.WriteLine("1. Simulate Match");
            Console.WriteLine("2. Simulate Segment");
            Console.WriteLine("3. Exit");
            Console.Write("\nChoose an option: ");

            string choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    SimulateMatch(wrestlers);
                    break;
                case "2":
                    SimulateSegment(wrestlers);
                    break;
                case "3":
                    running = false;
                    Console.WriteLine("Exiting...");
                    break;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }

            if (running)
            {
                Console.WriteLine("\nDo you want to return to the main menu or exit?");
                Console.WriteLine("1. Main Menu");
                Console.WriteLine("2. Exit");
                string nextAction = Console.ReadLine();
                if (nextAction == "2") running = false;
            }
        }
    }

    // ==========================
    // MATCH SIMULATION LOGIC
    // ==========================
    static void SimulateMatch(List<Wrestler> wrestlers)
    {
        Console.WriteLine("\nAvailable Wrestlers:");
        foreach (var w in wrestlers)
            Console.WriteLine($"- {w.RingName}");

        Wrestler wrestlerA = GetWrestlerByName("Enter the name of the FIRST wrestler", wrestlers);
        Wrestler wrestlerB = GetWrestlerByName("Enter the name of the SECOND wrestler", wrestlers);

        int simulations = GetSimulationCount();
        MatchType type = GetMatchStyle();

        Console.WriteLine($"\nSimulating {simulations} {type} match(es) between {wrestlerA.RingName} and {wrestlerB.RingName}...\n");

        var match = new Match(wrestlerA, wrestlerB, type);
        var sim = new MatchSimulator(match);

        for (int i = 1; i <= simulations; i++)
        {
            Console.WriteLine($"--- Simulation {i} ---");
            sim.Simulate();
        }
    }

    // ==========================
    // SEGMENT SIMULATION LOGIC
    // ==========================
    static void SimulateSegment(List<Wrestler> wrestlers)
    {
        Console.WriteLine("\nSegment Simulation Selected.");
        Console.WriteLine("Choose Segment Type:");
        Console.WriteLine("1. Promo");
        Console.WriteLine("2. Confrontation");
        Console.WriteLine("3. Surprise Return");
        Console.Write("\nYour choice: ");
        string choice = Console.ReadLine();

        Wrestler speaker = GetWrestlerByName("Select main wrestler", wrestlers);

        Segment segment = null;

        switch (choice)
        {
            case "1":
                Console.Write("Enter promo text: ");
                string promoText = Console.ReadLine();
                segment = SegmentFactory.CreatePromo(speaker, promoText);
                break;

            case "2":
                Wrestler interrupter = GetWrestlerByName("Select interrupter", wrestlers);
                Console.Write("Enter first dialogue: ");
                string d1 = Console.ReadLine();
                Console.Write("Enter interruption dialogue: ");
                string d2 = Console.ReadLine();
                segment = SegmentFactory.CreateConfrontation(speaker, interrupter, d1, d2);
                break;

            case "3":
                Wrestler victim = GetWrestlerByName("Select victim for surprise attack", wrestlers);
                segment = SegmentFactory.CreateSurpriseReturn(speaker, victim);
                break;

            default:
                Console.WriteLine("Invalid option. Returning to main menu...");
                return;
        }

        int simulations = GetSimulationCount();
        var sim = new SegmentSimulator();
        Console.WriteLine("\n--- Segment Simulation ---");

        for (int i = 1; i <= simulations; i++)
        {
            Console.WriteLine($"--- Simulation {i} ---");
            sim.SimulateSegment(segment);
        }
    }

    // ==========================
    // HELPERS
    // ==========================
    static Wrestler GetWrestlerByName(string prompt, List<Wrestler> wrestlers)
    {
        while (true)
        {
            Console.Write($"\n{prompt}: ");
            string name = Console.ReadLine();
            var wrestler = wrestlers.FirstOrDefault(w => w.RingName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (wrestler != null)
                return wrestler;

            Console.WriteLine("Not found. Picking random wrestler...");
            Random random = new Random();
            return wrestlers[random.Next(wrestlers.Count)];
        }
    }

    static int GetSimulationCount()
    {
        while (true)
        {
            Console.Write("\nHow many times do you want to simulate this? ");
            if (int.TryParse(Console.ReadLine(), out int count) && count > 0)
                return count;

            Console.WriteLine("Please enter a valid positive number.");
        }
    }

    static MatchType GetMatchStyle()
    {
        while (true)
        {
            Console.WriteLine("\nAvailable Match Styles:");
            foreach (var style in Enum.GetNames(typeof(MatchType)))
                Console.WriteLine($"- {style}");

            Console.Write("\nSelect match style: ");
            string input = Console.ReadLine();

            if (Enum.TryParse<MatchType>(input, true, out MatchType selectedType))
                return selectedType;

            Console.WriteLine("Invalid input. Defaulting to Standard.");
            return MatchType.Standard;
        }
    }
}
