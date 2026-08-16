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
}
