namespace WrestlingSim.Enums
{
    /// <summary>
    /// Where somebody works from, or where a promotion runs. Decided 13 Nov 2026 (living-world
    /// plan §0A): regions exist from the start, and small promotions book from their own.
    ///
    /// Seven broad regions of the United States, the market every price in the game was taken
    /// from. International scenes (Mexico, Japan, the UK) are not modelled yet.
    ///
    /// A wrestler with no region works anywhere. That is what the bundled database's people
    /// are: touring talent whose home a promotion does not care about. A promotion with
    /// television books from anywhere whatever its home; below television, a promotion books
    /// people from its own region and anybody who has none.
    /// </summary>
    public enum Region
    {
        Northeast,
        MidAtlantic,
        Southeast,
        Midwest,
        South,
        Mountain,
        Pacific
    }
}

namespace WrestlingSim.Enums
{
    public static class Regions
    {
        /// <summary>The region as a reader would say it: "the Northeast", "the Pacific coast".</summary>
        public static string Name(this Region region) => region switch
        {
            Region.Northeast   => "the Northeast",
            Region.MidAtlantic => "the Mid-Atlantic",
            Region.Southeast   => "the Southeast",
            Region.Midwest     => "the Midwest",
            Region.South       => "the South",
            Region.Mountain    => "the Mountain West",
            Region.Pacific     => "the Pacific coast",
            _                  => region.ToString()
        };

        /// <summary>Short form for a table cell: "Northeast", "Pacific".</summary>
        public static string Short(this Region region) => region switch
        {
            Region.MidAtlantic => "Mid-Atlantic",
            Region.Mountain    => "Mountain West",
            _                  => region.ToString()
        };
    }
}
