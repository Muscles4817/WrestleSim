namespace WrestlingSim.Enums
{
    public enum BeatType
    {
        // Openings
        HotOpening,
        SlowOpening,
        StandardOpening,

        // Control / Narrative
        HeatSegment,
        Comeback,
        RestHold,

        // High-impact spots
        HighSpot,
        CrowdBrawl,

        // Storytelling
        PsychologicalWarfare,
        RevengeSpot,
        FeudalEscalation,
        ThirdPartyPullIn,
        AlliesRejected,

        // Near falls
        NearFall,

        // ── Tag ──────────────────────────────────────────────────────────────
        // The tag formula, in the order it is usually worked. All but Shine and Cutoff
        // need a side with somebody on the apron — see MatchBeat.IsTagBeat and
        // MatchPlan.Validate. A shine and a cut-off are match-craft terms that describe
        // the singles face-in-peril structure just as well
        // (docs/wrestling-reference/18-match-craft.md §2.3), so they are bookable in both.

        /// <summary>The face team's opening control stretch, before the heat.</summary>
        Shine,

        /// <summary>The heels take over and end the shine. The heat starts here.</summary>
        Cutoff,

        /// <summary>
        /// The face-in-peril segment: one of them kept cut off from the corner. Charges the
        /// hot tag — this is what makes the payoff worth anything.
        /// </summary>
        Isolation,

        /// <summary>
        /// The tag reached for and denied. Quietens the room in the moment and charges the
        /// hot tag harder than another isolation does.
        /// </summary>
        NearTag,

        /// <summary>
        /// The payoff. The loudest planned moment in the format, and worth almost nothing
        /// if the isolation did not happen first.
        /// </summary>
        HotTag,

        /// <summary>A routine tag. Changes who is legal without being a moment.</summary>
        Tag,

        /// <summary>A tag the opponents did not see. Heel cheating, or a face surprise.</summary>
        BlindTag,

        /// <summary>Team offence. Scores off how well the pair work together.</summary>
        DoubleTeam,

        /// <summary>Partners collide. A story now, and a team that splits up later.</summary>
        Miscommunication,

        /// <summary>The partner breaks up the pin. Extends a near-fall; wears out fast.</summary>
        SaveBreakup,

        /// <summary>
        /// Everyone in, referee loses control. Resets the room before the finish.
        ///
        /// The name says four because that is what a tag match has. It is kept as the
        /// identifier even for trios, where six are in, because the enum member name is
        /// what gets written into save files — renaming it would orphan every card already
        /// on disk. The display name and the commentary count the people actually in there.
        /// </summary>
        AllFourBrawl,

        // Finishes
        FinishClean,
        FinishRollup,
        FinishSubmission,
        FinishDQ,
        FinishCountout,
        FinishInterference,
        FinishSuperFinisher
    }

    public enum BeatControl
    {
        WrestlerA,
        WrestlerB,
        Even,
        Contested  // rapid back-and-forth
    }

    public enum BeatIntensity
    {
        Low,
        Medium,
        High,
        Extreme
    }

    public enum BeatDuration
    {
        Brief,    // ~30s
        Short,    // ~1–2 min
        Medium,   // ~3–5 min
        Long,     // ~5–8 min
        Extended  // 8+ min
    }
}
