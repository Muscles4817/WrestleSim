using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Models;
using WrestlingSim.Enums;

namespace WrestlingSim.Engine
{
    public class MatchSimulator
    {
        private static readonly Random rand = new Random();

        public MatchSimulator() { }

        public void Simulate(Match match)
        {
            Console.WriteLine("");
            Console.WriteLine("WELCOME TO A NEW MATCH");
            Console.WriteLine($"Match: {match.WrestlerA.RingName} vs {match.WrestlerB.RingName}");

            double matchRating = CalculateMatchRating(match);
            var result = CalculateWinner(match);

            // Load all moves from Json - This will need to be changed to get wrestlers specific moves
            var fileName = "MoveList.json";
            List<Move> moves = DataLoaders.LoadMoves(fileName);

            // Get random move from list
            var LastMove = moves[rand.Next(moves.Count)];

            Console.WriteLine();

            Console.WriteLine($"MATCH RATING: {matchRating}");
            Console.WriteLine($"{result.Item1} used {LastMove.Name} to beat {result.Item2}");
            Console.WriteLine($"WINNER: {result.Item1}");
            Console.WriteLine("");
        }

        public double CalculateMatchRating(Match match)
        {
            return match.Type switch
            {
                Enums.MatchType.Standard => CalculateStandardRating(match),
                Enums.MatchType.Technical => CalculateTechnicalRating(match),
                _ => CalculateStandardRating(match)
            };
        }

        private double CalculateTechnicalRating(Match match)
        {
            double techRawA = match.WrestlerA.TechnicalMatchScore;
            double modifierA = GetCharismaModifier(match.WrestlerA.Charisma);
            double techScoreA = Math.Round(Clamp(techRawA + modifierA, 0.0, 5.0), 2);
            Console.WriteLine($"WRESTLER: {match.WrestlerA.RingName} | Tech: {techRawA} | CharismaMod: {modifierA} => {techScoreA}");

            double techRawB = match.WrestlerB.TechnicalMatchScore;
            double modifierB = GetCharismaModifier(match.WrestlerB.Charisma);
            double techScoreB = Math.Round(Clamp(techRawB + modifierB, 0.0, 5.0), 2);
            Console.WriteLine($"WRESTLER: {match.WrestlerB.RingName} | Tech: {techRawB} | CharismaMod: {modifierB} => {techScoreB}");

            return (techScoreA + techScoreB) / 2.0;
        }

        private double CalculateStandardRating(Match match)
        {
            double modifierA = GetCharismaModifier(match.WrestlerA.Charisma);
            double rawRatingA = match.WrestlerA.BaseMatchScore + modifierA;
            double wrestlerAScore = Math.Round(Clamp(rawRatingA, 0.0, 5.0), 1);
            Console.WriteLine($"WRESTLER: {match.WrestlerA.RingName}");
            Console.WriteLine($"Modifier: {modifierA}");
            Console.WriteLine($"RawRating: {match.WrestlerA.BaseMatchScore}");
            Console.WriteLine($"Score: {wrestlerAScore}");

            double modifierB = GetCharismaModifier(match.WrestlerB.Charisma);
            double rawRatingB = match.WrestlerB.BaseMatchScore + modifierB;
            double wrestlerBScore = Math.Round(Clamp(rawRatingB, 0.0, 5.0), 1);
            Console.WriteLine($"WRESTLER: {match.WrestlerB.RingName}");
            Console.WriteLine($"Modifier: {modifierB}");
            Console.WriteLine($"RawRating: {match.WrestlerB.BaseMatchScore}");
            Console.WriteLine($"Score: {wrestlerBScore}");

            return (wrestlerBScore + wrestlerAScore) / 2;
        }

        private double GetCharismaModifier(double charisma)
        {
            if (charisma < 2.0)
            {
                // Low charisma: more likely to negatively impact match
                double maxNeg = -1.0;
                double maxPos = 0.1 + (charisma / 2.0) * 0.1; // up to ~0.5 at CHA=2.0
                return RandomDouble(maxNeg, maxPos);
            }
            else if (charisma > 4.0)
            {
                // High charisma: more likely to boost match
                double maxNeg = -0.1 - ((5.0 - charisma) * 0.1); // down to ~-0.5 at CHA=4.0
                double maxPos = 1.0;
                return RandomDouble(maxNeg, maxPos);
            }
            else
            {
                // Neutral charisma: small, balanced range
                return RandomDouble(-0.3, 0.3);
            }
        }

        private static double RandomDouble(double min, double max)
        {
            return min + (rand.NextDouble() * (max - min));
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }


        public (string, string) CalculateWinner(Match match)
        {
            int total = match.WrestlerA.Popularity + match.WrestlerB.Popularity;
            int roll = rand.Next(1, total + 1);

            return roll <= match.WrestlerA.Popularity
                ? (match.WrestlerA.RingName, match.WrestlerB.RingName)
                : (match.WrestlerB.RingName, match.WrestlerA.RingName);
        }
    }
}
