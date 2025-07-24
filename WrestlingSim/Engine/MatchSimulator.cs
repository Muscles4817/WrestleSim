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
        private readonly Match _match;
        private static readonly Random rand = new Random();

        public MatchSimulator(Match match)
        {
            _match = match;
        }

        public void Simulate()
        {
            Console.WriteLine("");
            Console.WriteLine("WELCOME TO A NEW MATCH");
            Console.WriteLine($"Match: {_match.WrestlerA.RingName} vs {_match.WrestlerB.RingName}");

            double matchRating = CalculateMatchRating();
            var result = CalculateWinner();

            // Load all moves from Json - This will need to be changed to get wrestlers specific moves
            var fileName = "MoveList.json";
            List<Move> moves = DataLoaders.LoadMoves(fileName);

            // Get random move from list
            Random random = new Random();
            var LastMove = moves[random.Next(moves.Count)];

            Console.WriteLine();

            Console.WriteLine($"MATCH RATING: {matchRating}");
            Console.WriteLine($"{result.Item1} used {LastMove.Name} to beat {result.Item2}");
            Console.WriteLine($"WINNER: {result.Item1}");
            Console.WriteLine("");
        }

        public double CalculateMatchRating()
        {
            // STANDARD
            // General In Ring Ability
            // Charisma
            // Overall In Ring Ability averaged between the two wrestlers. Charisma acting as a modifier to increase or decrease their score.
            // inring +/- 
            return _match.Type switch
            {
                Enums.MatchType.Standard => CalculateStandardRating(),
                Enums.MatchType.Technical => CalculateTechnicalRating(),
                _ => CalculateStandardRating()
            };

            //switch(Type)
            //{
            //    case(MatchType.Standard):
            //        return CalculateStandardRating();
            //}



            // SPOTFEST
            // High Risk Moves (Moveset[Impact], InRingAbility)
            // Minimal Downtime (Stamina/Fitness?)
            // Minimal Selling (-Selling, -Storytelling)
            // Fast Paced 
            // InRingAbility (High Flyer, Powerhouse, Grappling, Brawlers)

            // TECHNICAL
            // Grappling Skill (Grappling, Technical, Psychology, Submission)

            // STORYTELLING
            // Selling (Selling, Storytelling, Psychology, Charisma, Popularity)

            //return (WrestlerA.InRingAbility + WrestlerB.InRingAbility)/2;
        }

        private double CalculateTechnicalRating()
        {
            double techRawA = _match.WrestlerA.TechnicalMatchScore;
            double modifierA = GetCharismaModifier(_match.WrestlerA.Charisma);
            double techScoreA = Math.Round(Clamp(techRawA + modifierA, 0.0, 5.0), 2);
            Console.WriteLine($"WRESTLER: {_match.WrestlerA.RingName} | Tech: {techRawA} | CharismaMod: {modifierA} => {techScoreA}");

            double techRawB = _match.WrestlerB.TechnicalMatchScore;
            double modifierB = GetCharismaModifier(_match.WrestlerB.Charisma);
            double techScoreB = Math.Round(Clamp(techRawB + modifierB, 0.0, 5.0), 2);
            Console.WriteLine($"WRESTLER: {_match.WrestlerB.RingName} | Tech: {techRawB} | CharismaMod: {modifierB} => {techScoreB}");

            return (techScoreA + techScoreB) / 2.0;
        }

        private double CalculateStandardRating()
        {
            double modifierA = GetCharismaModifier(_match.WrestlerA.Charisma);
            double rawRatingA = _match.WrestlerA.BaseMatchScore + modifierA;
            double wrestlerAScore = Math.Round(Clamp(rawRatingA, 0.0, 5.0), 1);
            Console.WriteLine($"WRESTLER: {_match.WrestlerA.RingName}");
            Console.WriteLine($"Modifier: {modifierA}");
            Console.WriteLine($"RawRating: {_match.WrestlerA.BaseMatchScore}");
            Console.WriteLine($"Score: {wrestlerAScore}");


            double modifierB = GetCharismaModifier(_match.WrestlerB.Charisma);
            double rawRatingB = _match.WrestlerB.BaseMatchScore + modifierB;
            double wrestlerBScore = Math.Round(Clamp(rawRatingB, 0.0, 5.0), 1);
            Console.WriteLine($"WRESTLER: {_match.WrestlerB.RingName}");
            Console.WriteLine($"Modifier: {modifierB}");
            Console.WriteLine($"RawRating: {_match.WrestlerB.BaseMatchScore}");
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


        public (string, string) CalculateWinner()
        {
            int total = _match.WrestlerA.Popularity + _match.WrestlerB.Popularity;
            int roll = rand.Next(1, total + 1);

            // Return a tuple with winner and loser
            var ResultsTuple = (roll <= _match.WrestlerA.Popularity ? (_match.WrestlerA.RingName, _match.WrestlerB.RingName) : (_match.WrestlerB.RingName, _match.WrestlerA.RingName));
            return ResultsTuple;
        }
    }
}
