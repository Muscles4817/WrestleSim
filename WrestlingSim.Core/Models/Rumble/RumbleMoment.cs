namespace WrestlingSim.Models.Rumble
{
    /// <summary>
    /// The things a battle royal is actually for.
    ///
    /// Doc 18 §2.5 names three — "a vehicle for a spectacle, a surprise return, and one story
    /// told in eliminations" — and says the format is "judged on moments, not on work". This
    /// enum is that sentence made bookable: the vocabulary is deliberately short, because a
    /// format whose point is that a few things stand out is not improved by a longer list of
    /// things that can stand out.
    /// </summary>
    public enum RumbleMomentKind
    {
        /// <summary>
        /// Somebody comes out early and is still there at the end. The format's best story,
        /// and the only one that cannot be booked as a spot — it has to be earned across the
        /// whole match, which is why <see cref="Engine.RumbleEngine"/> reads it off the
        /// entry numbers rather than taking the booker's word for it.
        /// </summary>
        IronMan,

        /// <summary>
        /// A returning wrestler nobody announced. Doc 18 names this specifically, and it is
        /// the one moment whose value has nothing to do with the work: the pop is for the
        /// music hitting, and everything after it is a bonus.
        /// </summary>
        SurpriseReturn,

        /// <summary>One wrestler dumping several at once. The spectacle, in its plainest form.</summary>
        MassElimination,

        /// <summary>
        /// Two rivals meet in the ring and everything else stops. The moment a Rumble
        /// borrows from a feud rather than building one — worth what the feud is worth,
        /// which is why a showdown between two wrestlers with no history is worth little.
        /// </summary>
        Showdown,

        /// <summary>
        /// Hanging on with the feet not touching. Cheap, repeatable, and the crowd knows it —
        /// which is exactly why it decays faster than anything else here.
        /// </summary>
        NearElimination,

        /// <summary>
        /// A partner dumps their own. Costs the pairing something real, and doc 20's whole
        /// argument is that this is how a tag team's break-up should start.
        /// </summary>
        Betrayal
    }

    /// <summary>One booked moment, and who it happens to.</summary>
    public class RumbleMoment
    {
        public required RumbleMomentKind Kind { get; init; }

        /// <summary>
        /// Whoever the moment is about. One name for a surprise return or a mass
        /// elimination; two for a showdown or a betrayal, the doer first.
        /// </summary>
        public List<Wrestler> Cast { get; init; } = new();

        /// <summary>
        /// Roughly when it lands, as a share of the match, 0–1. The engine places it near
        /// here rather than exactly, because a booker asks for a showdown "late on", not at
        /// elimination fourteen.
        /// </summary>
        public double At { get; init; } = 0.5;
    }
}
