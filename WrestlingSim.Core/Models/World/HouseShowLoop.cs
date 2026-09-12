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

        /// <summary>
        /// How many a side out there: 1 for singles, 2 for a tag run.
        ///
        /// **The protected rep, which is the thing the road was missing.** The sharpness
        /// model's own answer to "how do you get somebody back to match fitness without
        /// putting them on television before they are ready" is reps at less than full
        /// exposure, and a tag is exactly that — you are still out there, still taking the
        /// double team, but you spend a good part of the match on the apron. Until this, a
        /// loop was singles only, so the one setting in wrestling that exists to protect
        /// somebody was the one the road could not book.
        ///
        /// This is the run's **default**, and <see cref="InTags"/> overrides it for
        /// individuals — see there for why a mixed run is the real shape of a week.
        ///
        /// The share of the work it buys is <c>RingCondition.WorkShare</c>, which is the same
        /// number the televised card uses, so a tag on the road and a tag on television cost
        /// the same body the same thing.
        /// </summary>
        public int SideSize { get; set; } = 1;

        /// <summary>
        /// Names that work tags whatever the run's default is. **The mixed loop.**
        ///
        /// The first version made the format the run's and said so: a house show loop is
        /// booked as a cast and a number of towns, not as a card, so one chip for everybody
        /// was the shape that matched. But a real week is not one format — a loop is
        /// protected spots down the card and singles at the top of it, on the same night, and
        /// a run that can only be all of one is a run that cannot do the job the tag format
        /// was added for. The booker sending eight people out is usually protecting two of
        /// them, not eight.
        ///
        /// It is still not a card, which is the line this has to stay on the right side of.
        /// The decision is "who am I protecting", which is one tap on a name the booker has
        /// already picked — not who faces whom, not who partners whom, and not in what order.
        /// The engine bills bodies and never builds a match out here, so a flag on a body is
        /// the whole of what it can honestly read.
        /// </summary>
        public List<Wrestler> InTags { get; set; } = new();

        /// <summary>How many a side <paramref name="w"/> works, default or override.</summary>
        public int SideSizeFor(Wrestler w) => InTags.Contains(w) ? 2 : SideSize;

        /// <summary>
        /// The biggest format anybody on this run is working, which is what decides how many
        /// bodies it takes. Reads the cast rather than <see cref="InTags"/>, so a name marked
        /// and then dropped does not keep demanding room for a match nobody is in.
        /// </summary>
        public int LargestSide =>
            Cast.Count == 0 ? Math.Max(1, SideSize) : Cast.Max(SideSizeFor);

        /// <summary>
        /// A run needs two full sides of its largest format. Singles has always needed two
        /// bodies; a tag needs four, and booking three people into one is not a tag with
        /// somebody sitting out, it is a card that has not been finished.
        ///
        /// It is the *largest* and not the default, because protecting one person on an
        /// otherwise singles run still means somebody out there is working a tag match, and a
        /// tag match takes four bodies however few of them are the one being looked after.
        /// </summary>
        public bool IsBookable =>
            SideSize >= 1 && Cast.Count >= LargestSide * 2 && Towns >= 1;

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
