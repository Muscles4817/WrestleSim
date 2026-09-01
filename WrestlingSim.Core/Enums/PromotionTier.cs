namespace WrestlingSim.Enums
{
    /// <summary>
    /// Where a promotion sits in the industry. Documented in
    /// docs/wrestling-reference/01-industry-map.md.
    ///
    /// Tier is not a difficulty setting — it is a set of constraints. It determines how
    /// often you can run, how big a card the audience expects, what buildings you can
    /// fill, and how long a show runs. Everything else in a career is downstream of it.
    /// </summary>
    public enum PromotionTier
    {
        /// <summary>Local / hobbyist. A room, a ring, and a few hundred people at most.</summary>
        Local = 0,

        /// <summary>Regional independent. A handful of towns, monthly-ish.</summary>
        Independent = 1,

        /// <summary>National indie with a reputation and a streaming platform.</summary>
        SuperIndie = 2,

        /// <summary>Established promotion with real television or a domestic broadcast.</summary>
        Established = 3,

        /// <summary>National challenger — weekly TV, a large contracted roster.</summary>
        National = 4,

        /// <summary>The global major. The brand outdraws the roster.</summary>
        Global = 5
    }
}
