namespace WrestlingSim.Enums
{
    /// <summary>
    /// How many ways a match can go — a fact about the sides, not about how the match is
    /// worked (that is <see cref="MatchType"/>).
    ///
    /// The distinction matters because doc 18 §2.5 is about exactly one thing: with two sides
    /// every second of the match is accounted for, one person working and one being worked.
    /// Add a third and somebody is doing nothing, and the entire craft of the format is
    /// disposing of that person plausibly and bringing them back at the right moment.
    /// </summary>
    public enum MatchFormat
    {
        /// <summary>Singles, tag, trios — anything where two sides contest one fall.</summary>
        TwoSided,

        /// <summary>Three sides. One fall, anyone can be pinned, no disqualification.</summary>
        TripleThreat,

        /// <summary>Four or more sides. Same rules, more disposal.</summary>
        MultiWay
    }
}
