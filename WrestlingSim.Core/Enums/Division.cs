using System.Text.Json.Serialization;

namespace WrestlingSim.Enums
{
    /// <summary>
    /// Which division a wrestler competes in. Used to group the roster in the booking
    /// UI and to warn when a match is booked across divisions — it does not block it,
    /// because intergender matches are a legitimate booking decision, just an unusual one.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Division
    {
        Womens,
        Mens
    }

    public static class DivisionExtensions
    {
        public static string Label(this Division d) => d == Division.Womens ? "Women" : "Men";

        /// <summary>
        /// The modifier a division badge is styled with.
        ///
        /// Two hues that are neither of the alignment colours and neither of them the
        /// conventional pink and blue: the divisions need telling apart in a mixed list,
        /// which is a legibility problem, and nothing about which one is which follows from
        /// the colour.
        /// </summary>
        public static string Badge(this Division d) =>
            d == Division.Womens ? "badge--cyan" : "badge--violet";
    }
}
