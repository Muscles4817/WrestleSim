using System.Text.Json.Serialization;

namespace WrestlingSim.Enums
{
    /// <summary>
    /// Where a title sits in the promotion's hierarchy — docs/wrestling-reference/21-championships.md §2.
    ///
    /// The tier is not decoration. It decides how much of the audience's finite attention
    /// the belt lays claim to, which is what makes adding a second world title far more
    /// expensive than adding a television title.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TitleTier
    {
        /// <summary>The top of the promotion. Whoever holds it is the main event by definition.</summary>
        World,

        /// <summary>An elevation tool — a step toward the top, not a consolation prize.</summary>
        Secondary,

        /// <summary>A television, cruiserweight or specialty belt. Gives a division an identity.</summary>
        Tertiary
    }
}
