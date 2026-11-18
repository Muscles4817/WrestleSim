using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// A measuring tool, not a test: runs a world for years and reports how worn its characters
    /// get (docs/gimmick-plan.md Part 4). The rivals book; the player's promotion runs nothing.
    /// <code>CHARACTER_LOG=/tmp/characters.log CHARACTER_YEARS=3 dotnet test --filter "FullyQualifiedName~CharacterProbe"</code>
    /// </summary>
    public class CharacterProbe
    {
        [Fact]
        public void Run()
        {
            string log = Environment.GetEnvironmentVariable("CHARACTER_LOG") ?? "";
            if (log.Length == 0) return;
            int years = int.Parse(Environment.GetEnvironmentVariable("CHARACTER_YEARS") ?? "3");
            var lines = new List<string>();
            void Say(string s) { lines.Add(s); File.WriteAllLines(log, lines); }

            var day = new DateOnly(2026, 1, 5);
            var career = new Career
            {
                Promotion = new Promotion { Name = "Probe", Tier = PromotionTier.Independent, Region = Region.Midwest },
                StartDate = day, CurrentDate = day, Roster = DataLoaders.LoadEmbeddedWrestlers()
            };
            var choice = new WorldChoice(WorldSize.Small, WorldSource.Database, StableSeed.From("characters"));
            World.Populate(career, choice, DataLoaders.LoadEmbeddedWrestlers());
            Growth.Begin(career);
            career.OpenBooks(day);
            career.Titles.SeedDefaults(career.Promotion.Name, day);
            World.Open(career, day, choice);
            foreach (var _ in World.PlanOpening(career)) { }

            string Where(Wrestler w) =>
                career.Rivals.FirstOrDefault(r => r.Contracts.Any(c => c.WrestlerId == w.Id)) is { } r
                    ? (r.Promotion.HasTelevision ? "television" : "no television")
                    : "unsigned";

            void Report(string when)
            {
                Say($"── {when} ({career.CurrentDate})");
                foreach (var g in career.Roster.Where(w => !w.IsRetired).GroupBy(Where).OrderBy(g => g.Key))
                {
                    var f = g.Select(w => w.Gimmick.Freshness).OrderBy(x => x).ToList();
                    int worn = f.Count(x => x < CharacterFreshness.Worn);
                    Say($"  {g.Key,-14} n={f.Count,4}  p10 {f[f.Count / 10]:0.00}  median {f[f.Count / 2]:0.00}  p90 {f[f.Count * 9 / 10]:0.00}" +
                        $"  worn {worn,3} ({worn / (double)f.Count:P0})  mean appeal factor {g.Average(CharacterFreshness.Factor):0.000}");
                }
            }

            // Real people only, so the same people are compared at both ends.
            var real = career.Roster.Where(w => !w.IsGenerated).ToList();
            var startOver = real.ToDictionary(w => w.Id, w => w.Overness);
            void ByType()
            {
                Say("  change in overness by character type, real roster (mean, n):");
                foreach (var g in real.GroupBy(w => w.Gimmick.Type).OrderBy(g => g.Key.ToString()))
                    Say($"    {g.Key,-15} {g.Average(w => w.Overness - startOver[w.Id]),6:+0.0;-0.0}  n={g.Count()}");
            }

            Report("start");
            var end = day.AddYears(years);
            var next = day.AddMonths(6);
            while (career.CurrentDate < end)
            {
                if (!career.AdvanceOneDay()) { Say("stopped: a show is due"); break; }
                if (career.CurrentDate >= next) { Report($"after {(next.DayNumber - day.DayNumber) / 30} months"); next = next.AddMonths(6); }
            }
            ByType();
            var turned = career.Roster.Where(w => w.Turns > 0).ToList();
            Say($"turns: {turned.Sum(w => w.Turns)} by {turned.Count} people over {years} years; " +
                $"{career.News.Count(n => n.Text.Contains("has turned"))} in the last 80 news items");
            foreach (var n in career.News.Where(n => n.Text.Contains("has turned")).TakeLast(8)) Say($"  {n.Date}: {n.Text}");
        }
    }
}
