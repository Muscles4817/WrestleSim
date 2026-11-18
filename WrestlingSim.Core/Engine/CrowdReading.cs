using WrestlingSim.Enums;
using WrestlingSim.Models;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// How the crowd actually takes somebody, set beside how they are booked.
    ///
    /// The booked alignment is the player's decision and the engine never changes it
    /// (docs/gimmick-plan.md §0A). What the crowd does with it is
    /// <see cref="PerformerProfile.Favour"/>: alignment states the intent, and how much the
    /// crowd likes the person can overrule it — the cool heel who gets cheered, the babyface
    /// the crowd has turned on (doc 19 §4.2). That gap is information a booker acts on; this
    /// puts it in words.
    /// </summary>
    public static class CrowdReading
    {
        /// <summary>At or above this the crowd is cheering them.</summary>
        public const double Cheering = 0.6;

        /// <summary>At or below this the crowd is booing them.</summary>
        public const double Booing = 0.4;

        public enum Reaction { Cheering, Mixed, Booing }

        public static Reaction Of(double favour) =>
            favour >= Cheering ? Reaction.Cheering : favour <= Booing ? Reaction.Booing : Reaction.Mixed;

        public static Reaction Of(Wrestler w, DateOnly? on = null) => Of(new PerformerProfile(w, on).Favour);

        /// <summary>
        /// True when the crowd has gone the other way from the booking: a heel being cheered or
        /// a babyface being booed. A tweener cannot be crossed — they are whatever the room decides.
        /// </summary>
        public static bool Crossed(Alignment? booked, Reaction reaction) =>
            (booked == Alignment.Heel && reaction == Reaction.Cheering) ||
            (booked == Alignment.Face && reaction == Reaction.Booing);

        public static string Words(Reaction r) => r switch
        {
            Reaction.Cheering => "The crowd cheers them",
            Reaction.Booing   => "The crowd boos them",
            _                 => "The crowd is split on them"
        };

        /// <summary>What the gap means, for the profile, or null when the crowd is doing what the booking asks.</summary>
        public static string? Meaning(Alignment? booked, Reaction r) => (booked, r) switch
        {
            (Alignment.Heel, Reaction.Cheering) => "A heel the crowd cheers. Their matches lose the person the crowd wants beaten — or the crowd is telling you who it wants to cheer.",
            (Alignment.Face, Reaction.Booing)   => "A babyface the crowd boos. They are being rejected in the role; the crowd will not get behind them as booked.",
            (Alignment.Heel, Reaction.Mixed)    => "A heel the crowd half likes. The heat is softer than the booking wants.",
            (Alignment.Face, Reaction.Mixed)    => "A babyface the crowd is lukewarm on. The cheers are softer than the booking wants.",
            _                                   => null
        };
    }
}
