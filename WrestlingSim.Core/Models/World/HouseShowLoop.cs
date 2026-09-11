using WrestlingSim.Enums;

namespace WrestlingSim.Models.World
{
    /// <summary>
    /// A run of untelevised towns, booked as a cast rather than as a card.
    ///
    /// **The reps the sharpness meter has always been asking for.** `RingCondition` says most
    /// of the roster needs more than one match a week to hold a professional reading, and until
    /// this existed the only way to get a match was a televised one on a card booked beat by
    /// beat. A booker who wanted to bring somebody back to match fitness had nothing to do it
    /// with except put them on television before they were ready, which is the exact mistake
    /// the meter exists to describe.
    ///
    /// Booked as a list of names and a number of towns, because that is the decision. Nobody
    /// lays out a house show beat by beat, and a game that asked you to would be asking you to
    /// spend the evening on the part of the week that does not matter.
    ///
    /// **Nothing consequential happens on it**, which is <see cref="ShowType.HouseShow"/>'s
    /// own definition: the television audience was not there. So a loop moves condition and
    /// nothing else — no overness, no momentum, no feud heat, no titles. It does not even count
    /// as an appearance against <c>HeatEconomy</c>'s absence clock, and that is the sharpest
    /// thing the format says: you can work six towns a week and still be forgotten, because
    /// being *in the ring* and being *on television* are different currencies. Working the
    /// loop keeps you able to go. It does not keep you over.
    /// </summary>
    public class HouseShowLoop
    {
        /// <summary>Everybody on the run. Each of them works every town.</summary>
        public List<Wrestler> Cast { get; set; } = new();

        /// <summary>
        /// How many towns. Each is a night's work for everybody in the cast, so this is the
        /// dial that turns a loop from a top-up into a grind.
        /// </summary>
        public int Towns { get; set; } = 3;

        /// <summary>
        /// How hard they are working out there. A house show is usually a notch below
        /// television and that is why the loop is where you put somebody who is not ready —
        /// but a booker who runs them hard every night gets the fatigue and the injuries that
        /// go with it.
        /// </summary>
        public BeatIntensity Pace { get; set; } = BeatIntensity.Medium;

        /// <summary>Minutes each of them works a night.</summary>
        public int MinutesPerNight { get; set; } = 12;

        public bool IsBookable => Cast.Count >= 2 && Towns >= 1;

        /// <summary>Total nights of work this run asks of one wrestler.</summary>
        public int NightsEach => Math.Max(0, Towns);

        /// <summary>
        /// Nights this wrestler actually works, given who was on television the same night.
        ///
        /// **A televised match costs one town, not the whole run.** The first version of the
        /// mixed night took anybody on the card out of the travelling cast entirely, and that
        /// is not how a week works: somebody wrestles television on the Monday and is in a
        /// high school gym on the Friday. Dropping them from the run made television and the
        /// road mutually exclusive, which breaks the one route back to match fitness the
        /// sharpness meter actually describes — protected television plus live local reps.
        /// One night off the run is the real cost, because that is the night they were
        /// somewhere else.
        /// </summary>
        public int NightsFor(Wrestler w, IReadOnlySet<Wrestler> onTelevision) =>
            Math.Max(0, NightsEach - (onTelevision.Contains(w) ? 1 : 0));
    }
}
