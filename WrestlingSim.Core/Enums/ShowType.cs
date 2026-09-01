namespace WrestlingSim.Enums
{
    /// <summary>
    /// The three kinds of show a promotion runs. They have genuinely different economics
    /// and creative rules — see docs/wrestling-reference/06-schedule-and-cadence.md §1.
    /// </summary>
    public enum ShowType
    {
        /// <summary>
        /// Weekly episodic television. Exists to be broadcast; builds stories. You produce
        /// it whether or not you have anything to say.
        /// </summary>
        Television,

        /// <summary>
        /// The monthly-or-so major event. Where accumulated investment gets cashed in and
        /// blow-offs happen.
        /// </summary>
        PremiumEvent,

        /// <summary>
        /// Untelevised live event. Gate and merchandise; nothing consequential happens,
        /// because the television audience was not there to see it.
        /// </summary>
        HouseShow
    }
}
