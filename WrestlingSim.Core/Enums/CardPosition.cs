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
    }
}
