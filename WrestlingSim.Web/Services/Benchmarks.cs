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
}
