using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>A measuring tool: one rival night's card, item by item, worked and imagined.</summary>
    public class RivalNightProbe
    {
        [Fact]
        public void Run()
        {
            string log = Environment.GetEnvironmentVariable("NIGHT_LOG") ?? "";
            if (log.Length == 0) return;
            var lines = new List<string>();
            var day = new DateOnly(2026, 1, 5);
            var career = new Career { Promotion = new Promotion { Name = "Probe", Tier = PromotionTier.Local }, StartDate = day, CurrentDate = day, Roster = DataLoaders.LoadEmbeddedWrestlers() };
            Growth.Begin(career); career.OpenBooks(day); career.Titles.SeedDefaults(career.Promotion.Name, day);
            World.Open(career, day);
            for (int i = 0; i < 60; i++) career.AdvanceOneDay();
            foreach (var r in career.Rivals)
            {
                lines.Add($"{r.Name} ({r.Promotion.Tier}), runtime {r.Promotion.DefaultRuntimeFor(ShowType.PremiumEvent)}/{r.Promotion.DefaultRuntimeFor(ShowType.Television)} min:");
                foreach (var n in r.Nights.TakeLast(2))
                    lines.Add($"  {n.Date} {n.Type}: rating {n.Rating}, filled {n.Filled:P0}; {string.Join(" | ", n.Card)}");
            }
            File.WriteAllLines(log, lines);
        }
    }
}
