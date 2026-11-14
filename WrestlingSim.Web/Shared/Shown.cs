namespace WrestlingSim.Web.Shared;

/// <summary>
/// How numbers are shown to the player. Display only: the engine keeps its own scales.
///
/// The interface printed the engine's numbers as they were — a ring skill of 3.68, a match of
/// 3.68 / 5.00, a feud worth ×1.03 — and none of them read as anything. The owner asked for
/// that to stop (14 Nov 2026). The rules:
///
///   • **People are on one scale: whole numbers out of 100.** Popularity, psychology, selling
///     and stamina already were; ring skills and charisma (0–5 inside the engine) join them.
///   • **Matches are stars in quarter steps**, the industry's own scale, written as words.
///   • **Everything else is a whole number**, and a multiplier is a percentage.
/// </summary>
public static class Shown
{
    /// <summary>A 0–5 attribute (ring skills, charisma) on the 0–100 scale everything else about a person uses.</summary>
    public static int Of100(double fiveScale) => (int)Math.Round(Math.Clamp(fiveScale, 0, 5) * 20);

    /// <summary>A whole number.</summary>
    public static string Whole(double value) => Math.Round(value).ToString("0");

    /// <summary>A change as a signed whole number: "+3", "−2", "0".</summary>
    public static string Change(double delta)
    {
        int n = (int)Math.Round(delta);
        return n > 0 ? $"+{n}" : n < 0 ? $"−{-n}" : "0";
    }

    /// <summary>A multiplier as a percentage change: ×1.03 is "+3%", ×0.65 is "−35%".</summary>
    public static string Factor(double multiplier)
    {
        int pct = (int)Math.Round((multiplier - 1) * 100);
        return pct > 0 ? $"+{pct}%" : pct < 0 ? $"−{-pct}%" : "no change";
    }

    /// <summary>A share as a whole percentage.</summary>
    public static string Percent(double fraction) => $"{Math.Round(fraction * 100):0}%";

    /// <summary>A part of a match rating in stars, signed: "+1½ stars", "−¼ star", "no change".</summary>
    public static string StarChange(double delta)
    {
        if (Math.Round(Math.Abs(delta) * 4) == 0) return "no change";
        return (delta > 0 ? "+" : "−") + Stars(Math.Abs(delta));
    }

    /// <summary>
    /// A match rating in quarter stars, as words: "3¾ stars", "1 star", "½ star", "no stars".
    /// The engine's own rule (<see cref="WrestlingSim.Models.MatchPlan.MatchEngineResult.StarWords"/>),
    /// so the interface, the show notes and the drawn stars never disagree.
    /// </summary>
    public static string Stars(double rating) => WrestlingSim.Models.MatchPlan.MatchEngineResult.StarWords(rating);
}
