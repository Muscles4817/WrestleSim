using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// A measuring tool, not a test: runs a world for years and reports who arrived and who left.
    /// <code>CAREERS_LOG=/tmp/careers.log CAREERS_YEARS=10 dotnet test --filter "FullyQualifiedName~CareersProbe"</code>
    /// </summary>
    public class CareersProbe
    {
        [Fact]
        public void Run()
        {
            string log = Environment.GetEnvironmentVariable("CAREERS_LOG") ?? "";
            if (log.Length == 0) return;
            int years = int.Parse(Environment.GetEnvironmentVariable("CAREERS_YEARS") ?? "10");
            var lines = new List<string>();
            void Say(string s) { lines.Add(s); File.WriteAllLines(log, lines); }

            var day = new DateOnly(2026, 1, 5);
            var career = new Career
            {
                Promotion = new Promotion { Name = "Probe", Tier = PromotionTier.Independent, Region = Region.Midwest },
                StartDate = day, CurrentDate = day, Roster = DataLoaders.LoadEmbeddedWrestlers()
            };
            var choice = new WorldChoice(WorldSize.Small, WorldSource.Database, StableSeed.From("careers"));
            World.Populate(career, choice, DataLoaders.LoadEmbeddedWrestlers());
            Growth.Begin(career);
            career.OpenBooks(day);
            career.Titles.SeedDefaults(career.Promotion.Name, day);
            World.Open(career, day, choice);
            foreach (var _ in World.PlanOpening(career)) { }

            {
                var a0 = career.Roster.Where(w => !w.IsRetired).ToList();
                var t0 = a0.OrderByDescending(w => w.Overness).Take(10).ToList();
                Say($"start popularity: top ten mean {t0.Average(w => w.Overness):0}, 70+: {a0.Count(w => w.Overness >= 70)}, 50+: {a0.Count(w => w.Overness >= 50)}, median {a0.Select(w => w.Overness).OrderBy(x => x).ElementAt(a0.Count / 2):0}");
            }
            var real = career.Roster.Where(w => !w.IsGenerated).Select(w => w.Id).ToHashSet();
            for (int y = 0; y < years; y++)
            {
                var end = day.AddYears(y + 1);
                while (career.CurrentDate < end) if (!career.AdvanceOneDay()) { Say("stopped"); return; }
                var active = career.Roster.Where(w => !w.IsRetired).ToList();
                int retiredThisYear = career.Roster.Count(w => w.RetiredOn is { } r && r.Year == career.CurrentDate.Year - 1 + (career.CurrentDate.Month == 1 ? 0 : 1) - 0);
                var ages = active.Select(w => w.AgeOn(career.CurrentDate) ?? 30).OrderBy(a => a).ToList();
                Say($"{career.CurrentDate:yyyy-MM}: active {active.Count}, retired so far {career.Roster.Count(w => w.IsRetired)}, part-time {active.Count(w => w.IsPartTime)}, " +
                    $"rookies so far {career.Roster.Count(w => w.DebutedIn > 2026)}; ages median {ages[ages.Count / 2]}, p90 {ages[ages.Count * 9 / 10]}; " +
                    $"real people active {active.Count(w => real.Contains(w.Id))}; in-ring median {active.Select(w => w.RingSkills.GetOverallSkill() * 20).OrderBy(x => x).ElementAt(active.Count / 2):0}; " +
                    $"rival rosters {career.Rivals.Where(r => r.IsActive).Sum(r => r.Contracts.Count)}, active rivals {career.Rivals.Count(r => r.IsActive)}");
                var top = active.OrderByDescending(w => w.Overness).Take(10).ToList();
                Say($"    popularity: top ten mean {top.Average(w => w.Overness):0} (from {string.Join(", ", top.Take(3).Select(w => $"{w.RingName} {w.Overness:0}"))}), 70+: {active.Count(w => w.Overness >= 70)}, 50+: {active.Count(w => w.Overness >= 50)}, median {active.Select(w => w.Overness).OrderBy(x => x).ElementAt(active.Count / 2):0}; " +
                    $"best rookie {active.Where(w => w.DebutedIn > 2026).Select(w => w.Overness).DefaultIfEmpty(0).Max():0}");
            }

            var gone = career.Roster.Where(w => w.RetiredOn is not null).ToList();
            var at = gone.Select(w => w.AgeOn(w.RetiredOn!.Value) ?? 0).OrderBy(a => a).ToList();
            if (at.Count > 0)
                Say($"retirement ages: n={at.Count} min {at[0]} p25 {at[at.Count / 4]} median {at[at.Count / 2]} p75 {at[at.Count * 3 / 4]} max {at[^1]}; under 30: {at.Count(a => a < 30)}");
            foreach (var w in gone.Where(w => real.Contains(w.Id)).OrderBy(w => w.RetiredOn))
                Say($"  real: {w.RingName} retired {w.RetiredOn:yyyy-MM} at {w.AgeOn(w.RetiredOn!.Value)}; part-time from {w.PartTimeSince?.ToString("yyyy") ?? "-"}");
            foreach (var w in career.Roster.Where(w => real.Contains(w.Id) && !w.IsRetired && w.AgeOn(career.CurrentDate) >= 45))
                Say($"  still going: {w.RingName} at {w.AgeOn(career.CurrentDate)}, popularity {w.Overness:0}{(w.IsPartTime ? ", part-time" : "")}");
            var rookies = career.Roster.Where(w => w.DebutedIn > 2026).ToList();
            var best = rookies.OrderByDescending(w => w.Overness).Take(3);
            Say($"rookies: {rookies.Count}; the most popular now: {string.Join(", ", best.Select(w => $"{w.RingName} ({w.Overness:0}, in-ring {w.RingSkills.GetOverallSkill() * 20:0}, debut {w.DebutedIn})"))}");
            Say($"rookies on a television roster: {rookies.Count(w => career.SignedTo(w) is not null)}");
        }
    }
}
