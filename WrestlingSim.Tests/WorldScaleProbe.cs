using System.Diagnostics;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// A measuring tool, not a test: how long a week of the world takes as the world gets
    /// bigger, and where the time goes. It does nothing unless told where to write.
    ///
    /// <code>
    /// SCALE_LOG=/tmp/scale.log SCALE_CASE=3 SCALE_WEEKS=8 \
    ///   dotnet test --filter "FullyQualifiedName~WorldScaleProbe"
    /// </code>
    ///
    /// The big worlds are **stand-ins**: the ninety real wrestlers copied under numbered names
    /// with their overness scaled down, and promotions handed rosters by rank. They are for
    /// timing and for nothing else — no figure about what such a world does should be read off
    /// one. docs/business-design-record.md, "What it costs", has the table this produced.
    /// </summary>
    public class WorldScaleProbe
    {

        [Fact]
        public void Scale()
        {
            string log = Environment.GetEnvironmentVariable("SCALE_LOG") ?? "";
            if (log.Length == 0) return;
            int only = int.Parse(Environment.GetEnvironmentVariable("SCALE_CASE") ?? "0");
            int weeks = int.Parse(Environment.GetEnvironmentVariable("SCALE_WEEKS") ?? "4");
            var cases = new (string Name, int Wrestlers, (PromotionTier, int, int)[]? Promos)[]
            {
                ("90 wrestlers, 4 rivals (today)", 90, null),
                ("2,000 wrestlers, same 4 rivals", 2000, null),
                ("600 wrestlers, 32 rivals, game-scale rosters", 600, [(PromotionTier.Global, 2, 40), (PromotionTier.National, 3, 25), (PromotionTier.Established, 4, 15), (PromotionTier.SuperIndie, 8, 22), (PromotionTier.Independent, 15, 16)]),
                ("3,000 wrestlers, 92 rivals, real rosters", 3000, [(PromotionTier.Global, 2, 200), (PromotionTier.National, 4, 100), (PromotionTier.Established, 6, 50), (PromotionTier.SuperIndie, 20, 25), (PromotionTier.Independent, 60, 16)]),
                ("6,000 wrestlers, 208 rivals, real rosters", 6000, [(PromotionTier.Global, 2, 200), (PromotionTier.National, 6, 100), (PromotionTier.Established, 10, 50), (PromotionTier.SuperIndie, 40, 25), (PromotionTier.Independent, 150, 16)]),
            };
            var (name, wrestlers, promos) = cases[only];
            var career = StandInWorld.Build(wrestlers, promos);
            World.ForecastsRun = 0;
            World.ForecastCost = new();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < weeks * 7; i++)
            {
                career.AdvanceOneDay();
                if (i % 7 == 6) System.IO.File.AppendAllText(log, $"{name}: week {i / 7 + 1} done at {sw.Elapsed.TotalSeconds:F1}s, {World.ForecastsRun} forecasts\n");
            }
            double week = sw.Elapsed.TotalMilliseconds / weeks;
            double f = 1000.0 / Stopwatch.Frequency;
            double inForecasts = World.ForecastCost.Values.Sum(v => v.Ticks) * f / weeks;
            string byTier = string.Join("; ", World.ForecastCost.OrderByDescending(kv => kv.Key).Select(kv =>
                $"{kv.Key} {kv.Value.Count / weeks}/wk x {kv.Value.Ticks * f / Math.Max(1, kv.Value.Count):F2}ms = {kv.Value.Ticks * f / weeks:F0}ms"));
            var sample = career.Rivals.First(r => r.Promotion.Tier == PromotionTier.Independent);
            var all = new Dictionary<string, int>();
            foreach (var r in career.Rivals) { string k = r.Promotion.Tier + " roster"; all[k] = all.GetValueOrDefault(k) + r.Contracts.Count; }
            var text = new List<string> { "ROSTERS " + string.Join(", ", all.Select(kv => kv.Key + " " + kv.Value)) };
            text.Add($"SAMPLE {sample.Name}: roster {sample.Contracts.Count}, cash {sample.Books.Cash:N0}, last house {(sample.Nights.Count == 0 ? 0 : sample.Nights[^1].Attendance * 100 / Math.Max(1, sample.Nights[^1].Capacity))}%");
            foreach (var d in sample.Decisions)
                text.Add($"   {d.Date:MM-dd} {d.Move} {d.WrestlerId} [{d.Score}] {d.Reason} | {string.Join(", ", d.Terms.Select(t => t.Key + " " + t.Value))}");
            System.IO.File.AppendAllLines(log, text);
            sw.Restart();
            string json = SaveSerializer.ToJson(career);
            System.IO.File.AppendAllText(log,
                $"RESULT {name}: week {week:F0} ms | forecasts {World.ForecastsRun / weeks}/wk taking {inForecasts:F0} ms | everything else {week - inForecasts:F0} ms | save {json.Length / 1048576.0:F2} MB\n   {byTier}\n");
        }
    }
}
