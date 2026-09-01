using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Person;

namespace WrestlingSim.Tests
{
    /// <summary>Lightweight wrestler factory so the segment and show tests stay readable.</summary>
    internal static class TestRoster
    {
        public static Wrestler Make(
            string name,
            double overness = 70,
            double charisma = 3.0,
            int psychology = 80,
            int toughness = 80,
            int strength = 70,
            double skill = 3.0)
        {
            var gimmick = new Gimmick(name)
            {
                NaturalAlignment = Alignment.Face,
                AppealRatings    = new List<FanGroupAppeal>
                {
                    new() { Group = "Casual",   AppealScore = 0.7 },
                    new() { Group = "Hardcore", AppealScore = 0.7 }
                }
            };

            return new Wrestler(
                realName   : name,
                gimmick    : gimmick,
                overness: overness,
                ringSkills : new RingSkills(skill, skill, skill, skill, skill, skill),
                charisma   : charisma,
                style      : WrestlingStyle.Technical)
            {
                Mental   = new MentalAttributes { Psychology = psychology, Selling = 80, RingIQ = 80, Toughness = toughness },
                Physical = new PhysicalAttributes { Strength = strength }
            };
        }

        public static Wrestler Babyface => Make("Babyface Bill", overness: 80, charisma: 4.0);
        public static Wrestler Heel     => Make("Heel Harry",    overness: 75, charisma: 3.5);
    }
}
