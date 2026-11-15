using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.Person;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Careers that move (living-world plan, Part 2). Once a month every wrestler in the world
    /// takes a step in each of six skill groups: toward where their age would take them, if
    /// they have been used; away from it, if age has passed it.
    ///
    /// **This replaces the age lens.** <see cref="AgeCurve"/> used to be applied at match time
    /// as a ratio against 2026, so the profile showed one number and the engine read another.
    /// Now the curve sets each group's *target*, the monthly step moves the stored attributes
    /// toward it, and the stored attribute is what everything reads.
    ///
    /// What decides how far a step goes, all of it from 15 Nov's decisions:
    ///  - the **ceiling**: what the group would be at their best age;
    ///  - the **curve**: where their age (or, for presence, their tenure) puts them against it;
    ///  - **use**: matches that exercised the group, a month's worth being about one a week.
    ///    Conditioning is the gym, so it does not need matches. Nobody else grows sitting at home;
    ///  - **learning speed**, per group;
    ///  - **opposition**, for craft and mat wrestling: you learn more from somebody better;
    ///  - **injury**: a hurt month grows nothing and costs the body a little.
    /// Decline needs no use: age takes what it takes.
    /// </summary>
    public static class Growth
    {
        // ── Calibration (first guesses, for Part 8's harness to compare) ──────

        /// <summary>
        /// The share of the gap to target closed in a month of full use at typical speed: about
        /// 63% a year. Measured 15 Nov: at 5% a busy twenty-two-year-old's craft went 67 to 71 in
        /// three years against the curve's 76, trailing a target that moves every month.
        /// </summary>
        public const double Pace = 0.08;

        /// <summary>The share of the overshoot age takes back each month.</summary>
        public const double DeclinePace = 0.08;

        /// <summary>
        /// A month of full use: three matches. Measured 15 Nov: a television roster member works
        /// one or two televised matches a month (house shows are not simulated for rivals), so a
        /// figure of four left almost nobody fully used and the busiest young wrestlers grew
        /// less than two points in five years. Use rises as the square root, so one match a
        /// month is well over half a month's learning and none is nothing.
        /// </summary>
        public const double FullMonth = 3.0;

        // ── The groups and what is in them ───────────────────────────────────

        private sealed record Attr(string Name, Func<Wrestler, double> Get, Action<Wrestler, double> Set, bool Whole, double Scale);

        // Scale puts every attribute on 0–100: ring skills and charisma are stored 0–5.
        private static readonly Attr[][] Members =
        [
            // Conditioning
            [
                new("Strength", w => w.Physical.Strength, (w, v) => w.Physical.Strength = (int)v, true, 1),
                new("Stamina",  w => w.Physical.Stamina,  (w, v) => w.Physical.Stamina  = (int)v, true, 1)
            ],
            // Athletic
            [
                new("Speed",     w => w.Physical.Speed,      (w, v) => w.Physical.Speed   = (int)v, true, 1),
                new("Agility",   w => w.Physical.Agility,    (w, v) => w.Physical.Agility = (int)v, true, 1),
                new("HighFlyer", w => w.RingSkills.HighFlyer, (w, v) => w.RingSkills.HighFlyer = v, false, 20)
            ],
            // Combat
            [
                new("Striker",    w => w.RingSkills.Striker,    (w, v) => w.RingSkills.Striker    = v, false, 20),
                new("Brawler",    w => w.RingSkills.Brawler,    (w, v) => w.RingSkills.Brawler    = v, false, 20),
                new("Powerhouse", w => w.RingSkills.Powerhouse, (w, v) => w.RingSkills.Powerhouse = v, false, 20)
            ],
            // Mat
            [
                new("Technical", w => w.RingSkills.Technical, (w, v) => w.RingSkills.Technical = v, false, 20),
                new("Grappler",  w => w.RingSkills.Grappler,  (w, v) => w.RingSkills.Grappler  = v, false, 20)
            ],
            // Craft
            [
                new("Psychology", w => w.Mental.Psychology, (w, v) => w.Mental.Psychology = (int)v, true, 1),
                new("Selling",    w => w.Mental.Selling,    (w, v) => w.Mental.Selling    = (int)v, true, 1),
                new("RingIQ",     w => w.Mental.RingIQ,     (w, v) => w.Mental.RingIQ     = (int)v, true, 1)
            ],
            // Presence
            [
                new("Charisma", w => w.Charisma, (w, v) => w.Charisma = v, false, 20)
            ]
        ];

        public static IReadOnlyList<SkillGroup> All { get; } = Enum.GetValues<SkillGroup>();

        /// <summary>A group's level, 0–100: the average of what is in it.</summary>
        public static double Level(Wrestler w, SkillGroup g)
        {
            var members = Members[(int)g];
            double sum = 0;
            foreach (var a in members) sum += a.Get(w) * a.Scale;
            return sum / members.Length;
        }

        /// <summary>The group a wrestler's style trains, beyond craft and presence which everybody's does.</summary>
        public static SkillGroup StyleGroup(WrestlingStyle style) => style switch
        {
            WrestlingStyle.HighFlyer => SkillGroup.Athletic,
            WrestlingStyle.Technical or WrestlingStyle.Grappler => SkillGroup.Mat,
            _ => SkillGroup.Combat
        };

        /// <summary>
        /// Whether a group says anything about how fast this person learns it: craft and presence
        /// for everybody, and the group their style uses. A powerhouse's poor high-flying is a
        /// thing they never trained, not a thing they were slow at.
        /// </summary>
        public static bool Judged(Wrestler w, SkillGroup g) =>
            g is SkillGroup.Craft or SkillGroup.Presence || g == StyleGroup(w.Style);

        // ── Where age puts them ──────────────────────────────────────────────

        /// <summary>
        /// Each group's share of its ceiling at this age, from <see cref="AgeCurve"/>. Presence is
        /// tenure, as connection is. Mat wrestling is mostly understanding and ages well; combat
        /// is half body, half head.
        /// </summary>
        public static double Curve(Wrestler w, SkillGroup g, int year)
        {
            int? age = w.BornIn is { } born ? year - born : null;
            double body = age is { } a1 ? AgeCurve.Athleticism(a1, w.Style) : 1.0;
            double head = age is { } a2 ? AgeCurve.RingPsychology(a2) : 1.0;
            return g switch
            {
                SkillGroup.Conditioning or SkillGroup.Athletic => body,
                SkillGroup.Combat => 0.5 * body + 0.5 * head,
                SkillGroup.Mat    => 0.35 * (age is { } a3 ? AgeCurve.Athleticism(a3, WrestlingStyle.Technical) : 1.0) + 0.65 * head,
                SkillGroup.Craft  => head,
                _ => w.DebutedIn is { } debut ? AgeCurve.Connection(Math.Max(0, year - debut)) : 1.0
            };
        }

        /// <summary>
        /// The curve on a particular day: between this year's and next year's by how far through
        /// the year it is. Ages are held to the year (<see cref="Wrestler.BornIn"/>), and stepping
        /// the target once a January left a twenty-two-year-old learning nothing for their first
        /// eleven months however hard they worked (found by test, 15 Nov).
        /// </summary>
        public static double Curve(Wrestler w, SkillGroup g, DateOnly on)
        {
            double t = (on.DayOfYear - 1) / 365.0;
            return Curve(w, g, on.Year) * (1 - t) + Curve(w, g, on.Year + 1) * t;
        }

        /// <summary>
        /// How good somebody usually is at a group, for their years in the business, as a share
        /// of a seasoned veteran. What "how far they have come for their years" is measured against.
        /// </summary>
        private static readonly (int Years, double Share)[] Experience =
        [
            (0, 0.62), (3, 0.78), (6, 0.88), (10, 0.96), (14, 1.0)
        ];

        private static double ExperienceShare(int years)
        {
            if (years <= Experience[0].Years) return Experience[0].Share;
            for (int i = 0; i < Experience.Length - 1; i++)
            {
                var (y0, s0) = Experience[i];
                var (y1, s1) = Experience[i + 1];
                if (years <= y1) return s0 + (s1 - s0) * (years - y0) / (double)(y1 - y0);
            }
            return 1.0;
        }

        // ── Reading a real person; drawing a generated one ───────────────────

        /// <summary>
        /// The bundled roster's seasoned veterans (twelve years or more), each group's median level,
        /// and the learning speeds the roster shows — the reference everybody is read against.
        /// </summary>
        private static readonly Lazy<(double[] Veteran, double[][] Speeds)> Reference = new(() =>
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var veteran = new double[Development.Groups];
            foreach (var g in All)
            {
                // Veterans who use the group: a high-flyer is measured against high-flyers.
                var levels = roster.Where(w => Judged(w, g) && w.DebutedIn is { } d && AgeCurve.RosterBaselineYear - d >= 12)
                                   .Select(w => Level(w, g)).OrderBy(x => x).ToList();
                veteran[(int)g] = levels.Count == 0 ? 60 : levels[levels.Count / 2];
            }
            var speeds = All.Select(g => roster.Where(w => Judged(w, g))
                .Select(w => SpeedFrom(w, g, AgeCurve.RosterBaselineYear, veteran)).ToArray()).ToArray();
            return (veteran, speeds);
        });

        private static double SpeedFrom(Wrestler w, SkillGroup g, int year, double[] veteran)
        {
            int years = w.DebutedIn is { } d ? Math.Max(0, year - d) : 10;
            double expected = veteran[(int)g] * ExperienceShare(years);
            double ratio = expected <= 0 ? 1 : Level(w, g) / expected;
            return Math.Clamp(Math.Pow(ratio, 1.5), 0.5, 1.6);
        }

        /// <summary>
        /// A real person, read from their own numbers, inventing nothing. Each ceiling is where
        /// their own curve takes them from today; each learning speed is how far they have come in
        /// that group for their years, for the groups their style judges. The rest take the
        /// average of those, pulled toward typical, and conditioning — the gym, which is effort
        /// the database does not record — is typical for everybody.
        /// </summary>
        public static Development Read(Wrestler w, int year) => Read(w, new DateOnly(year, 1, 1));

        public static Development Read(Wrestler w, DateOnly on)
        {
            int year = on.Year;
            var (veteran, _) = Reference.Value;
            var d = new Development();
            foreach (var g in All)
            {
                double level = Level(w, g);
                double curve = Curve(w, g, on);
                // Not capped at 100: it is a target, and the attributes cap themselves. Capping it
                // put a gifted twenty-year-old's target below where they already were.
                d.Ceiling[(int)g] = Math.Clamp(curve <= 0 ? level : level / curve, level, 150);
            }

            var judged = All.Where(g => g != SkillGroup.Conditioning && Judged(w, g)).ToList();
            foreach (var g in judged) d.Speed[(int)g] = SpeedFrom(w, g, year, veteran);
            double usual = judged.Count == 0 ? 1.0 : judged.Average(g => d.Speed[(int)g]);
            foreach (var g in All.Where(g => !judged.Contains(g)))
                d.Speed[(int)g] = g == SkillGroup.Conditioning ? 1.0 : (1.0 + usual) / 2;
            return Rounded(d);
        }

        /// <summary>
        /// A generated person: ceilings as for a real one, a little either way; learning speeds
        /// drawn, group by group, from the speeds the real roster shows.
        /// </summary>
        public static Development Draw(Wrestler w, DateOnly on, Random rng)
        {
            var d = Read(w, on);
            var (_, speeds) = Reference.Value;
            foreach (var g in All)
            {
                double level = Level(w, g);
                // Room above where their own curve takes them, never less: a ceiling below
                // today's level would have them declining at twenty-four (measured 15 Nov).
                d.Ceiling[(int)g] = Math.Clamp(d.Ceiling[(int)g] * (1.0 + 0.10 * rng.NextDouble()), level, 150);
                if (g == SkillGroup.Conditioning) continue;
                var pool = speeds[(int)g];
                if (pool.Length > 0) d.Speed[(int)g] = pool[rng.Next(pool.Length)];
            }
            return Rounded(d);
        }

        /// <summary>Two places are plenty, and a save of three thousand people notices the rest.</summary>
        private static Development Rounded(Development d)
        {
            for (int i = 0; i < Development.Groups; i++)
            {
                d.Ceiling[i] = Math.Round(d.Ceiling[i], 1);
                d.Speed[i] = Math.Round(d.Speed[i], 2);
            }
            return d;
        }

        // ── Starting ─────────────────────────────────────────────────────────

        /// <summary>
        /// Gives everybody who has none a development, as of the career's date. A real person's
        /// numbers are as of <see cref="AgeCurve.RosterBaselineYear"/>; a career in another year
        /// first moves them along their own curve to it, which is what the old lens did at every
        /// match and now happens once. Generated people are made as of the year they are made in.
        /// </summary>
        public static void Begin(Career career)
        {
            var on = career.CurrentDate;
            foreach (var w in career.Roster)
            {
                if (w.Development is not null) continue;
                if (!w.IsGenerated) Age(w, AgeCurve.RosterBaselineYear, on.Year);
                w.Development = w.IsGenerated
                    ? Draw(w, on, new Random(StableSeed.From(w.Id, "growth")))
                    : Read(w, on);
                Mark(w, on);
            }
        }

        /// <summary>
        /// Moves the attributes the age lens used to move, by the ratios it used: athleticism
        /// (speed, agility), ring psychology (psychology, ring IQ) and connection (charisma).
        /// </summary>
        internal static void Age(Wrestler w, int fromYear, int toYear)
        {
            if (fromYear == toYear) return;
            double body = Ratio(w.BornIn, b => AgeCurve.Athleticism(b, w.Style), fromYear, toYear);
            double head = Ratio(w.BornIn, AgeCurve.RingPsychology, fromYear, toYear);
            double tenure = Ratio(w.DebutedIn, t => AgeCurve.Connection(Math.Max(0, t)), fromYear, toYear);
            w.Physical.Speed   = (int)Math.Round(Math.Clamp(w.Physical.Speed * body, 1, 99));
            w.Physical.Agility = (int)Math.Round(Math.Clamp(w.Physical.Agility * body, 1, 99));
            w.Mental.Psychology = (int)Math.Round(Math.Clamp(w.Mental.Psychology * head, 1, 99));
            w.Mental.RingIQ     = (int)Math.Round(Math.Clamp(w.Mental.RingIQ * head, 1, 99));
            w.Charisma = Math.Round(Math.Clamp(w.Charisma * tenure, 0, 5), 2);
        }

        /// <summary>
        /// Moves somebody's age-bound attributes from one age and tenure to another, by the same
        /// ratios as <see cref="Age"/>. What the generator does to the real wrestler it starts
        /// from: a twenty-year-old drawn from a thirty-five-year-old does not keep the veteran's
        /// psychology.
        /// </summary>
        internal static void Reage(Wrestler w, int fromAge, int toAge, int fromTenure, int toTenure)
        {
            double body = AgeCurve.Athleticism(toAge, w.Style) / Math.Max(0.01, AgeCurve.Athleticism(fromAge, w.Style));
            double head = AgeCurve.RingPsychology(toAge) / Math.Max(0.01, AgeCurve.RingPsychology(fromAge));
            double tenure = AgeCurve.Connection(Math.Max(0, toTenure)) / Math.Max(0.01, AgeCurve.Connection(Math.Max(0, fromTenure)));
            w.Physical.Speed    = (int)Math.Round(Math.Clamp(w.Physical.Speed * body, 1, 99));
            w.Physical.Agility  = (int)Math.Round(Math.Clamp(w.Physical.Agility * body, 1, 99));
            w.Physical.Strength = (int)Math.Round(Math.Clamp(w.Physical.Strength * body, 1, 99));
            w.Physical.Stamina  = (int)Math.Round(Math.Clamp(w.Physical.Stamina * body, 1, 99));
            w.Mental.Psychology = (int)Math.Round(Math.Clamp(w.Mental.Psychology * head, 1, 99));
            w.Mental.RingIQ     = (int)Math.Round(Math.Clamp(w.Mental.RingIQ * head, 1, 99));
            w.Mental.Selling    = (int)Math.Round(Math.Clamp(w.Mental.Selling * head, 1, 99));
            var r = w.RingSkills;
            r.Technical = Math.Clamp(r.Technical * (0.35 * body + 0.65 * head), 0.5, 5);
            r.Grappler  = Math.Clamp(r.Grappler  * (0.35 * body + 0.65 * head), 0.5, 5);
            r.HighFlyer = Math.Clamp(r.HighFlyer * body, 0.5, 5);
            r.Striker   = Math.Clamp(r.Striker   * (0.5 * body + 0.5 * head), 0.5, 5);
            r.Brawler   = Math.Clamp(r.Brawler   * (0.5 * body + 0.5 * head), 0.5, 5);
            r.Powerhouse = Math.Clamp(r.Powerhouse * (0.5 * body + 0.5 * head), 0.5, 5);
            w.Charisma = Math.Clamp(w.Charisma * tenure, 1, 5);
        }

        private static double Ratio(int? since, Func<int, double> curve, int fromYear, int toYear)
        {
            if (since is not { } s) return 1.0;
            double then = curve(fromYear - s);
            return then <= 0 ? 1.0 : curve(toYear - s) / then;
        }

        // ── What a month's work exercised ────────────────────────────────────

        /// <summary>Somebody's level as an opponent: their ring skills and their psychology, 0–100.</summary>
        public static double AsOpponent(Wrestler w) => (w.RingSkills.GetOverallSkill() * 20 + w.Mental.Psychology) / 2;

        /// <summary>
        /// A match worked. Every match is craft and a little presence. The rest is what they did
        /// in it: the beats the engine had them work, where there are beats; their style where
        /// there are not (a rival's match, until Part 5 runs those through the engine).
        /// </summary>
        public static void RecordMatch(Wrestler w, IEnumerable<Wrestler> opponents, MatchEngineResult? result = null)
        {
            if (w.Development is not { } d) return;
            d.Matches++;
            var against = opponents.Where(o => o != w).ToList();
            d.Opposition += against.Count == 0 ? AsOpponent(w) : against.Average(AsOpponent);
            d.Worked[(int)SkillGroup.Craft] += 1;
            d.Worked[(int)SkillGroup.Presence] += 0.25;

            var shares = new double[Development.Groups];
            int beats = 0;
            foreach (var beat in result?.BeatResults ?? [])
                if (beat.Worker == w) { shares[(int)(BeatGroup(beat.BeatType) ?? StyleGroup(w.Style))]++; beats++; }
            if (beats == 0) { d.Worked[(int)StyleGroup(w.Style)] += 1; return; }
            for (int g = 0; g < shares.Length; g++) d.Worked[g] += shares[g] / beats;
        }

        /// <summary>What a beat exercises, where it says; null where it is the worker's own style.</summary>
        public static SkillGroup? BeatGroup(BeatType type) => type switch
        {
            BeatType.HighSpot or BeatType.HotOpening or BeatType.Shine or BeatType.HopeSpot => SkillGroup.Athletic,
            BeatType.CrowdBrawl or BeatType.Comeback or BeatType.RevengeSpot or BeatType.HeatSegment or BeatType.Cutoff => SkillGroup.Combat,
            BeatType.RestHold or BeatType.SlowOpening or BeatType.Isolation => SkillGroup.Mat,
            _ => null
        };

        /// <summary>A promo or a segment: presence.</summary>
        public static void RecordTalk(Wrestler w)
        {
            if (w.Development is { } d) d.Worked[(int)SkillGroup.Presence] += 1;
        }

        public static void RecordHurt(Wrestler w)
        {
            if (w.Development is { } d) d.Hurt = true;
        }

        // ── The month ────────────────────────────────────────────────────────

        /// <summary>Everybody's step, on the first of the month.</summary>
        public static void Month(Career career)
        {
            Begin(career);
            foreach (var w in career.Roster.Where(w => !w.IsRetired))
            {
                // The player hears when one of theirs moves a step of five in the ring.
                bool yours = career.ContractOf(w) is not null;
                int before = yours ? InRing(w) / 5 : 0;
                Step(w, career.CurrentDate);
                if (!yours) continue;
                int after = InRing(w) / 5;
                if (after > before)
                    career.AddNews($"{w.RingName} is improving in the ring: up to {InRing(w)} out of 100.", aboutYou: true);
                else if (after < before)
                    career.AddNews($"{w.RingName} is slipping in the ring: down to {InRing(w)} out of 100.", aboutYou: true);
            }
        }

        /// <summary>The in-ring figure the profile shows, out of 100.</summary>
        private static int InRing(Wrestler w) => (int)Math.Round(Math.Clamp(w.RingSkills.GetOverallSkill(), 0, 5) * 20);

        /// <summary>One wrestler's month. Returns how far each group moved, in points out of 100.</summary>
        public static double[] Step(Wrestler w, DateOnly on)
        {
            var moved = new double[Development.Groups];
            if (w.Development is not { } d) return moved;
            if (w.IsInjured(on)) d.Hurt = true;

            foreach (var g in All)
            {
                int i = (int)g;
                double level = Level(w, g);
                double target = d.Ceiling[i] * Curve(w, g, on);
                double delta;
                if (level < target)
                {
                    double use = g == SkillGroup.Conditioning
                        ? (d.Hurt ? 0 : 1)
                        : Math.Sqrt(Math.Min(1, d.Worked[i] / FullMonth));
                    double opposition = 1;
                    if (g is SkillGroup.Craft or SkillGroup.Mat && d.Matches > 0)
                        opposition = Math.Clamp(1 + (d.Opposition / d.Matches - level) / 50, 0.6, 1.4);
                    if (d.Hurt) use = 0;
                    delta = (target - level) * d.Speed[i] * Pace * use * opposition;
                }
                else delta = -(level - target) * DeclinePace;

                if (d.Hurt && g is SkillGroup.Athletic or SkillGroup.Conditioning) delta -= 0.3;
                Shift(w, d, g, delta);
                moved[i] = delta;
            }

            Array.Clear(d.Worked);
            d.Opposition = 0;
            d.Matches = 0;
            d.Opposition = 0;
            d.Hurt = false;
            Mark(w, on);
            return moved;
        }

        private static void Shift(Wrestler w, Development d, SkillGroup g, double points)
        {
            if (points == 0) return;
            foreach (var a in Members[(int)g])
            {
                if (a.Whole)
                {
                    double owed = d.Carry.GetValueOrDefault(a.Name) + points;
                    int whole = (int)Math.Truncate(owed);
                    d.Carry[a.Name] = Math.Round(owed - whole, 3);
                    if (whole != 0) a.Set(w, Math.Clamp(a.Get(w) + whole, 1, 99));
                }
                else a.Set(w, Math.Round(Math.Clamp(a.Get(w) + points / a.Scale, 0.5, 5.0), 3));
            }
        }

        /// <summary>
        /// Writes down where they are. Monthly for two years, then one a year (January), so a
        /// long career's save does not carry a mark for every month of every wrestler.
        /// </summary>
        private static void Mark(Wrestler w, DateOnly on)
        {
            var d = w.Development!;
            d.History.Add(new DevelopmentMark { On = on, Levels = All.Select(g => (int)Math.Round(Level(w, g))).ToArray() });
            var cutoff = on.AddMonths(-24);
            d.History.RemoveAll(m => m.On < cutoff && m.On.Month != 1 && m != d.History[0]);
        }

        /// <summary>Where they were on or before a date, from the marks; null if no mark is that old.</summary>
        public static int[]? LevelsOn(Wrestler w, DateOnly on) =>
            w.Development?.History.LastOrDefault(m => m.On <= on)?.Levels;
    }
}
