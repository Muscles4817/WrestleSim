using System.Text.Json.Serialization;

namespace WrestlingSim.Enums
{
    /// <summary>
    /// What the match is *about*. The one style decision a booker actually makes, and the
    /// thing that decides the shape of the beat sheet.
    ///
    /// This replaces <see cref="MatchType"/> as an input. That enum asked the player to
    /// declare how a match would be graded, which is not a decision a booker has — the
    /// promise a match makes comes from who is in it, what the feud has been and what the
    /// stipulation is, and it is kept or broken by how the match is booked. Nobody
    /// announces a style. See <c>MatchExpectation</c> for the derived half.
    ///
    /// A story picks the *pool and the flavour*. <see cref="MatchScale"/> picks the counts.
    /// The two together are the whole grammar, which is why the old structure library
    /// collapses into it: `Face-in-Peril` and `Technical Showcase` were the same eleven-beat
    /// skeleton with a different opening, a different rest hold and a different finish.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MatchStory
    {
        /// <summary>
        /// Two people who could each beat the other. Control changes hands, so both get a
        /// heat section — the only shape here where the loser controls a stretch of it.
        /// </summary>
        EvenContest,

        /// <summary>
        /// Doc 18 §2.3's spine. One shines, gets cut off, endures, and comes back. The
        /// Hogan/Cena formula and still the most reliable shape in wrestling.
        /// </summary>
        FaceInPeril,

        /// <summary>
        /// They do not care about winning so much as hurting each other. Brawling, revenge
        /// spots, and the feud erupting somewhere in the middle.
        /// </summary>
        Grudge,

        /// <summary>
        /// Mat work, limb targeting, a submission payoff. Slow to start on purpose: the
        /// technical ceiling needs the time.
        /// </summary>
        TechnicalExhibition,

        /// <summary>
        /// One of them should not be able to survive this. No shine, an early cut-off, a
        /// long beating and a great many hope spots — the drama is whether he lasts, not
        /// whether he wins.
        /// </summary>
        DavidAndGoliath,

        /// <summary>
        /// Spectacle. High spots from the bell, no control section to speak of, and the
        /// crowd carried on moves rather than psychology.
        /// </summary>
        Spectacle,

        /// <summary>
        /// One-sided by design. Somebody is being made to look unbeatable and the other is
        /// there to be beaten. A squash, or close to it.
        /// </summary>
        Showcase
    }

    /// <summary>
    /// How long, in the terms doc 18 §3.1 uses — and the reference's table is a table of
    /// *contents*, not of minutes, which is why this is an enum rather than a number.
    /// "15–25 min adds a second heat/comeback cycle and a real finishing stretch." So the
    /// length is the cycle count, and the minutes follow from it.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MatchScale
    {
        /// <summary>Doc 18 §3.1's 5–8 min. Shine, cut-off, short heat, comeback, finish.</summary>
        Opener,

        /// <summary>Television standard. One full cycle with a hope spot.</summary>
        Television,

        /// <summary>10–15 min. "The workhorse length." One cycle with a proper valley.</summary>
        Workhorse,

        /// <summary>15–25 min. The second cycle and a real finishing stretch.</summary>
        BigMatch,

        /// <summary>25–40 min. Everything, plus the rest the performers need to work it.</summary>
        Epic
    }

    /// <summary>
    /// How somebody should come out of the match, which is **not** the same question as who
    /// wins. Doc 20 runs feuds on the difference: a challenger who loses having taken the
    /// champion to the limit is worth more afterwards than one who wins a nothing match.
    ///
    /// The game already carries the consequence of this in <c>FinishWeight</c>; this is the
    /// booker's side of it, and it decides how the offence is shared out rather than who
    /// gets the fall.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Booking
    {
        /// <summary>They should look better leaving than arriving. Extra shine, extra near falls.</summary>
        Elevated,

        /// <summary>Nothing taken off them. They keep their comeback even in defeat.</summary>
        Protected,

        /// <summary>No thumb on the scale.</summary>
        Even,

        /// <summary>They are being taken down a peg. No shine, no near falls.</summary>
        Diminished
    }

    /// <summary>
    /// How it ends. Maps onto the finish beats the engine already grades, and onto
    /// <c>FinishWeight</c>, which decides whether a belt moves and how much a loss costs.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FinishKind
    {
        /// <summary>Pinned in the middle. The full statement.</summary>
        Clean,

        /// <summary>The finisher, emphatically, with nothing left in doubt.</summary>
        Dominant,

        /// <summary>They gave up. The only finish that says the loser chose to stop.</summary>
        Submission,

        /// <summary>A roll-up out of nowhere. They were not beaten, exactly.</summary>
        Stolen,

        /// <summary>Somebody else decided it.</summary>
        Interference,

        /// <summary>A disqualification. Nobody is beaten and the story keeps running.</summary>
        Disqualification,

        /// <summary>Counted out. The cheapest ending there is.</summary>
        CountOut
    }

    /// <summary>
    /// Somebody who is not in the match affecting it. Each one buys beats, and each one is
    /// a promise the finish has to keep — booking a manager at ringside and then never
    /// using them is a loaded gun that never goes off.
    /// </summary>
    [System.Flags]
    public enum OutsideFactor
    {
        None            = 0,

        /// <summary>A second at ringside, in position to matter.</summary>
        Manager         = 1 << 0,

        /// <summary>Somebody arrives who was not booked in the match.</summary>
        RunIn           = 1 << 1,

        /// <summary>The referee goes down and the rules go with him.</summary>
        RefereeBump     = 1 << 2
    }
}
