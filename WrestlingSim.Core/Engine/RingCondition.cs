using WrestlingSim.Enums;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// What working takes out of a wrestler, and what not working does to them.
    ///
    /// **Two meters that pull against each other**, which is the whole design and the
    /// reason this is one file rather than two:
    ///
    /// <code>
    ///                works a match      rests a week
    ///   Fatigue          ▲ up             ▼ down
    ///   Sharpness        ▲ up             ▼ down
    /// </code>
    ///
    /// Rest fixes one and breaks the other, so there is no correct amount of time off —
    /// only a trade being made about a particular wrestler. A booker who rests their main
    /// eventer for two months gets them back fresh and rusty; one who works them every week
    /// gets them sharp and cooked. Neither is the right answer, which is what makes it a
    /// decision.
    ///
    /// **Danger is not fatigue.** Nothing in here reads match type or stipulation. What
    /// costs a wrestler is how long they were out there and how fast they went — a
    /// twelve-minute sprint at full tilt can take more out of somebody than a
    /// twenty-six-minute mat classic, and a ladder match is dangerous without being
    /// especially tiring. Injury risk and exertion are different axes that happen to
    /// correlate sometimes, and modelling them as one thing would get both wrong.
    ///
    /// Every rule here is a pure function of numbers, so each can be asserted as itself
    /// rather than inferred from a career that ran.
    /// </summary>
    public static class RingCondition
    {
        // ── The meters ───────────────────────────────────────────────────────

        /// <summary>Fatigue at which a wrestler should not be booked without a reason.</summary>
        public const double CookedThreshold = 70.0;

        /// <summary>Sharpness below which the ring rust is visible to an audience.</summary>
        public const double RustyThreshold = 55.0;

        // ── What a match costs ───────────────────────────────────────────────

        /// <summary>
        /// How hard a beat is being worked, per minute. The pace axis.
        ///
        /// Not linear across the four steps: the gap between High and Extreme is where
        /// wrestlers get hurt and where they run out of air, and it is much wider than the
        /// gap between Low and Medium.
        /// </summary>
        public static double IntensityWeight(BeatIntensity intensity) => intensity switch
        {
            BeatIntensity.Low     => 0.55,
            BeatIntensity.Medium  => 1.00,
            BeatIntensity.High    => 1.50,
            BeatIntensity.Extreme => 2.15,
            _                     => 1.00
        };

        /// <summary>
        /// Mean intensity per minute across a plan — the pace it was worked at. 1.0 is a
        /// match of straight Medium beats.
        ///
        /// A minute-weighted mean rather than a plain one, so eight brief Extreme spots do
        /// not read the same as eight minutes of them.
        /// </summary>
        public static double Pace(IEnumerable<MatchBeat> beats)
        {
            double minutes = 0, exertion = 0;
            foreach (var beat in beats)
            {
                minutes  += beat.DurationMinutes;
                exertion += IntensityWeight(beat.Intensity) * beat.DurationMinutes;
            }
            return minutes <= 0 ? 1.0 : exertion / minutes;
        }

        /// <summary>
        /// How much a style suffers from <em>pace</em>, around 1.0.
        ///
        /// A style is not simply tiring or not tiring — it has a shape over pace and
        /// length, and the two are different. What is expensive here is offence that has to
        /// be thrown and taken at speed.
        /// </summary>
        public static double PaceLoad(WrestlingStyle style) => style switch
        {
            // Every spot is a full-body landing, and a sprint gives you nowhere to hide.
            WrestlingStyle.HighFlyer => 1.30,
            // Throwing and eating strikes at that rate is its own kind of tiring.
            WrestlingStyle.Striker   => 1.25,
            WrestlingStyle.Brawler   => 1.00,
            WrestlingStyle.Grappler  => 0.80,
            // Big men are not fast, and being asked to go fast is not what costs them.
            WrestlingStyle.Powerhouse=> 0.75,
            // The most economical style there is, which is why technical wrestlers could
            // always work every night of the week.
            WrestlingStyle.Technical => 0.70,
            _                        => 1.00
        };

        /// <summary>
        /// How much a style suffers from <em>length</em>, around 1.0.
        ///
        /// The other half, and it does not rank the same way. This is the axis on which big
        /// men gas: carrying that much weight for twenty-five minutes is the killer, and it
        /// has almost nothing to do with how fast the match was.
        /// </summary>
        public static double VolumeLoad(WrestlingStyle style) => style switch
        {
            WrestlingStyle.Powerhouse=> 1.45,
            WrestlingStyle.HighFlyer => 1.05,
            WrestlingStyle.Brawler   => 1.00,
            WrestlingStyle.Grappler  => 0.95,
            WrestlingStyle.Striker   => 0.80,
            WrestlingStyle.Technical => 0.70,
            _                        => 1.00
        };

        /// <summary>Match length at which the volume term reads as 1.0. A long television match.</summary>
        private const double ReferenceMinutes = 20.0;

        /// <summary>
        /// Turns each half of the exertion into meter points. Measured against doc 06's
        /// hundred-to-hundred-and-fifty dates a year for a full-timer, not guessed — the
        /// first pair of values put a mid-card technical wrestler and a high-flyer both at
        /// a pinned 100 on the same schedule, which is a meter that has stopped saying
        /// anything.
        /// </summary>
        private const double PaceScale   = 0.20;
        private const double VolumeScale = 0.18;

        /// <summary>
        /// Fatigue points a match costs one wrestler.
        ///
        /// The two style loads are added rather than multiplied, so each can dominate on
        /// its own ground: a powerhouse in a thirty-minute epic is punished by the volume
        /// term whatever the pace, and a high-flyer in a twelve-minute sprint by the pace
        /// term whatever the length. Multiplying would have made a long slow match cheap
        /// for everybody, which is the one reading that is definitely wrong.
        ///
        /// <paramref name="share"/> is how much of their side's work this wrestler did —
        /// see <see cref="WorkShare"/>. <paramref name="conditioning"/> is the engine's
        /// factor, around 1.0; a better gas tank pays less for the same match.
        /// </summary>
        public static double MatchCost(double minutes, double pace, WrestlingStyle style,
                                       double share = 1.0, double conditioning = 1.0)
        {
            if (minutes <= 0) return 0.0;

            // **Pace is squared and volume is not**, and that one asymmetry is the whole
            // reason a twelve-minute sprint can cost more than a match twice its length.
            //
            // It is also the physiology: going flat out is anaerobic and the bill for it
            // grows far faster than the clock does, while a long match worked at a sensible
            // rate is something a conditioned body can keep paying for. The first version
            // multiplied both halves by the running time, which meant length dominated
            // everything and a sprint was always the cheap option — the exact reading this
            // system exists to deny.
            double paced  = minutes * PaceLoad(style)   * Math.Pow(Math.Max(0, pace), 2) * PaceScale;
            double lasted = minutes * VolumeLoad(style) * (minutes / ReferenceMinutes)   * VolumeScale;

            return Math.Max(0, (paced + lasted) * Math.Clamp(share, 0, 1)
                              / Math.Clamp(conditioning, 0.55, 1.35));
        }

        /// <summary>
        /// How much of a side's work one member does.
        ///
        /// Not 1/n. Standing on the apron is a rest and that is the whole reason tag
        /// wrestling is survivable at volume — but you are still out there, still taking
        /// the double team and still running in for the save, so the fall-off is well short
        /// of proportional.
        /// </summary>
        public static double WorkShare(int sideSize) =>
            sideSize <= 1 ? 1.0 : 0.45 + 0.55 / sideSize;

        // ── What rest gives back ─────────────────────────────────────────────

        /// <summary>
        /// Fatigue recovered in one day off.
        ///
        /// Proportional to how tired they are plus a floor, so recovery is fast from deep
        /// fatigue and slow from nearly-fresh — which is why a week off does most of the
        /// work of a month off, and why there is a point past which resting somebody
        /// further is only costing them sharpness.
        /// </summary>
        public static double RecoveryPerDay(double fatigue, double conditioning) =>
            (1.6 + Math.Max(0, fatigue) * 0.055) * Math.Clamp(conditioning, 0.55, 1.35);

        // ── What rest takes away ─────────────────────────────────────────────

        /// <summary>
        /// How well somebody keeps themselves ring-ready away from the ring, 0–1.
        ///
        /// Psychology and ring IQ, because that is what this model has to say "veteran"
        /// with — doc 15 §4 has psychology rising into a wrestler's forties while
        /// athleticism declines from around twenty-eight, so a high reading here *is* the
        /// game's picture of somebody who has been doing it a long time.
        ///
        /// It is also the right stat on the merits. What a veteran keeps at home is the
        /// knowing — where to be, when to move, how to lay a match out. What goes is the
        /// timing with another body in the ring, and no amount of understanding replaces
        /// live reps for that.
        /// </summary>
        public static double SelfMaintenance(int psychology, int ringIQ) =>
            Math.Clamp((psychology * 0.6 + ringIQ * 0.4) / 100.0 * 0.9 - 0.09, 0.0, 0.82);

        /// <summary>
        /// The sharpness a wrestler settles at when they are not working at all.
        ///
        /// A **floor**, not a ceiling, and the distinction matters. Nobody decays to
        /// nothing: somebody who has worked for twenty years and keeps themselves in shape
        /// stops falling at "fine" — able to go out and have a good match without
        /// embarrassing anybody. What they cannot reach from home is their own best, and
        /// the last stretch to it is only ever bought with live matches.
        ///
        /// A wrestler whose game is speed and timing settles a long way below that, which
        /// is why the young high-flyer needs to be out there every week and the old hand
        /// does not.
        ///
        /// **Recalibrated, because the first band was far too generous.** It ran 30–75 against
        /// a roster whose self-maintenance spans 0.30 to 0.77, so the median wrestler settled
        /// at 62 and only 21% of the roster could ever read rusty however long they sat. The
        /// meter existed and its threshold was unreachable for four names in five. Measured on
        /// the shipped roster, the band below settles the median at 43 and leaves only the
        /// most diligent handful above the rusty line — which is the right shape, because
        /// keeping yourself ring-ready without ever being in a ring is the exception and the
        /// old numbers made it the rule.
        /// </summary>
        public static double RestingFloor(double selfMaintenance) =>
            Math.Max(0, Math.Clamp(selfMaintenance, 0, 1) * 80.0 - 4.0);

        /// <summary>
        /// Sharpness lost in one day away, moving toward <see cref="RestingFloor"/>.
        ///
        /// Geometric rather than linear, so the first weeks off cost the most and a
        /// wrestler who has been away a year is not still falling.
        /// </summary>
        /// <summary>Share of the gap to the floor that a day away closes.</summary>
        public const double RustFraction = 0.030;

        public static double RustPerDay(double sharpness, double selfMaintenance)
        {
            double floor = RestingFloor(selfMaintenance);
            if (sharpness <= floor) return 0.0;
            return (sharpness - floor) * RustFraction;
        }

        // ── What working gives back ──────────────────────────────────────────

        /// <summary>
        /// Sharpness gained from having a match.
        ///
        /// **A rep and a workout, and the rep is the bigger half.** What comes back in a ring
        /// is timing with another body, and you get that from having had the match at all —
        /// so the first term is simply "you were out there", saturating at around eight
        /// minutes because a three-minute squash is not a night's work and a twenty-minute
        /// match is not two.
        ///
        /// That shape is the whole reason a protected spot is where somebody is brought back.
        /// A tag is a rep at a fraction of the exposure: <paramref name="share"/> scales the
        /// workout half and not the rep half, because standing on the apron waiting to be
        /// tagged is still a night of timing a hot tag. And a match worked at a sensible pace
        /// still counts, because the second term is the smaller one — a returning wrestler
        /// does not have to go out and have a war to get himself back, which is fortunate,
        /// since going out and having a war is exactly what he is not ready for.
        ///
        /// Measured against the shipped roster: an eight-minute tag at a moderate pace is
        /// worth about four fifths of what a fifteen-minute singles main event is worth, at
        /// roughly half the fatigue and a fraction of the injury risk.
        /// </summary>
        public static double SharpnessGain(double minutes, double pace,
                                           double selfMaintenance, double share = 1.0)
        {
            if (minutes <= 0) return 0.0;

            double rep    = RepValue * Math.Min(1.0, minutes / RepMinutes);
            double demand = minutes * (0.45 + Math.Max(0, pace) * 0.35);

            // **The two routes are inverses.** The young one's body answers a rep harder —
            // more back per match — and the old hand's answers less, because what he is
            // shaking off is timing with another body and knowing where to be is not the
            // same as being there.
            //
            // What that produces once rust is in the picture is the thing worth having, and
            // it is not the thing this line says on its own: because the youngster also
            // falls fastest, he needs *more* matches a week to hold a high reading, while
            // the veteran holds his on fewer. Measured, working twice a week from a
            // sharpness of 40, the veteran is back to razor in fourteen weeks and the
            // rookie is still climbing after a year — not because a rep does less for him
            // but because five days off does more against him. That is the young wrestler
            // needing the reps week in and week out, arrived at rather than asserted.
            double responds = 1.5 - Math.Clamp(selfMaintenance, 0, 1) * 0.8;

            return Math.Clamp(
                (rep + demand * WorkoutScale * Math.Clamp(share, 0, 1)) * responds, 0, 11.0);
        }

        /// <summary>What having had a match at all is worth, before the work in it.</summary>
        private const double RepValue = 4.4;

        /// <summary>Minutes at which a match counts as a full night's reps.</summary>
        private const double RepMinutes = 8.0;

        /// <summary>What the work on top of the rep is worth, per unit of demand.</summary>
        private const double WorkoutScale = 0.30;

        // ── What the meters do to a performance ──────────────────────────────

        /// <summary>
        /// What fatigue does to the gas tank, 0.72–1.0.
        ///
        /// Applied to <see cref="PerformerProfile.Conditioning"/>, which is the single
        /// highest-leverage place to put it: conditioning already drives the late-match
        /// fade, the penalty for booking a long match, and the extra wear of being
        /// outnumbered. One hook, three mechanisms, and no new rule about what being tired
        /// feels like in a match — the engine already knew.
        /// </summary>
        public static double ConditioningFactor(double fatigue) =>
            1.0 - Math.Clamp(fatigue, 0, 100) / 100.0 * 0.28;

        /// <summary>
        /// What rust does to the craft, 0.78–1.0.
        ///
        /// Applied to workrate and ring psychology — timing and crispness — and
        /// deliberately **not** to connection. Being rusty and being forgotten are
        /// different things, and the game already models forgotten: absence bleeds overness
        /// (doc 17 §3.9). Keeping rust off the crowd axis is what lets a returning legend
        /// be white hot and unable to go, which is the actual problem with returning
        /// legends.
        /// </summary>
        public static double CraftFactor(double sharpness) =>
            0.78 + Math.Clamp(sharpness, 0, 100) / 100.0 * 0.22;

        /// <summary>
        /// Where a wrestler's sharpness starts when a career begins.
        ///
        /// **Not 100 for everybody.** The property defaults to razor because a wrestler built
        /// in a test should not have to be warmed up first, but a whole roster at the ceiling
        /// on day one means every rep in the booker's first months is worth literally nothing
        /// — measured in the browser, six wrestlers on a three-town run came home with +0.0
        /// sharpness each and only the fatigue, which makes the road look broken on the one
        /// occasion a new player is most likely to try it.
        ///
        /// The seed is what a weekly schedule settles somebody at, because that is what the
        /// roster has been doing: a spread from the low sixties to the high eighties, nobody
        /// rusty and nobody finished. Which is the state the game wants to open in — the road
        /// is worth using from the first week, and it is worth more to some of them than to
        /// others.
        /// </summary>
        public static double OpeningSharpness(double selfMaintenance)
        {
            double floor = RestingFloor(selfMaintenance);

            // Settled on one match a week, solved rather than simulated: a week's rust from S
            // is (S - floor) * (1 - 0.97^7), and it balances one rep.
            double weeklyRust = 1 - Math.Pow(1 - RustFraction, 7);
            double rep = SharpnessGain(15, 1.0, selfMaintenance);

            return Math.Clamp(floor + rep / weeklyRust, 0, 100);
        }

        // ── Applying ─────────────────────────────────────────────────────────

        /// <summary>
        /// One day of not wrestling. The only mutating method here, and it moves both
        /// meters at once because that is the point: the same day pays back fatigue and
        /// costs sharpness.
        /// </summary>
        public static void ApplyDayOff(Models.Wrestler wrestler)
        {
            double conditioning = new PerformerProfile(wrestler).BaseConditioning;

            wrestler.Fatigue = Math.Clamp(
                wrestler.Fatigue - RecoveryPerDay(wrestler.Fatigue, conditioning), 0, 100);

            double upkeep = SelfMaintenance(
                wrestler.Mental?.Psychology ?? 70, wrestler.Mental?.RingIQ ?? 70);

            wrestler.Sharpness = Math.Clamp(
                wrestler.Sharpness - RustPerDay(wrestler.Sharpness, upkeep), 0, 100);
        }

        /// <summary>The reading for a particular wrestler, so callers do not re-derive it.</summary>
        public static string? WarningFor(Models.Wrestler w) => Warning(w.Fatigue, w.Sharpness);

        // ── Words for the booker ─────────────────────────────────────────────

        /// <summary>A reading of the two meters, or null when there is nothing to say.</summary>
        public static string? Warning(double fatigue, double sharpness)
        {
            bool cooked = fatigue >= CookedThreshold;
            bool rusty  = sharpness <= RustyThreshold;

            return (cooked, rusty) switch
            {
                (true, true)  => "cooked and rusty — they have been worked into the ground and it still is not sharp",
                (true, false) => "cooked — they need time off more than they need this match",
                (false, true) => "ring rusty — they need the reps, and this match will not be their best",
                _             => null
            };
        }
    }
}
