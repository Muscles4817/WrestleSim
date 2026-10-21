using WrestlingSim.Enums;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// What getting older does to a wrestler. Doc 15 §4.
    ///
    /// Wrestling has an unusual career arc because two of its three core competencies
    /// *improve* with age while the third declines:
    ///
    /// <code>
    ///                     24      28      32      36      40      44      48
    ///   Athleticism      peak ───── ▼ ───────── ▼▼ ──────── ▼▼▼ ─────── ▼▼▼▼
    ///   Ring psychology   ▲▲ ────── ▲ ───────── peak ─────── hold ─────── ▼
    ///   Connection        (not age at all — tenure, and it takes a decade)
    /// </code>
    ///
    /// **Everything here is a ratio against the roster baseline, not an absolute level**,
    /// and that is the whole design. `Wrestlers.json` was authored by hand with doc 15 §4
    /// already applied — Randy Orton at 46 is Agility 58 and Psychology 96, Bron Breakker
    /// at 29 is Agility 84 and Psychology 62. Those readings are *correct for their age*.
    /// Applying an absolute curve on top would price every veteran's age twice and quietly
    /// re-balance a roster somebody tuned on purpose.
    ///
    /// So a wrestler's authored stats are taken as true as of <see cref="RosterBaselineYear"/>,
    /// and what the curve returns is how far they have moved *since then*. On day one of a
    /// save starting in the baseline year every drift is exactly 1.000, so the engine
    /// produces bit-identical matches to the ones it produced before this existed. The
    /// curve only begins to bite as the save's clock runs, which is the only time a career
    /// arc is a thing the player can observe anyway.
    ///
    /// Age is held to **year precision** — see <see cref="Models.Wrestler.BornIn"/> for why
    /// — and every function here is pure so it can be asserted as itself.
    /// </summary>
    public static class AgeCurve
    {
        /// <summary>
        /// The year the shipped roster's attributes are taken to be true as of.
        ///
        /// Moving this re-bases every drift in the game, so it moves when the roster is
        /// re-authored and not otherwise.
        /// </summary>
        public const int RosterBaselineYear = 2026;

        // ── The three curves ─────────────────────────────────────────────────

        /// <summary>
        /// Raw athletic capacity by age, peaking across 24–28 and falling away after.
        ///
        /// The twenty-year-old is below peak but not *declining* — doc 15's 18–23 band is
        /// "physically capable, no idea what they're doing", so what is missing there is
        /// craft rather than body. That is why style does not scale the young end.
        /// </summary>
        private static readonly (int Age, double Value)[] AthleticRows =
        [
            (20, 0.93), (24, 1.00), (28, 1.00), (32, 0.95), (36, 0.86),
            (40, 0.76), (44, 0.65), (48, 0.55), (52, 0.46)
        ];

        /// <summary>
        /// Ring psychology by age. Rises steeply through the twenties, peaks around 40 per
        /// doc 15 §4, and then holds rather than falling — what a veteran loses is the
        /// body, not the understanding.
        /// </summary>
        private static readonly (int Age, double Value)[] PsychRows =
        [
            (20, 0.62), (24, 0.76), (28, 0.87), (32, 0.94), (36, 0.99),
            (40, 1.00), (44, 1.00), (48, 0.99), (52, 0.97)
        ];

        /// <summary>
        /// Audience connection by **tenure in years, not age**.
        ///
        /// Doc 15 §4.1: drawing power "is a function of accumulated audience relationship,
        /// which takes a decade to build". That is why the curve is nearly flat after
        /// twelve years and why it is keyed to the debut rather than the birthday — this is
        /// the mechanism behind the industry's core scheduling tension, that performers are
        /// most valuable at the point their bodies are least able to sustain the schedule.
        /// </summary>
        private static readonly (int Years, double Value)[] ConnectionRows =
        [
            (0, 0.68), (2, 0.79), (5, 0.89), (8, 0.95), (12, 1.00), (25, 1.00)
        ];

        /// <summary>
        /// Injury risk by age, as a multiplier on the per-beat roll.
        ///
        /// **A U, not a ramp.** Doc 15 §3.1 gives "age over 35: 1.5–2× and rising sharply",
        /// and §4's band table gives the 18–23s the "highest injury rate from
        /// inexperience". Both ends of a career are more dangerous than the middle, for
        /// opposite reasons, and a monotonic curve would miss half of that.
        /// </summary>
        private static readonly (int Age, double Value)[] InjuryRows =
        [
            (18, 1.38), (21, 1.16), (25, 0.96), (28, 0.95), (32, 1.10),
            (35, 1.28), (38, 1.58), (42, 1.92), (46, 2.32), (50, 2.70)
        ];

        /// <summary>
        /// How fast a body pays fatigue back, by age. Nothing in doc 15 quantifies this;
        /// §4's "match style must adapt or the body breaks" and the schedule tension in
        /// §4.1 are the grounds for it being monotonic and for the size of the spread.
        /// </summary>
        private static readonly (int Age, double Value)[] RecoveryRows =
        [
            (22, 1.10), (28, 1.00), (34, 0.91), (40, 0.81), (46, 0.71), (52, 0.62)
        ];

        // ── Style-adjusted longevity, doc 15 §4.2 ────────────────────────────

        /// <summary>
        /// How hard the athletic decline bites, by style. Doc 15 §4.2 ranks these
        /// explicitly: technical and mat styles "age extremely well" because the offence
        /// does not punish the body; high-flying "ages badly" because "the offence itself
        /// is the damage".
        ///
        /// Scales the **shortfall below peak**, not the level — so it changes how steeply
        /// somebody falls off and never makes a 24-year-old better than their peak.
        ///
        /// Doc 15's hardcore/deathmatch row has no home here because
        /// <see cref="WrestlingStyle"/> has no such member. That is the right omission
        /// rather than a missing one: it is a booking choice about match types, not a
        /// wrestler's style, and it belongs with stipulations.
        /// </summary>
        public static double StyleDeclineScale(WrestlingStyle style) => style switch
        {
            WrestlingStyle.Technical  => 0.55,
            WrestlingStyle.Grappler   => 0.68,
            WrestlingStyle.Powerhouse => 0.80,
            WrestlingStyle.Brawler    => 0.82,
            WrestlingStyle.Striker    => 0.92,
            WrestlingStyle.HighFlyer  => 1.35,
            _                         => 1.00
        };

        // ── Absolute readings ────────────────────────────────────────────────

        /// <summary>Athletic capacity at an age, for a style. Peak is 1.00.</summary>
        public static double Athleticism(int age, WrestlingStyle style = WrestlingStyle.Null)
        {
            double raw = Interpolate(AthleticRows, age);

            // Only the decline is style-adjusted. Being twenty is not a style problem.
            if (age <= 28) return raw;
            return 1.0 - (1.0 - raw) * StyleDeclineScale(style);
        }

        /// <summary>Ring psychology at an age. Peak is 1.00, reached around 40.</summary>
        public static double RingPsychology(int age) => Interpolate(PsychRows, age);

        /// <summary>Audience connection at a tenure in years. Peak is 1.00.</summary>
        public static double Connection(int tenureYears) =>
            Interpolate(ConnectionRows, Math.Max(0, tenureYears));

        /// <summary>Injury-risk multiplier at an age.</summary>
        public static double InjuryMultiplier(int age) => Interpolate(InjuryRows, age);

        /// <summary>Fatigue-recovery multiplier at an age.</summary>
        public static double RecoveryMultiplier(int age) => Interpolate(RecoveryRows, age);

        // ── Drift: what the engine actually applies ──────────────────────────

        /// <summary>
        /// How far athletic capacity has moved since the roster baseline. 1.0 = unchanged.
        /// Null birth year means no reading, which means no drift.
        /// </summary>
        public static double AthleticismDrift(int? bornIn, int year, WrestlingStyle style)
        {
            if (bornIn is null) return 1.0;
            double now  = Athleticism(year - bornIn.Value, style);
            double then = Athleticism(RosterBaselineYear - bornIn.Value, style);
            return then <= 0 ? 1.0 : now / then;
        }

        /// <summary>How far ring psychology has moved since the roster baseline.</summary>
        public static double RingPsychologyDrift(int? bornIn, int year)
        {
            if (bornIn is null) return 1.0;
            double now  = RingPsychology(year - bornIn.Value);
            double then = RingPsychology(RosterBaselineYear - bornIn.Value);
            return then <= 0 ? 1.0 : now / then;
        }

        /// <summary>
        /// How far audience connection has moved since the roster baseline, on tenure.
        ///
        /// Keyed to the debut year, so a wrestler three years in gains quickly and a
        /// fifteen-year veteran gains nothing — they already have the relationship, and
        /// doc 15 §4.1's point is that it cannot be hurried.
        /// </summary>
        public static double ConnectionDrift(int? debutedIn, int year)
        {
            if (debutedIn is null) return 1.0;
            double now  = Connection(year - debutedIn.Value);
            double then = Connection(RosterBaselineYear - debutedIn.Value);
            return then <= 0 ? 1.0 : now / then;
        }

        // ── Words for the booker ─────────────────────────────────────────────

        /// <summary>
        /// Doc 15 §4's band table, which is what a booker actually reasons with. The
        /// labels are the reference's own.
        /// </summary>
        public static string Band(int age) => age switch
        {
            < 24 => "Raw",
            < 29 => "Athletic peak",
            < 36 => "Prime",
            < 43 => "Veteran",
            < 51 => "Special attraction",
            _    => "Legend"
        };

        /// <summary>
        /// The one-line reading for a roster sheet: what this age is good and bad at.
        /// Doc 15 §4's band descriptions, compressed.
        /// </summary>
        public static string Reading(int age) => age switch
        {
            < 24 => "Physically capable, still learning. Highest injury rate of any band.",
            < 29 => "Athletic peak. Great matches, thin character.",
            < 36 => "The prime — athleticism high, psychology developed, character formed.",
            < 43 => "Athleticism declining, psychology and character at their peak. Often the biggest drawing years.",
            < 51 => "Special attraction. Reduced schedule, shorter matches, protected booking.",
            _    => "Rarely full-time. The value is the name."
        };

        // ── Interpolation ────────────────────────────────────────────────────

        /// <summary>
        /// Linear between the rows, flat outside them. Rows rather than a formula for the
        /// same reason the familiarity curve uses them: a table can be read against the
        /// reference it came from, and a polynomial cannot.
        /// </summary>
        private static double Interpolate((int Key, double Value)[] rows, int at)
        {
            if (at <= rows[0].Key) return rows[0].Value;
            if (at >= rows[^1].Key) return rows[^1].Value;

            for (int i = 0; i < rows.Length - 1; i++)
            {
                var (k0, v0) = rows[i];
                var (k1, v1) = rows[i + 1];
                if (at < k0 || at > k1) continue;

                double t = (at - k0) / (double)(k1 - k0);
                return v0 + (v1 - v0) * t;
            }

            return rows[^1].Value;
        }
    }
}
