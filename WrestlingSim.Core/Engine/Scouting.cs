using WrestlingSim.Models;
using WrestlingSim.Models.Person;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// What the player can tell about where somebody is going (Part 2, decided 14–15 Nov 2026):
    /// potential and learning speed as words, never numbers, sharpening the longer somebody has
    /// worked for the player. Anybody else is read from what anyone could see — their age, and
    /// how they have changed on screen.
    /// </summary>
    public static class Scouting
    {
        public sealed record GroupReading(SkillGroup Group, string Name, int Level, int? Change, string Potential, string? Learns);

        public sealed record Reading(string Summary, string Knowledge, int? MonthsWithYou, IReadOnlyList<GroupReading> Groups);

        public static string NameOf(SkillGroup g) => g switch
        {
            SkillGroup.Conditioning => "Conditioning",
            SkillGroup.Athletic     => "Athleticism",
            SkillGroup.Combat       => "Striking and power",
            SkillGroup.Mat          => "Mat wrestling",
            SkillGroup.Craft        => "Ring craft",
            _                       => "Presence"
        };

        /// <summary>How long somebody has worked for the player, in whole months; null if they do not.</summary>
        public static int? MonthsWithYou(Career career, Wrestler w) =>
            career.ContractOf(w) is { } c
                ? Math.Max(0, (career.CurrentDate.Year - c.Start.Year) * 12 + career.CurrentDate.Month - c.Start.Month)
                : null;

        public static Reading Of(Career career, Wrestler w)
        {
            var today = career.CurrentDate;
            int? months = MonthsWithYou(career, w);
            int know = months is not { } m ? 0 : m < 3 ? 0 : m < 12 ? 1 : 2;
            var mine = career.ContractOf(w);
            var before = mine is not null ? Growth.LevelsOn(w, mine.Start) : Growth.LevelsOn(w, today.AddMonths(-12));

            var groups = new List<GroupReading>();
            bool room = false, past = false;
            foreach (var g in Growth.All)
            {
                double level = Growth.Level(w, g);
                double now = w.Development is { } d ? d.Ceiling[(int)g] * Growth.Curve(w, g, today) : level;
                double best = w.Development is { } d2 ? Math.Min(99, d2.Ceiling[(int)g] * Peak(w, g, today.Year)) : level;
                double toCome = best - level;
                if (toCome >= 5 && g != SkillGroup.Conditioning) room = true;
                if (level > now + 2) past = true;

                string potential = know switch
                {
                    // What anyone can see: which way their age is taking them.
                    0 => level > now + 2 ? "Past their best" : toCome >= 5 ? "Young enough to improve" : "About where they will be",
                    1 => level > now + 2 ? "Past their best" : toCome >= 7 ? "Room to grow" : "Close to their level",
                    _ => level > now + 2 ? "Past their best"
                       : toCome >= 15 ? "A lot still to come"
                       : toCome >= 7  ? "Room to grow"
                       : toCome >= 3  ? "A little more to come"
                       : "At their level"
                };

                string? learns = null;
                if (know > 0 && w.Development is { } dev && g != SkillGroup.Conditioning)
                {
                    double s = dev.Speed[(int)g];
                    learns = know == 1
                        ? (s >= 1.15 ? "Picks it up quickly" : s >= 0.85 ? "Learns steadily" : "Slow to learn it")
                        : (s >= 1.3 ? "Picks it up quickly" : s >= 1.1 ? "Learns well" : s >= 0.9 ? "Learns steadily"
                           : s >= 0.7 ? "Slow to learn it" : "Very slow to learn it");
                }

                int? change = before is { } b ? (int)Math.Round(level) - b[(int)g] : null;
                groups.Add(new GroupReading(g, NameOf(g), (int)Math.Round(level), change, potential, learns));
            }

            // Idle means two months without a match — counted from when they could first have
            // had one, not from a career that opened this morning.
            var since = w.LastAppearance ?? mine?.Start ?? career.StartDate;
            bool idle = today.DayNumber - since.DayNumber > 60;
            string summary = past && !room ? "Past their best in the ring: age is taking more than work adds."
                : room && idle ? "Has room to grow, but is not working enough to improve."
                : room ? "Still improving, as long as they keep working."
                : "About as good as they will get; it is a matter of keeping them sharp.";

            string knowledge = months switch
            {
                null => "What anyone can see from the outside. Work with them to learn more.",
                < 3  => "Too early to judge much; this sharpens the longer they work for you.",
                < 12 => "A first read. It sharpens after a year with you.",
                _    => "You know them well by now."
            };
            return new Reading(summary, knowledge, months, groups);
        }

        /// <summary>The group's curve at its best age from here on: where the ceiling is reached.</summary>
        private static double Peak(Wrestler w, SkillGroup g, int year)
        {
            double best = 0;
            for (int y = year; y <= year + 25; y++) best = Math.Max(best, Growth.Curve(w, g, y));
            return best;
        }
    }
}
