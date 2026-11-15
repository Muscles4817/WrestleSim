using System.Diagnostics;
using Microsoft.JSInterop;
using WrestlingSim.Engine;

namespace WrestlingSim.Web.Services;

/// <summary>
/// A developer's measurement, not part of the game: no screen links to it. Called from the
/// browser console (or a test script) as
/// <c>DotNet.invokeMethodAsync('WrestlingSim.Web', 'WorldWeek', 'full', 4)</c>, it builds a
/// stand-in world of the given size inside the running app and times weeks of the clock, so
/// the speed budget (one week in two seconds, docs/living-world-plan.md) is measured where
/// the player plays rather than natively.
/// </summary>
public static class Benchmarks
{
    [JSInvokable]
    public static string WorldWeek(string size, int weeks)
    {
        var (wrestlers, promotions) = StandInWorld.Size(size);
        var build = Stopwatch.StartNew();
        var career = StandInWorld.Build(wrestlers, promotions);
        double built = build.Elapsed.TotalMilliseconds;

        var each = new List<double>();
        for (int w = 0; w < weeks; w++)
        {
            var sw = Stopwatch.StartNew();
            for (int d = 0; d < 7; d++) career.AdvanceOneDay();
            each.Add(sw.Elapsed.TotalMilliseconds);
        }
        var save = Stopwatch.StartNew();
        int length = WrestlingSim.Persistence.SaveSerializer.ToJson(career).Length;
        double saved = save.Elapsed.TotalMilliseconds;
        return $"{size}: {wrestlers} wrestlers, {promotions.Sum(p => p.Count)} promotions; built in {built:F0} ms; "
             + $"weeks {string.Join(", ", each.Select(e => $"{e / 1000:F1}s"))}; save {length / 1024} KB written in {saved:F0} ms";
    }

    /// <summary>
    /// The same for a real world of the chosen size and source, made as a new career makes
    /// one: <c>DotNet.invokeMethodAsync('WrestlingSim.Web', 'RealWeek', 'Full', 'Database', 4)</c>.
    /// Times the making, the opening plans, each week, and the save, separately.
    /// </summary>
    [JSInvokable]
    public static string RealWeek(string size, string source, int weeks)
    {
        var day = new DateOnly(2026, 1, 5);
        var choice = new WorldChoice(Enum.Parse<WorldSize>(size), Enum.Parse<WorldSource>(source), 12345);
        var sw = Stopwatch.StartNew();
        var career = new WrestlingSim.Models.World.Career
        {
            Promotion = new WrestlingSim.Models.World.Promotion { Name = "Bench", Tier = WrestlingSim.Enums.PromotionTier.Independent, Region = WrestlingSim.Enums.Region.Midwest },
            StartDate = day, CurrentDate = day, Roster = DataLoaders.LoadEmbeddedWrestlers()
        };
        World.Populate(career, choice, DataLoaders.LoadEmbeddedWrestlers());
        double populated = sw.Elapsed.TotalMilliseconds; sw.Restart();
        career.OpenBooks(day);
        career.Titles.SeedDefaults(career.Promotion.Name, day);
        World.Open(career, day, choice);
        double opened = sw.Elapsed.TotalMilliseconds; sw.Restart();
        foreach (var _ in World.PlanOpening(career)) { }
        double planned = sw.Elapsed.TotalMilliseconds;

        var each = new List<string>();
        for (int w = 0; w < weeks; w++)
        {
            var days = new List<double>();
            for (int d = 0; d < 7; d++) { sw.Restart(); career.AdvanceOneDay(); days.Add(sw.Elapsed.TotalMilliseconds); }
            each.Add($"{days.Sum() / 1000:F1}s (worst day {days.Max() / 1000:F1}s)");
        }
        sw.Restart();
        int length = WrestlingSim.Persistence.SaveSerializer.ToJson(career).Length;
        double saved = sw.Elapsed.TotalMilliseconds;
        return $"{size}/{source}: populated {populated / 1000:F1}s, opened {opened / 1000:F1}s, opening plans {planned / 1000:F1}s; "
             + $"weeks {string.Join(", ", each)}; save {length / 1024:N0} KB in {saved / 1000:F1}s";
    }
}
