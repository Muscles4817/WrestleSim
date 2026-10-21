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

    public static class FeudHistoryTags
    {
        /// <summary>
        /// What a tag is called on screen.
        ///
        /// The enum name used to be printed directly, so a player who booked an exhibition
        /// match was shown "+ PriorMatch" on the result — an identifier with the spaces
        /// taken out, which is the single clearest sign that a screen is showing you the
        /// inside of the program rather than the outside of the game.
        /// </summary>
        public static string Label(this FeudHistoryTag tag) => tag switch
        {
            FeudHistoryTag.Betrayal            => "A betrayal",
            FeudHistoryTag.InjuryAngle         => "An injury angle",
            FeudHistoryTag.TitleStolen         => "A stolen title",
            FeudHistoryTag.PersonalInsult      => "Personal insults",
            FeudHistoryTag.FamilyInvolved      => "Family involved",
            FeudHistoryTag.ManagerConflict     => "A manager in it",
            FeudHistoryTag.PriorMatch          => "A previous match",
            FeudHistoryTag.ChampionshipRivalry => "A title rivalry",
            FeudHistoryTag.FactionConflict     => "Faction warfare",
            _                                  => tag.ToString()
        };
    }

    public enum FeudalResonanceType
    {
        Callback,    // references a prior moment in the feud
        Escalation,  // the feud boiling over
        Revenge,     // doing to them what was done to you
        ThirdParty   // pulling in someone connected to the feud
    }
}
