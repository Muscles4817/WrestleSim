using System.Text.Json.Serialization;

namespace WrestlingSim.Enums
{
    /// <summary>
    /// What kind of noise — or silence — a beat drew.
    ///
    /// The engine used to carry crowd reaction as a single number, which cannot tell apart
    /// the two things a wrestling crowd does that both look like "quiet": a room holding
    /// its breath, and a room that has stopped caring. Those are opposite outcomes.
    /// docs/wrestling-reference/16-crowd-psychology.md §2 is the taxonomy this follows, and
    /// §2.1 is the rule that matters most — **promotions consistently over-fear boos and
    /// under-fear silence**.
    ///
    /// The ordering here is deliberate: everything above <see cref="Silence"/> is a crowd
    /// that is invested, whichever direction it is invested in.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ReactionKind
    {
        /// <summary>
        /// A sudden cheer. Something the audience wanted just happened.
        /// </summary>
        Pop,

        /// <summary>
        /// A sustained boo aimed at somebody the crowd wants to see beaten. This is a
        /// *good* outcome and the engine treats it as one — heat is the fuel the whole
        /// face-in-peril structure runs on.
        /// </summary>
        Heat,

        /// <summary>
        /// Hushed and attentive. The audience is invested and tense — the near-tag, the
        /// count before a kick-out. Reads as dead on a decibel meter and is the opposite.
        /// </summary>
        Tension,

        /// <summary>
        /// Nothing. No investment either way, and the actual failure state: a booed
        /// babyface is a solvable problem, an ignored one takes months to fix.
        /// </summary>
        Silence,

        /// <summary>
        /// Boos with disengagement — "boring", counting along, chanting for somebody who
        /// is not in the match. The audience is entertaining itself, which is worse than
        /// silence because it is contagious and it is loud.
        /// </summary>
        GoAwayHeat
    }
}
