using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Enums;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Models
{
    public class Match
    {
        public Wrestler WrestlerA { get; set; }
        public Wrestler WrestlerB { get; set; }
        public MatchType Type { get; set; }
        public MatchLength Length { get; set; }
        public double PsychologyRating { get; set; }

            // Psychology, Storytelling, Move Variety/Execution, CrowdEngagement
            // Pacing/Structure, FinishQuality, Feud/Story, Chemistry, StarPower

        private static readonly Random rand = new Random();


        public Match(Wrestler wrestlerA, Wrestler wrestlerB, MatchType type = MatchType.Standard)
        {
            WrestlerA = wrestlerA;
            WrestlerB = wrestlerB;
            Type = type;
        }

        public double CalculateMatchRating()
        {
            // STANDARD
            // General In Ring Ability
            // Charisma
            // Overall In Ring Ability averaged between the two wrestlers. Charisma acting as a modifier to increase or decrease their score.
            // inring +/- 
            return Type switch
            {
                MatchType.Standard=> CalculateStandardRating(),
                MatchType.Technical => CalculateTechnicalRating(),
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
            double techRawA = WrestlerA.TechnicalMatchScore;
            double modifierA = GetCharismaModifier(WrestlerA.Charisma);
            double techScoreA = Math.Round(Clamp(techRawA + modifierA, 0.0, 5.0), 2);
            Console.WriteLine($"WRESTLER: {WrestlerA.RingName} | Tech: {techRawA} | CharismaMod: {modifierA} => {techScoreA}");

            double techRawB = WrestlerB.TechnicalMatchScore;
            double modifierB = GetCharismaModifier(WrestlerB.Charisma);
            double techScoreB = Math.Round(Clamp(techRawB + modifierB, 0.0, 5.0), 2);
            Console.WriteLine($"WRESTLER: {WrestlerB.RingName} | Tech: {techRawB} | CharismaMod: {modifierB} => {techScoreB}");

            return (techScoreA + techScoreB) / 2.0;
        }

        private double CalculateStandardRating()
        {
            double modifierA = GetCharismaModifier(WrestlerA.Charisma);
            double rawRatingA = WrestlerA.BaseMatchScore + modifierA;
            double wrestlerAScore = Math.Round(Clamp(rawRatingA, 0.0, 5.0), 1);
            Console.WriteLine($"WRESTLER: {WrestlerA.RingName}");
            Console.WriteLine($"Modifier: {modifierA}");
            Console.WriteLine($"RawRating: {WrestlerA.BaseMatchScore}");
            Console.WriteLine($"Score: {wrestlerAScore}");


            double modifierB = GetCharismaModifier(WrestlerB.Charisma);
            double rawRatingB = WrestlerB.BaseMatchScore + modifierB;
            double wrestlerBScore = Math.Round(Clamp(rawRatingB, 0.0, 5.0), 1);
            Console.WriteLine($"WRESTLER: {WrestlerB.RingName}");
            Console.WriteLine($"Modifier: {modifierB}");
            Console.WriteLine($"RawRating: {WrestlerB.BaseMatchScore}");
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
            int total = WrestlerA.Popularity + WrestlerB.Popularity;
            int roll = rand.Next(1, total + 1);

            // Return a tuple with winner and loser
            var ResultsTuple = (roll <= WrestlerA.Popularity ? (WrestlerA.RingName, WrestlerB.RingName) : (WrestlerB.RingName, WrestlerA.RingName));
            return ResultsTuple;
        }
    }
}
