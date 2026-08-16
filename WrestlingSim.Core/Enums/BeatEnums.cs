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
