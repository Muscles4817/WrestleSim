namespace WrestlingSim.Enums
{
    public enum FeudIntensity
    {
        None,
        Cold,
        Building,
        Hot,
        Nuclear
    }

    public enum FeudHistoryTag
    {
        Betrayal,
        InjuryAngle,
        TitleStolen,
        PersonalInsult,
        FamilyInvolved,
        ManagerConflict,
        PriorMatch,
        ChampionshipRivalry,
        FactionConflict
    }

    public enum FeudalResonanceType
    {
        Callback,    // references a prior moment in the feud
        Escalation,  // the feud boiling over
        Revenge,     // doing to them what was done to you
        ThirdParty   // pulling in someone connected to the feud
    }
}
