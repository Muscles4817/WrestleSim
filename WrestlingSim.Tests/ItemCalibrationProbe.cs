using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models.World;
using Xunit;
namespace WrestlingSim.Tests
{
    /// <summary>A measuring tool: every rival item worked by the engine, beside what the forecast imagined. Writes CSV.</summary>
    public class ItemCalibrationProbe
    {
        [Fact]
        public void Run()
        {
            string log = Environment.GetEnvironmentVariable("ITEMS_CSV") ?? "";
            if (log.Length == 0) return;
            World.Observed = new();
            var day = new DateOnly(2026, 1, 5);
            var career = new Career { Promotion = new Promotion { Name = "Probe", Tier = PromotionTier.Independent, Region = Region.Midwest }, StartDate = day, CurrentDate = day, Roster = DataLoaders.LoadEmbeddedWrestlers() };
            var choice = new WorldChoice(WorldSize.Small, WorldSource.Database, StableSeed.From("items"));
            World.Populate(career, choice, DataLoaders.LoadEmbeddedWrestlers());
            Growth.Begin(career); career.OpenBooks(day); career.Titles.SeedDefaults(career.Promotion.Name, day);
            World.Open(career, day, choice);
            foreach (var _ in World.PlanOpening(career)) { }
            for (int i = 0; i < 240; i++) career.AdvanceOneDay();
            var lines = new List<string> { "tier,type,match,title,minutes,imagined,raw,score,skill,over,acts,fresh,charisma,psych,bias" };
            foreach (var o in World.Observed)
                lines.Add($"{(int)o.Tier},{o.Type},{(o.Match ? 1 : 0)},{(o.Title ? 1 : 0)},{o.Minutes},{o.Imagined:F2},{o.Raw:F2},{o.Score:F2},{o.Skill:F3},{o.Over:F2},{o.Acts:F3},{o.Fresh:F3},{o.Charisma:F2},{o.Psych:F1},{o.Bias}");
            File.WriteAllLines(log, lines);
            World.Observed = null;
        }
    }
}
