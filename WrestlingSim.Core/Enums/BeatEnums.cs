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

        /// <summary>
        /// A brief flurry from the wrestler in trouble that the heat cuts off again.
        ///
        /// Doc 18 §2.3 draws the face-in-peril structure as seven stages and puts this at
        /// the middle of it: "HOPE SPOTS — Brief comeback attempts that fail. Each raises
        /// tension", and then, in the paragraph explaining why the structure works, "the
        /// hope spots are **essential** — they keep the audience from giving up during the
        /// heat."
        ///
        /// It was missing. The tag formula has <see cref="NearTag"/> doing this job, and the
        /// singles formula — which doc 18 calls the dominant structure in wrestling — had
        /// nothing between the cut-off and the comeback at all. Every singles template in
        /// <see cref="Engine.MatchStructureLibrary"/> therefore ran heat straight into
        /// comeback, which is the one thing the reference says not to do.
        ///
        /// Not a small <see cref="Comeback"/>: a comeback releases the tension and a hope
        /// spot winds it tighter. The crowd pops for the offence and then watches it get cut
        /// off, which is why this reads as <see cref="ReactionKind.Tension"/> and takes the
        /// room *up* rather than down — the opposite of a near tag, where the reach fails
        /// and the building groans.
        /// </summary>
        HopeSpot,

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

        // ── Multi-man (doc 18 §2.5) ──────────────────────────────────────────

        /// <summary>
        /// The third man is removed — through a table, over the barricade, into the steps.
        ///
        /// The format's load-bearing beat. With three in the match somebody is doing nothing,
        /// and the entire craft is disposing of them plausibly and bringing them back at the
        /// right moment. It buys a window in which the remaining two can work uninterrupted,
        /// and a near fall inside that window is worth something a near fall outside it is not.
        /// </summary>
        DisposalSpot,

        /// <summary>
        /// A third party breaks up the fall. The multi-man equivalent of the tag save, and
        /// the reason every near fall in the format is cheaper than a singles near fall:
        /// nobody believes a cover until the third man is verifiably unable to reach it.
        /// </summary>
        PinBreak,

        /// <summary>
        /// A pin broken out of spite — the breaker could have taken the win and went after
        /// their rival instead.
        ///
        /// The signature beat of a multi-man match with a live feud in it, and the one that
        /// tells the crowd the grudge outranks the prize. Costs the spiter position, which
        /// is the point: it is not a good decision, it is a character decision.
        /// </summary>
        SpiteBreak,

        /// <summary>
        /// A winnable position abandoned to go after a rival instead. The cheaper cousin of
        /// the spite break, and what you book on the way to one.
        /// </summary>
        IgnoredOpportunity,

        /// <summary>
        /// Two rivals wipe each other out and neither can capitalise. The beat that sets up
        /// the third man crawling over — the classic multi-man finish, one beat early.
        /// </summary>
        MutualDestruction,

        /// <summary>
        /// **Two of them work the third.**
        ///
        /// Doc 18 §2.5 calls the temporary alliance and its betrayal "the format's single
        /// best story", and this is the first half of it. It is also the answer to the
        /// third-man problem that is not a disposal: nobody is on the floor wondering what
        /// to do, because all three are accounted for and two of them are busy.
        ///
        /// The engine had no way to express this at all. Every multi-man beat it carried was
        /// about *removing* somebody — disposal, pin break, elimination — and none of them
        /// could say that two people were working together, which is the thing a three-way
        /// opens with and turns on.
        /// </summary>
        Alliance,

        /// <summary>
        /// The alliance breaks, and whoever moves first has the advantage.
        ///
        /// Doc 18 §2.5: "the moment it breaks is the peak." It pays more than almost
        /// anything else in a multi-man match and it costs the betrayer nothing mechanically
        /// — unlike a <see cref="SpiteBreak"/>, this is the *good* decision as well as the
        /// dramatic one, which is exactly why the audience spends the whole alliance waiting
        /// for it.
        /// </summary>
        Betrayal,

        /// <summary>
        /// A fall that removes somebody from the match instead of ending it.
        ///
        /// Doc 18 §2.5: elimination "solves the third-man problem by construction, which is
        /// why it scales where a four-way does not" — you cannot ask where the fourth man
        /// went once he has been pinned and sent to the back. What it costs is the thing a
        /// three-way's whole tension rests on, that any fall could be the last, and what it
        /// buys instead is the order: "the drama moves from the fall to the *order* of
        /// eliminations".
        ///
        /// So this beat is not a finish. It is worked, it takes somebody out, and the match
        /// carries on with fewer people in it.
        /// </summary>
        Elimination,

        // Finishes
        FinishClean,
        FinishRollup,
        FinishSubmission,
        FinishDQ,
        FinishCountout,
        FinishInterference,
        FinishSuperFinisher
    }

    /// <summary>
    /// Whose beat this is. Named for sides, not for wrestlers — in a tag match it is whichever
    /// of that side's members is legal.
    ///
    /// `SideC` and `SideD` are appended rather than replacing the A/B names, so the several
    /// hundred existing structure definitions keep reading the way they were written. The
    /// value is resolved in exactly one place, <see cref="MatchPlan.MatchPlan.SideIndex"/>;
    /// there is no second copy, because the risk with adding members to an enum that is
    /// compared with `==` rather than switched on is precisely that `!= WrestlerA` silently
    /// means "side B" in a hundred places.
    /// </summary>
    public enum BeatControl
    {
        WrestlerA,
        WrestlerB,
        Even,
        Contested,  // rapid back-and-forth
        SideC,
        SideD
    }

    public enum BeatIntensity
    {
        Low,
        Medium,
        High,
        Extreme
    }

    public static class BeatIntensityExtensions
    {
        /// <summary>
        /// The class an intensity is painted with, warming as it climbs.
        ///
        /// A beat sheet is a shape, and the shape is what the intensities do down the page —
        /// a valley, a climb, a peak at the finish. Rendered in one colour it is a list of
        /// words, and the booker has to read four of them to see what the eye should have got
        /// in one pass.
        /// </summary>
        public static string Tone(this BeatIntensity i) => i switch
        {
            BeatIntensity.Extreme => "heat--extreme",
            BeatIntensity.High    => "heat--high",
            BeatIntensity.Medium  => "heat--medium",
            _                     => "heat--low"
        };
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
