using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Enums;

namespace WrestlingSim.Models
{
    public class RingSkills
    {
        public double HighFlyer {  get; set; }
        public double Grappler { get; set; }
        public double Powerhouse { get; set; }
        public double Technical {  get; set; }
        public double Brawler { get; set; }
        public double Striker { get; set; }

        public RingSkills() { }
        public RingSkills(double highFlyer, double grappler, double powerHouse, double technical, double brawler, double striker) {
            HighFlyer = highFlyer;
            Grappler = grappler;
            Powerhouse = powerHouse;
            Technical = technical;
            Brawler = brawler;
            Striker = striker;
        }

        public double GetOverallSkill()
        {
            return (HighFlyer + Grappler + Powerhouse + Technical + Brawler + Striker) / 6;
        }

        public double GetStyleProficiency(WrestlingStyle style)
        {
            return style switch
            {
                WrestlingStyle.Grappler => Grappler,
                WrestlingStyle.Brawler => Brawler,
                WrestlingStyle.Striker => Striker,
                WrestlingStyle.Powerhouse => Powerhouse,
                WrestlingStyle.Technical => Technical,
                WrestlingStyle.HighFlyer => HighFlyer,
                _ => GetOverallSkill()
            };
        }

        public double GetStandardScore(WrestlingStyle style)
        {
            // 1. Get top 2 from HighFlyer, Grappler, Powerhouse
            var firstGroup = new List<double> { HighFlyer, Grappler, Powerhouse };
            var topTwoPrimary = firstGroup.OrderByDescending(x => x).Take(2).ToList();

            // 2. Get higher of Brawler or Striker
            double secondary = Math.Max(Brawler, Striker);

            // 3. Base average of selected 3 and Technical
            double baseAverage = (topTwoPrimary[0] + topTwoPrimary[1] + secondary + Technical) / 4.0;

            // 4. Get the overall highest of all six
            double speciality = GetStyleProficiency(style);

            // 5. Apply slight weighting toward highest (e.g., +10% of max)
            double weightedScore = baseAverage * 0.7 + speciality * 0.3;

            return weightedScore;
        }

        public double GetTechnicalScore()
        {
            // 1. Emphasize Technical and Grappler skill
            double core = (Technical * 0.5) + (Grappler * 0.3);

            // 2. Minor boost from Striker for realism
            double secondary = Striker * 0.1;

            // 3. Deduct slightly for reliance on Brawling (anti-technical)
            double penalty = Brawler * 0.1;

            // 4. Total score
            double rawScore = core + secondary - penalty;

            // 5. Clamp and round
            return Math.Round(Clamp(rawScore, 0.0, 5.0), 2);
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

    }
}
