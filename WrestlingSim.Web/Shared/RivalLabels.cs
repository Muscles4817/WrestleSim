using WrestlingSim.Models.World;

namespace WrestlingSim.Web.Shared;

/// <summary>Words for a rival promotion that more than one screen uses.</summary>
public static class RivalLabels
{
    public static string Motive(FounderMotive m) => m switch
    {
        FounderMotive.Business  => "Run for a return",
        FounderMotive.Vanity    => "Run for its owner",
        FounderMotive.Ideology  => "Run for good wrestling",
        FounderMotive.Legacy    => "A family business",
        FounderMotive.MarkMoney => "A wealthy fan's money",
        FounderMotive.Platform  => "A media company's",
        _                       => m.ToString()
    };

    /// <summary>The most recent thing it decided, in a line, or what it is known for if nothing yet.</summary>
    public static string Doing(Career career, RivalPromotion rival)
    {
        if (!rival.IsActive) return $"Closed {rival.Folded:d MMM yyyy}";
        if (rival.Decisions.LastOrDefault() is { } d)
        {
            var who = career.FindWrestler(d.WrestlerId)?.RingName ?? d.WrestlerId;
            string verb = d.Move switch
            {
                "signed"   => "Signed",
                "released" => "Released",
                "bid for"  => "Bid for",
                "added"    => "Started booking",
                "dropped"  => "Stopped booking",
                _          => d.Move
            };
            return $"{verb} {who} ({d.Date:d MMM})";
        }
        return "No moves yet";
    }

    public static string WageReading(double factor) =>
        factor > 1.01 ? "Above normal. A third big company bidding pushes every price up."
        : factor < 0.99 ? "Below normal. Fewer big companies bidding means cheaper talent."
        : "Normal. Two big companies are bidding for talent.";

    public static string InterestReading(double factor) =>
        factor > 1.01 ? "Up. Crowds everywhere grow when the big companies are at war."
        : factor < 0.99 ? "Down. A monopoly lets interest in wrestling sag."
        : "Normal. Two big companies competing keeps interest where it is.";
}
