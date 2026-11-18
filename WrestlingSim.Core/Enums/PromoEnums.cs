using System.Text.Json.Serialization;

namespace WrestlingSim.Enums
{
    /// <summary>
    /// How much of a promo is written. docs/gimmick-plan.md Part 3: neither form wins by rule —
    /// both can be superb and both can be awful, and which depends on who is involved.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PromoForm
    {
        /// <summary>Every word written. As good as the writing and the reading of it.</summary>
        Scripted,

        /// <summary>The beats are written; the words are theirs.</summary>
        BulletPoints,

        /// <summary>Handed the microphone. As good as they are live.</summary>
        Free
    }

    /// <summary>What a talking segment physically asks of somebody.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PromoStaging
    {
        /// <summary>Seated or still: an interview, a desk, a sit-down.</summary>
        Seated,

        /// <summary>Standing in the ring with a microphone.</summary>
        Ring,

        /// <summary>Free to roam and work the crowd.</summary>
        Roaming,

        /// <summary>Produced: a vignette, props, a cinematic piece.</summary>
        SetPiece,

        /// <summary>The promo includes, or turns into, a fight.</summary>
        Physical
    }

    /// <summary>What a promo is meant to make the audience feel.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PromoRegister
    {
        Menacing,
        Angry,
        Emotional,
        Fearful,
        Funny,
        Arrogant,
        Inspirational,
        Cold
    }
}
