using System.Text.Json;
using WrestlingSim.Enums;
using WrestlingSim.Models;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Makes wrestlers who do not exist, to fill a world bigger than its database (living-world
    /// plan, Part 1, decided 13 Nov 2026).
    ///
    /// **A generated wrestler is a real one re-drawn.** Each starts from somebody in the bundled
    /// database of the same division — their style, their shape as a worker, their kind of
    /// gimmick, their moves — and is then moved: skills shifted for the level they work at and
    /// jittered, a new name, a new age, their own popularity. So a generated mid-carder looks
    /// like the hand-authored ones because the hand-authored ones are where its numbers came
    /// from, not because a second set of distributions was written to resemble them.
    ///
    /// What is not taken: the real person's name, ring name, persona descriptor and the names of
    /// their signature moves, which belong to them. A generated wrestler's finisher is called
    /// what the move is.
    ///
    /// Deterministic from the seed it is given.
    /// </summary>
    public sealed class WrestlerGenerator
    {
        private readonly IReadOnlyList<Wrestler> _templates;
        private readonly Random _rng;
        private readonly HashSet<string> _takenNames;
        private readonly int _year;

        /// <param name="templates">Who generated people are drawn from: the bundled database.</param>
        /// <param name="taken">Names already in the world — real names, ring names and former ring names — which nobody generated may share.</param>
        public WrestlerGenerator(IReadOnlyList<Wrestler> templates, IEnumerable<Wrestler> taken, int seed, int year)
        {
            _templates = templates;
            _rng = new Random(seed);
            _year = year;
            _takenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in taken.Concat(templates))
            {
                if (!string.IsNullOrWhiteSpace(w.RealName)) _takenNames.Add(w.RealName);
                if (!string.IsNullOrWhiteSpace(w.RingName)) _takenNames.Add(w.RingName);
                foreach (var old in w.Gimmick?.PreviousNames ?? []) _takenNames.Add(old);
                foreach (var g in w.PreviousGimmicks ?? []) if (!string.IsNullOrWhiteSpace(g.Name)) _takenNames.Add(g.Name);
            }
        }

        // ── Calibration ──────────────────────────────────────────────────────
        // First guesses, labelled as such: what the harness (Part 8) compares against the
        // reference figures. Nothing here is set so that anybody in particular does anything.

        /// <summary>
        /// How over somebody working at each level usually is. The bundled database is a major
        /// league's roster and spans 23–96 with a median of 60, so Global sits there; each level
        /// down is a smaller audience that knows them.
        /// </summary>
        public static (double Low, double High) OvernessBand(PromotionTier level) => level switch
        {
            PromotionTier.Global      => (45, 92),
            PromotionTier.National    => (35, 78),
            PromotionTier.Established => (26, 64),
            PromotionTier.SuperIndie  => (18, 52),
            PromotionTier.Independent => (8, 38),
            _                         => (3, 26)
        };

        /// <summary>
        /// How much less polished, on the 0–5 ring-skill scale, than the major-league roster they
        /// are drawn from. A super-indie's workers are close to television's — that is what a
        /// super-indie is known for — and the local show's are not.
        /// </summary>
        public static double SkillShift(PromotionTier level) => level switch
        {
            PromotionTier.Global      => 0.0,
            PromotionTier.National    => -0.15,
            PromotionTier.Established => -0.3,
            PromotionTier.SuperIndie  => -0.3,
            PromotionTier.Independent => -0.6,
            _                         => -0.9
        };

        // ── Making one ───────────────────────────────────────────────────────

        /// <summary>
        /// Somebody who works at <paramref name="level"/>, from <paramref name="region"/>.
        /// <paramref name="overnessCap"/> keeps a top-up from inventing a bigger star than the
        /// database's own.
        /// </summary>
        public Wrestler Make(Division division, PromotionTier level, Region? region, double overnessCap = 100, bool rookie = false)
        {
            var pool = _templates.Where(t => t.Division == division).ToList();
            if (pool.Count == 0) pool = _templates.ToList();
            var t = pool[_rng.Next(pool.Count)];

            double shift = SkillShift(level);
            var (lo, hi) = OvernessBand(level);
            // A cap lowers the band rather than cutting it off, so a capped level is still a
            // spread of people and not a crowd standing at the cap.
            hi = Math.Min(hi, overnessCap);
            lo = Math.Min(lo, hi * 0.6);
            // Weighted toward the bottom of the band: most people at any level are not its stars.
            double overness = lo + (hi - lo) * Math.Pow(_rng.NextDouble(), 1.6);
            // A rookie is somebody nobody has heard of yet (Part 3).
            if (rookie) overness = 2 + 10 * _rng.NextDouble();

            var (realName, ringName) = NewName(division);

            double Skill(double v) => Math.Round(Math.Clamp(v + shift + Jitter(0.4), 0.5, 5.0), 1);
            int Rating(int v, double scale) => (int)Math.Round(Math.Clamp(v + shift * scale + Jitter(8), 20, 99));

            // Lower levels are younger on the whole, and every level has its veterans.
            int age = (int)Math.Round(Math.Clamp(
                (level >= PromotionTier.Established ? 31 : 27) + Jitter(7), 19, 46));
            int debutAge = t.BornIn is { } born && t.DebutedIn is { } debut
                ? Math.Clamp(debut - born + (int)Math.Round(Jitter(2)), 16, 30)
                : 20;
            int bornIn = _year - age;
            int debutedIn = Math.Min(_year, bornIn + Math.Min(debutAge, age - 1));
            // Out of training this year, at nineteen to twenty-five (doc 15: six months to two
            // years of school, most starting in their late teens or early twenties).
            if (rookie) { age = 19 + _rng.Next(7); bornIn = _year - age; debutedIn = _year; }

            var gimmick = new Gimmick(ringName)
            {
                Type              = t.Gimmick?.Type ?? GimmickType.Unspecified,
                PersonaDescriptor = Descriptor(t.Gimmick?.Type ?? GimmickType.Unspecified),
                Tone              = t.Gimmick?.Tone ?? default,
                NaturalAlignment  = t.Gimmick?.NaturalAlignment ?? default,
                Durability        = t.Gimmick?.Durability ?? default,
                Freshness         = Math.Round(0.5 + 0.5 * _rng.NextDouble(), 2),
                GimmickTraits     = [.. t.Gimmick?.GimmickTraits ?? []],
                AppealRatings     = Clone(t.Gimmick?.AppealRatings) ?? [],
                PreviousNames     = []
            };

            var rs = t.RingSkills;
            var w = new Wrestler(realName, gimmick, Math.Round(overness, 1), new RingSkills
            {
                HighFlyer  = Skill(rs.HighFlyer),
                Grappler   = Skill(rs.Grappler),
                Powerhouse = Skill(rs.Powerhouse),
                Technical  = Skill(rs.Technical),
                Brawler    = Skill(rs.Brawler),
                Striker    = Skill(rs.Striker)
            }, Math.Round(Math.Clamp(t.Charisma + shift * 0.8 + Jitter(0.5), 1.0, 5.0), 1), t.Style)
            {
                Id          = "gen-" + Wrestler.SlugOf(realName),
                IsGenerated = true,
                Region      = region,
                Division    = t.Division,
                BornIn      = bornIn,
                DebutedIn   = debutedIn,
                Physical    = new Models.Person.PhysicalAttributes
                {
                    Strength = Rating(t.Physical.Strength, 0),
                    Speed    = Rating(t.Physical.Speed, 0),
                    Agility  = Rating(t.Physical.Agility, 0),
                    Stamina  = Rating(t.Physical.Stamina, 20),
                    Size     = Math.Clamp(t.Physical.Size + _rng.Next(-1, 2), 1, 5)
                },
                Mental      = new Models.Person.MentalAttributes
                {
                    Psychology = Rating(t.Mental.Psychology, 20),
                    Selling    = Rating(t.Mental.Selling, 20),
                    RingIQ     = Rating(t.Mental.RingIQ, 20),
                    Toughness  = Rating(t.Mental.Toughness, 0)
                },
                Promo       = PromoFrom(t, realName, Math.Clamp(t.Charisma + shift * 0.8, 1.0, 5.0)),
                Moveset     = Clone(t.Moveset) ?? [],
                Signature   = (Clone(t.Signature) ?? []).Select(s =>
                {
                    // The move keeps its own name; the real wrestler's name for it is theirs.
                    s.Name = s.Move?.Name ?? s.Name;
                    s.Overness = (int)Math.Round(overness);
                    return s;
                }).ToList()
            };
            // The real wrestler it was drawn from was a different age with a different career
            // behind them. Move the age-bound numbers to this person's age and years in, by the
            // same curves careers move along, so a twenty-year-old is not born with a veteran's
            // psychology (measured 15 Nov: they were, and then declined from it).
            if (t.BornIn is { } tBorn && t.DebutedIn is { } tDebut)
            {
                int tAge = AgeCurve.RosterBaselineYear - tBorn, tYears = AgeCurve.RosterBaselineYear - tDebut;
                Growth.Reage(w, tAge, age, tYears, _year - debutedIn);
            }
            return w;
        }

        /// <summary>
        /// The real wrestler's promo shape — where they are strong and weak — moved to this
        /// person's charisma and jittered. From its own stream, seeded by the name, so adding it
        /// moved no other generated number and no generated world.
        /// </summary>
        private static Models.Person.PromoSkills? PromoFrom(Wrestler t, string realName, double charisma)
        {
            if (t.Promo is not { } p) return null;
            var rng = new Random(StableSeed.From(realName, "promo"));
            int shift = (int)Math.Round((charisma - t.Charisma) * 20);
            int Move(int v) => Math.Clamp(v + shift + rng.Next(-6, 7), 5, 100);
            var c = p.Clone();
            c.Delivery = Move(c.Delivery); c.Improvisation = Move(c.Improvisation);
            c.Stillness = Move(c.Stillness); c.RingPresence = Move(c.RingPresence); c.CrowdWork = Move(c.CrowdWork);
            c.SetPiece = Move(c.SetPiece); c.Physicality = Move(c.Physicality); c.Range = Move(c.Range);
            foreach (var r in c.Registers) r.Rating = Move(r.Rating);
            return c;
        }

        private double Jitter(double spread) => (_rng.NextDouble() + _rng.NextDouble() - 1.0) * spread;

        private static readonly JsonSerializerOptions CloneOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        private static T? Clone<T>(T? value) where T : class =>
            value is null ? null : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, CloneOptions), CloneOptions);

        // ── Names ────────────────────────────────────────────────────────────

        /// <summary>
        /// A real name, and the name they wrestle under: usually the same, sometimes a ring
        /// surname, now and then one word. Never one already in the world.
        /// </summary>
        private (string Real, string Ring) NewName(Division division)
        {
            var firsts = division == Division.Womens ? WomensFirstNames : MensFirstNames;
            for (int attempt = 0; ; attempt++)
            {
                string real = $"{Pick(firsts)} {Pick(Surnames)}";
                // After many collisions — a world bigger than the lists — a middle initial.
                if (attempt > 50) real = $"{Pick(firsts)} {(char)('A' + _rng.Next(26))}. {Pick(Surnames)}";
                if (_takenNames.Contains(real)) continue;

                double roll = _rng.NextDouble();
                string ring = roll < 0.6 ? real
                    : roll < 0.85 ? $"{real.Split(' ')[0]} {Pick(RingSurnames)}"
                    : Pick(Monikers);
                if (ring != real && _takenNames.Contains(ring)) ring = real;

                _takenNames.Add(real);
                _takenNames.Add(ring);
                return (real, ring);
            }
        }

        private string Pick(string[] list) => list[_rng.Next(list.Length)];

        private string Descriptor(GimmickType type)
        {
            var options = type switch
            {
                GimmickType.Monster        => new[] { "The Wrecking Ball", "The Big Problem", "Unstoppable Force" },
                GimmickType.Egotist        => ["All Ego", "God's Gift", "The Self-Made Star"],
                GimmickType.Underdog       => ["The Long Shot", "Never Stays Down", "Against The Odds"],
                GimmickType.Rebel          => ["The Troublemaker", "No Rules", "Out Of Line"],
                GimmickType.Showman        => ["The Showstopper", "Born Entertainer", "Lights And Glitter"],
                GimmickType.Daredevil      => ["Death Wish", "No Fear", "Over The Top Rope"],
                GimmickType.Loner          => ["Walks Alone", "Nobody's Friend", "The Outsider"],
                GimmickType.Realist        => ["No-Nonsense", "The Professional", "Clock In, Clock Out"],
                GimmickType.Comedy         => ["The Joker", "Comic Relief", "Laughs Last"],
                GimmickType.Superstar      => ["The Next Big Thing", "Destined For It", "The Franchise"],
                GimmickType.DarkPersona    => ["From The Shadows", "Something Wrong", "The Nightmare"],
                GimmickType.AntiHero       => ["Does It Their Way", "Bad Means, Good Ends", "Rough Justice"],
                GimmickType.Traditionalist => ["Old School", "Respect The Craft", "Fundamentals First"],
                GimmickType.Sellout        => ["For The Money", "Bought And Paid For", "The Company Choice"],
                GimmickType.Cryptic        => ["The Riddle", "Says Nothing", "Signs And Omens"],
                GimmickType.CultLeader     => ["The Prophet", "Followers Welcome", "The Message"],
                _                          => ["Local Talent", "Just Here To Wrestle", "Working Wrestler"]
            };
            return Pick(options);
        }

        // Ordinary first names and surnames, combined at random. A combination may happen to be
        // somebody's name somewhere; it is checked against every name in the world's database.
        private static readonly string[] MensFirstNames =
        [
            "Aaron", "Adrian", "Alex", "Andre", "Austin", "Ben", "Blake", "Brandon", "Brett", "Bryce",
            "Caleb", "Calvin", "Cameron", "Carlos", "Chase", "Chris", "Clay", "Cole", "Colton", "Connor",
            "Corey", "Curtis", "Dalton", "Damian", "Dante", "Darius", "Dean", "Derek", "Devin", "Diego",
            "Dominic", "Drew", "Dylan", "Eddie", "Eli", "Elijah", "Eric", "Ethan", "Evan", "Felix",
            "Gabe", "Garrett", "Grant", "Hank", "Hector", "Isaac", "Ivan", "Jace", "Jake", "Jamal",
            "Jared", "Jason", "Javier", "Jesse", "Joel", "Jonah", "Jordan", "Julian", "Kai", "Keith",
            "Kevin", "Kyle", "Lance", "Leon", "Levi", "Logan", "Lucas", "Luis", "Malik", "Marco",
            "Marcus", "Mason", "Mateo", "Miles", "Nate", "Nico", "Noah", "Omar", "Owen", "Parker",
            "Quinn", "Rafael", "Reggie", "Rhett", "Ricky", "Riley", "Ross", "Ruben", "Ryan", "Sam",
            "Shane", "Silas", "Spencer", "Terrell", "Theo", "Travis", "Trent", "Tyrone", "Victor", "Wade",
            "Wes", "Xavier", "Zach", "Zane"
        ];

        private static readonly string[] WomensFirstNames =
        [
            "Abby", "Adriana", "Alexis", "Alicia", "Alyssa", "Amara", "Amber", "Ana", "Angela", "Aria",
            "Ashley", "Autumn", "Bianca", "Brianna", "Brooke", "Camila", "Carmen", "Cassie", "Chloe", "Ciara",
            "Daisy", "Dana", "Daniela", "Destiny", "Elena", "Ella", "Erin", "Eva", "Faith", "Gabriela",
            "Gemma", "Grace", "Hailey", "Harper", "Hazel", "Imani", "Ivy", "Jada", "Jasmine", "Jenna",
            "Jess", "Josie", "Kara", "Katie", "Kayla", "Keira", "Kendra", "Kim", "Lacey", "Lana",
            "Layla", "Leah", "Lena", "Lila", "Lucia", "Lydia", "Mackenzie", "Maddie", "Maya", "Megan",
            "Mia", "Mila", "Monica", "Morgan", "Nadia", "Naomi", "Natalie", "Nina", "Nora", "Olivia",
            "Paige", "Piper", "Quinn", "Rachel", "Reagan", "Reese", "Rosa", "Ruby", "Sadie", "Sara",
            "Savannah", "Selena", "Sierra", "Skye", "Sofia", "Stella", "Tamara", "Tara", "Tess", "Tiana",
            "Valeria", "Vanessa", "Violet", "Willow", "Yasmin", "Zoe"
        ];

        private static readonly string[] Surnames =
        [
            "Abbott", "Acosta", "Aldridge", "Alvarado", "Ashford", "Baird", "Barlow", "Barrett", "Beckett", "Bishop",
            "Blackwood", "Bowman", "Bradford", "Brennan", "Briggs", "Bristow", "Calloway", "Carver", "Castillo", "Chambers",
            "Chandler", "Coleman", "Conley", "Crane", "Crawford", "Cruz", "Dalton", "Delgado", "Dixon", "Donovan",
            "Drummond", "Duarte", "Easton", "Ellison", "Espinoza", "Fairbanks", "Fenwick", "Fletcher", "Forsyth", "Fuentes",
            "Gallagher", "Garrison", "Gentry", "Gibbs", "Gordon", "Graves", "Hadley", "Hale", "Harlow", "Hawkins",
            "Hayward", "Hendrix", "Holloway", "Huxley", "Ingram", "Jennings", "Kearney", "Keller", "Kendall", "Kirby",
            "Lambert", "Langley", "Lawson", "Lockwood", "Lowell", "Maddox", "Malone", "Marsh", "Maxwell", "McAllister",
            "Medina", "Mercer", "Montoya", "Morrow", "Nash", "Navarro", "Norwood", "Oakley", "Ochoa", "Osborne",
            "Pacheco", "Palmer", "Pearce", "Pemberton", "Prescott", "Quintero", "Ramsey", "Reyes", "Radcliffe", "Rios",
            "Rourke", "Salazar", "Sawyer", "Sheffield", "Sinclair", "Slater", "Sloane", "Stanton", "Sterling", "Sutton",
            "Tate", "Thornton", "Tolliver", "Townsend", "Underwood", "Valdez", "Vance", "Vaughn", "Wakefield", "Walsh",
            "Warwick", "Webber", "Whitfield", "Wilder", "Winslow", "Wolfe", "Yates", "Yorke", "Zamora", "Zeller"
        ];

        private static readonly string[] RingSurnames =
        [
            "Blaze", "Steele", "Stone", "Storm", "Cross", "Knox", "Wardell", "Ryder", "Diamond", "Phoenix",
            "Fury", "Valor", "Vortex", "Hawk", "Riot", "Saint", "Rivers", "Wilde", "Frost", "Ember",
            "Lynx", "Vega", "Rook", "Blackburn", "Ironside", "Thorne", "Dunmore", "Starr", "Locke", "Maverick"
        ];

        private static readonly string[] Monikers =
        [
            "Brickhouse", "Cinder", "Havoc", "Jolt", "Kestrel", "Lantern", "Mammoth", "Nightjar", "Onyx", "Quarry",
            "Ramshackle", "Sparrowhawk", "Tumbleweed", "Undertow", "Vesper", "Wildfire", "Yardstick", "Zephyr", "Anvil", "Bramble",
            "Crowbar", "Driftwood", "Flint", "Gristle", "Hornet", "Ironwood", "Juniper", "Krakatoa", "Lockjaw", "Marrow"
        ];
    }
}
