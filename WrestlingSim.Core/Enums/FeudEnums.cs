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

    public static class FeudIntensityExtensions
    {
        /// <summary>
        /// The badge a feud's heat is shown in.
        ///
        /// This map lived privately inside `FeudUpdates.razor` while the match builder
        /// rendered the same five values with no colour at all, which is the two-copies
        /// problem one step before it happens: one screen had the rule and the other did not
        /// have it yet.
        ///
        /// Nuclear is the warning colour rather than the best one on purpose. Doc 20 §6 has a
        /// feud at nuclear as something that has to be *paid off soon* — it is the state with
        /// a clock on it, not the state to sit in.
        /// </summary>
        public static string Badge(this FeudIntensity intensity) => intensity switch
        {
            FeudIntensity.Nuclear  => "badge--heel",
            FeudIntensity.Hot      => "badge--gold",
            FeudIntensity.Building => "badge--cyan",
            FeudIntensity.Cold     => "badge--muted",
            _                      => "badge--muted"
        };
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
