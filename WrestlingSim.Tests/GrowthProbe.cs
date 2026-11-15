using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Person;
using WrestlingSim.Models.World;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// A measuring tool, not a test: runs a world for years and reports how careers moved.
    /// <code>GROWTH_LOG=/tmp/growth.log GROWTH_YEARS=5 dotnet test --filter "FullyQualifiedName~GrowthProbe"</code>
    /// </summary>
    public class GrowthProbe
    {
        [Fact]
        public void Run()
        {
            string log = Environment.GetEnvironmentVariable("GROWTH_LOG") ?? "";
            if (log.Length == 0) return;
            int years = int.Parse(Environment.GetEnvironmentVariable("GROWTH_YEARS") ?? "5");
            var source = Enum.Parse<WorldSource>(Environment.GetEnvironmentVariable("GROWTH_SOURCE") ?? "Database");
            var lines = new List<string>();
            void Say(string s) { lines.Add(s); File.WriteAllLines(log, lines); }

            var day = new DateOnly(2026, 1, 5);
            var career = new Career
            {
                Promotion = new Promotion { Name = "Probe", Tier = PromotionTier.Independent, Region = Region.Midwest },
                StartDate = day, CurrentDate = day, Roster = DataLoaders.LoadEmbeddedWrestlers()
            };
            var choice = new WorldChoice(WorldSize.Small, source, StableSeed.From("growth"));
            World.Populate(career, choice, DataLoaders.LoadEmbeddedWrestlers());
            Growth.Begin(career);
            career.OpenBooks(day);
            career.Titles.SeedDefaults(career.Promotion.Name, day);
            World.Open(career, day, choice);
            foreach (var _ in World.PlanOpening(career)) { }

            var speeds = Growth.All.Select(g => career.Roster.Where(w => !w.IsGenerated && Growth.Judged(w, g) && g != SkillGroup.Conditioning)
                .Select(w => w.Development!.Speed[(int)g]).OrderBy(x => x).ToList()).ToList();
            foreach (var g in Growth.All.Where(g => speeds[(int)g].Count > 0))
            {
                var s = speeds[(int)g];
                Say($"real learning speed, {g}: n={s.Count} min {s[0]:0.00} p25 {s[s.Count / 4]:0.00} median {s[s.Count / 2]:0.00} p75 {s[s.Count * 3 / 4]:0.00} max {s[^1]:0.00}");
            }

            var start = career.Roster.ToDictionary(w => w.Id, w => Growth.All.Select(g => Growth.Level(w, g)).ToArray());
            var matches = career.Roster.ToDictionary(w => w.Id, _ => 0);
            var months = career.Roster.ToDictionary(w => w.Id, _ => 0);

            var end = day.AddYears(years);
            while (career.CurrentDate < end)
            {
                bool first = career.CurrentDate.AddDays(1).Day == 1;
                if (first)
                    foreach (var w in career.Roster)
                        if (w.Development is { } d) { matches[w.Id] += d.Matches; months[w.Id]++; }
                if (!career.AdvanceOneDay()) { Say("stopped: a show is due"); break; }
            }
            Say($"ran to {career.CurrentDate}");

            string Band(Wrestler w) => (w.AgeOn(day) ?? 30) switch { < 24 => "under 24", < 29 => "24-28", < 36 => "29-35", < 43 => "36-42", _ => "43+" };
            string Use(Wrestler w) => months[w.Id] == 0 ? "?" : (matches[w.Id] / (double)months[w.Id]) switch { < 0.5 => "unused", < 2 => "some", _ => "busy" };

            Say(source == WorldSource.Database ? "(the real roster only)" : "(everybody)");
            Say("change in each group's level over the run, by age at the start and how much they worked (mean, points out of 100):");
            Say("  " + string.Join(" | ", Growth.All.Select(g => (g.ToString() + "    ")[..4])) + "  (n)");
            foreach (var grp in career.Roster.Where(w => !w.IsGenerated || source == WorldSource.Generated).GroupBy(w => (Band(w), Use(w))).OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2))
            {
                var deltas = Growth.All.Select(g => grp.Average(w => Growth.Level(w, g) - start[w.Id][(int)g])).ToArray();
                Say($"  {grp.Key.Item1,-9} {grp.Key.Item2,-7} " + string.Join(" | ", deltas.Select(x => $"{x,5:+0.0;-0.0}")) + $"  ({grp.Count()})");
            }

            double Overall(Wrestler w) => w.RingSkills.GetOverallSkill() * 20;
            Say($"whole world in-ring average: {start.Values.Average(v => (v[2] + v[3] + v[1]) / 3):0.0} -> {career.Roster.Average(w => (Growth.Level(w, SkillGroup.Combat) + Growth.Level(w, SkillGroup.Mat) + Growth.Level(w, SkillGroup.Athletic)) / 3):0.0}");
            foreach (var name in new[] { "Bron Breakker", "Rhea Ripley", "Randy Orton", "Cody Rhodes", "Rey Mysterio" })
                if (career.Roster.FirstOrDefault(w => w.RingName == name) is { } w)
                    Say($"  {name}: age {w.AgeOn(day)}, worked {matches[w.Id]} matches; " + string.Join(", ", Growth.All.Select(g => $"{(g.ToString() + "    ")[..4]} {start[w.Id][(int)g]:0}->{Growth.Level(w, g):0}")));
        }
    }
}
