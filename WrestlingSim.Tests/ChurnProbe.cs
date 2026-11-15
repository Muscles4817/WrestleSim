using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models.World;
using Xunit;
namespace WrestlingSim.Tests
{
    /// <summary>A measuring tool: what television promotions decide, week by week, and why.</summary>
    public class ChurnProbe
    {
        [Fact]
        public void Run()
        {
            string log = Environment.GetEnvironmentVariable("CHURN_LOG") ?? "";
            if (log.Length == 0) return;
            int weeks = int.Parse(Environment.GetEnvironmentVariable("CHURN_WEEKS") ?? "12");
            var lines = new List<string>();
            var day = new DateOnly(2026, 1, 5);
            var career = new Career { Promotion = new Promotion { Name = "Probe", Tier = PromotionTier.Independent, Region = Region.Midwest }, StartDate = day, CurrentDate = day, Roster = DataLoaders.LoadEmbeddedWrestlers() };
            var choice = new WorldChoice(Enum.Parse<WorldSize>(Environment.GetEnvironmentVariable("CHURN_SIZE") ?? "Small"), WorldSource.Database, StableSeed.From("churn"));
            World.Populate(career, choice, DataLoaders.LoadEmbeddedWrestlers());
            Growth.Begin(career); career.OpenBooks(day); career.Titles.SeedDefaults(career.Promotion.Name, day);
            World.Open(career, day, choice);
            foreach (var _ in World.PlanOpening(career)) { }
            var opening = career.Rivals.Where(r => r.Promotion.HasTelevision).SelectMany(r => r.Decisions).Select(d => d.Score).OrderBy(x => x).ToList();
            if (opening.Count > 0)
                lines.Add($"opening TV decisions: {opening.Count}, scores min {opening[0]:F3} p25 {opening[opening.Count / 4]:F3} median {opening[opening.Count / 2]:F3} p75 {opening[opening.Count * 3 / 4]:F3} max {opening[^1]:F3}; under 0.01: {opening.Count(x => x < 0.01)}");
            var seen = career.Rivals.ToDictionary(r => r.Id, r => r.Decisions.Count);
            for (int w = 0; w < weeks; w++)
            {
                for (int d = 0; d < 7; d++) career.AdvanceOneDay();
                foreach (var r in career.Rivals.Where(r => r.Promotion.HasTelevision && r.IsActive))
                {
                    foreach (var dec in r.Decisions.Skip(seen[r.Id]))
                    {
                        var who = career.FindWrestler(dec.WrestlerId);
                        lines.Add($"wk{w + 1} {r.Name} ({r.Promotion.Tier}, {r.Contracts.Count} on books): {dec.Move} {who?.RingName} (pop {who?.Overness:0}, ask {dec.Annual:N0}) score {dec.Score:F3} — {dec.Reason} | terms {string.Join(", ", dec.Terms.Where(t => Math.Abs(t.Value) > 0.0005).Select(t => $"{t.Key} {t.Value:+0.000;-0.000}"))}");
                    }
                    seen[r.Id] = r.Decisions.Count;
                }
            }
            foreach (var r in career.Rivals.Where(r => r.Promotion.HasTelevision).OrderByDescending(r => r.Promotion.Tier))
                lines.Insert(0, $"  {r.Name} ({r.Promotion.Tier}): {r.Contracts.Count} on the books now, opened with {WorldShape.RosterFor(choice.Size, r.Promotion.Tier)}; payroll {r.DailyPayroll * 365:N0} a year, cash {r.Books.Cash:N0}{(r.IsActive ? "" : " (folded)")}");
            var moves = lines.GroupBy(l => l.Split(':')[1].Trim().Split(' ')[0]).Select(g => $"{g.Key} {g.Count()}");
            lines.Insert(0, $"{lines.Count} TV decisions in {weeks} weeks: {string.Join(", ", moves)}");
            File.WriteAllLines(log, lines);
        }
    }
}
