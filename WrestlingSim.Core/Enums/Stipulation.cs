using System.Text.Json.Serialization;

namespace WrestlingSim.Enums
{
    /// <summary>
    /// A gimmick match, defined as what it takes away.
    ///
    /// **This is a feud mechanic, not a match format.** Everything in
    /// <see cref="MatchType"/> and every format the plan can express — multi-man,
    /// elimination, handicap — comes out of doc 18 §2.5 and changes *who is in the match*
    /// or *how a fall works*. A stipulation is in doc 20 §6, inside "The blow-off", and
    /// doc 04 §5 lists it in the booker's toolkit beside the turn and the title change.
    /// Doc 20 is explicit: "the stipulation must match the escalation. A cage match for a
    /// feud that never got past words…"
    ///
    /// So the vocabulary here is deliberately narrow — the rungs of doc 20 §6.2's ladder
    /// that can be stated as **which finishes are legal**. Doc 04 names the cost of the
    /// screwjob finish as "protects both, sells the rematch", and every rung of the ladder
    /// is the removal of exactly that. One sentence covers the whole enum:
    ///
    ///   **In a gimmick match you can no longer lose cheaply.**
    ///
    /// **Falls Count Anywhere is deliberately absent**, and its absence is the honest
    /// answer rather than an oversight. In a model whose entire stipulation vocabulary is
    /// which finishes are legal, it is indistinguishable from <see cref="NoDisqualification"/>:
    /// what it actually removes is the ring as a boundary, which is a statement about where
    /// the match happens, and the engine has no notion of location. A menu entry that is a
    /// mechanical duplicate of the one above it is a menu that lies.
    ///
    /// The rest of doc 20 §6.2 — Hell in a Cell, Ladder/TLC, Career, Mask, Hair, Loser
    /// Leaves Town — is not here either, for a different reason: those are paid for with
    /// bodies and with roster departures, and this codebase has neither a match injury
    /// model nor a way to write somebody off.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Stipulation
    {
        /// <summary>Wrestling under the rules. Every finish is available.</summary>
        None,

        /// <summary>
        /// No disqualification, or a street fight — doc 20's "rules can't contain this".
        /// The bottom rung, and the one that shows what the ladder is for: the referee
        /// stops being an escape route, and a run-in stops being an excuse.
        /// </summary>
        NoDisqualification,

        /// <summary>
        /// "Nobody escapes, nobody interferes." The rung whose whole point is *who it keeps
        /// out*, which in this codebase means the interference finish is off the table
        /// entirely rather than merely un-protected.
        /// </summary>
        SteelCage,

        /// <summary>
        /// "Only unconsciousness ends it." A roll-up cannot win it and a submission cannot
        /// win it — nothing that relies on the other person being *briefly* beaten counts.
        /// </summary>
        LastManStanding,

        /// <summary>
        /// "Submission of will, not just body — the most personal." The only rung that names
        /// a single way to win rather than removing several, which is why it is not simply
        /// one notch above <see cref="LastManStanding"/> on what it forbids.
        /// </summary>
        IQuit
    }
}
