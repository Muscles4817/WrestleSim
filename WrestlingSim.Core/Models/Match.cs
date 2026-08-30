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
    }
}
