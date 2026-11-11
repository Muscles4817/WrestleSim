using System.Globalization;

namespace WrestlingSim.Web.Shared;

/// <summary>
/// Dollars as a booker reads them. A Local promotion counts in hundreds and a Global one in
/// millions, so the format scales with the figure rather than printing $250,000,000.
/// </summary>
public static class Money
{
    // Invariant, not en-US: the WebAssembly build runs in globalization-invariant mode, where
    // asking for a named culture throws — it took the dashboard down on first render. The
    // invariant culture groups thousands with commas and uses a full stop, which is all this
    // needs.
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    public static string Format(decimal amount)
    {
        var sign = amount < 0 ? "−" : "";
        var a = Math.Abs(amount);
        var body = a switch
        {
            >= 1_000_000_000m => (a / 1_000_000_000m).ToString("0.##", En) + "B",
            >= 10_000_000m    => (a / 1_000_000m).ToString("0.#", En) + "M",
            >= 1_000_000m     => (a / 1_000_000m).ToString("0.##", En) + "M",
            _                 => a.ToString("N0", En)
        };
        return $"{sign}${body}";
    }

    /// <summary>With an explicit plus, for a figure that is a change rather than a balance.</summary>
    public static string Signed(decimal amount) => amount > 0 ? "+" + Format(amount) : Format(amount);

    /// <summary>"93%", not the invariant culture's "93 %".</summary>
    public static string Percent(double fraction) => $"{fraction * 100:F0}%";

    public static string Tone(decimal amount) => amount < 0 ? "grade--poor" : "grade--great";
}
