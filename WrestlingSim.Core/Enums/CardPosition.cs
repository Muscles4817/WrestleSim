using System.Text.Json.Serialization;

namespace WrestlingSim.Enums
{
    /// <summary>
    /// Where a wrestler sits on a card. Derived from popularity rather than stored, so it
    /// can never drift out of sync with the roster data. Used to group the booking lists —
    /// picking an opponent from a 30-person roster is otherwise a wall of names.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CardPosition
    {
        Enhancement,
        LowerCard,
        Midcard,
        UpperCard,
        MainEvent
    }

    public static class CardPositionExtensions
    {
        public static string Label(this CardPosition p) => p switch
        {
            CardPosition.MainEvent   => "Main event",
            CardPosition.UpperCard   => "Upper card",
            CardPosition.Midcard     => "Midcard",
            CardPosition.LowerCard   => "Lower card",
            _                        => "Enhancement"
        };

        /// <summary>
        /// The class an overness reading is painted with.
        ///
        /// Every number in the roster list used to be the same grey, so a 91 and a 34 read
        /// as equally important and the booker had to compare digits on seventy rows. The
        /// bands are <see cref="Models.Wrestler.CardPosition"/>'s, not new thresholds — the
        /// game already decides where somebody sits on a card, and inventing a second set of
        /// cut-offs for the colour would let the two disagree.
        /// </summary>
        public static string Tone(this CardPosition p) => p switch
        {
            CardPosition.MainEvent => "pop--main",
            CardPosition.UpperCard => "pop--upper",
            CardPosition.Midcard   => "pop--mid",
            CardPosition.LowerCard => "pop--lower",
            _                      => "pop--enh"
        };
    }
}
