using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Session-level store of every feud the booker has going.
    /// This is the connector between segments and matches: segments deposit heat and
    /// history tags here, and the match booker reads the resulting feud back out.
    /// </summary>
    public class FeudBook
    {
        private readonly Dictionary<string, Feud> _feuds = new();

        /// <summary>Every feud that has accumulated any heat at all, hottest first.</summary>
        public IReadOnlyList<Feud> All =>
            _feuds.Values
                  .Where(f => f.Intensity > FeudIntensity.None)
                  .OrderByDescending(f => f.Heat)
                  .ToList();

        /// <summary>
        /// Every feud on the books, dormant ones included. `All` hides feuds that have not
        /// reached Cold, but a dormant pair can still carry a match count and history tags,
        /// and a save has to keep those.
        /// </summary>
        public IReadOnlyList<Feud> AllIncludingDormant =>
            _feuds.Values.OrderByDescending(f => f.Heat).ToList();

        /// <summary>Feuds involving a specific wrestler, hottest first.</summary>
        public IReadOnlyList<Feud> For(Wrestler w) =>
            All.Where(f => f.Involves(w)).ToList();

        /// <summary>
        /// The live story that puts these two on opposite sides, or null if there is none.
        ///
        /// A key lookup is not enough once a feud can have three camps: Rock, Triple H and
        /// Foley in a triangle is one story keyed on all three, and asking for "Rock vs
        /// Foley" has to find it. So this looks up the dedicated pairing first — a direct
        /// rivalry beats being incidentally in the same larger story — and otherwise takes
        /// the hottest story that has them opposed.
        /// </summary>
        public Feud? Find(Wrestler a, Wrestler b)
        {
            if (_feuds.TryGetValue(Key(a, b), out var exact)) return exact;

            // `_feuds.Values`, not `All`: `All` hides feuds below Cold, and the key lookup
            // this scan replaces did not. Searching the live ones only would have made
            // `Find` blind to a dormant triangle while still finding a dormant pairing —
            // the same question answered two different ways depending on how the story was
            // shaped, which is not a distinction anything meant to draw.
            return _feuds.Values
                         .Where(f => f.Opposes(a, b))
                         .OrderByDescending(f => f.Intensity)
                         .ThenByDescending(f => f.Heat)
                         .FirstOrDefault();
        }

        /// <summary>
        /// Every live story among these people that has at least two of them opposed.
        ///
        /// This is what a multi-man match needs and a two-side match never did: with three
        /// in the ring the match's headline rivalry is not the only one present, and the
        /// story that decides the finish is often between two people who are both losing to
        /// the third.
        /// </summary>
        /// <remarks>
        /// Live stories only — this asks what is *going on* between these people, and a
        /// dormant feud is by definition not going on. <see cref="Find"/> deliberately
        /// differs: it answers "do these two have history", which a cold feud still is.
        /// </remarks>
        public IReadOnlyList<Feud> Among(IReadOnlyList<Wrestler> people) =>
            All.Where(f => people.Any(a => people.Any(b => !ReferenceEquals(a, b) && f.Opposes(a, b))))
               .OrderByDescending(f => f.Intensity)
               .ThenByDescending(f => f.Heat)
               .ToList();

        /// <summary>The rivalry between these two sides, if it has been booked before.</summary>
        public Feud? Find(IReadOnlyList<Wrestler> sideA, IReadOnlyList<Wrestler> sideB) =>
            _feuds.TryGetValue(Key(sideA, sideB), out var feud) ? feud : null;

        /// <summary>
        /// Returns the feud between two wrestlers, creating a dormant one if it doesn't exist.
        /// A newly created feud starts at None intensity with zero heat.
        /// </summary>
        public Feud GetOrCreate(Wrestler a, Wrestler b) => GetOrCreate([a], [b]);

        /// <summary>The rivalry between these two sides, created if it is new.</summary>
        public Feud GetOrCreate(IReadOnlyList<Wrestler> sideA, IReadOnlyList<Wrestler> sideB) =>
            GetOrCreate([sideA, sideB]);

        /// <summary>
        /// The story between these camps, created if it is new. Two camps is a rivalry;
        /// three or more is a triangle or a faction war, which is one story and not several.
        /// </summary>
        public Feud GetOrCreate(IReadOnlyList<IReadOnlyList<Wrestler>> camps)
        {
            if (camps.Count < 2)
                throw new ArgumentException("A feud needs at least two camps.", nameof(camps));

            string key = Key(camps);
            if (_feuds.TryGetValue(key, out var existing)) return existing;

            var feud = new Feud
            {
                // Stable ordering so the same set of camps always maps to the same feud
                // whichever way round it is booked.
                Camps     = Ordered(camps).Select(c => c.ToList()).ToList(),
                Intensity = FeudIntensity.None
            };
            _feuds[key] = feud;
            return feud;
        }

        private static IReadOnlyList<IReadOnlyList<Wrestler>> Ordered(
            IReadOnlyList<IReadOnlyList<Wrestler>> camps) =>
            camps.OrderBy(CampName, StringComparer.Ordinal).ToList();

        private static string CampName(IReadOnlyList<Wrestler> camp) =>
            string.Join("+", camp.Select(w => w.RealName).OrderBy(n => n, StringComparer.Ordinal));

        /// <summary>
        /// Deposits heat and history tags from a booked segment or match.
        /// Returns the affected feud so the caller can report what changed.
        /// </summary>
        public FeudUpdate Record(Wrestler a, Wrestler b, double heat,
            IEnumerable<FeudHistoryTag>? tags = null, DateOnly? date = null) =>
            Record([a], [b], heat, tags, date);

        /// <summary>Deposits heat into the rivalry between two sides.</summary>
        public FeudUpdate Record(
            IReadOnlyList<Wrestler> sideA, IReadOnlyList<Wrestler> sideB,
            double heat, IEnumerable<FeudHistoryTag>? tags = null, DateOnly? date = null)
        {
            var feud = GetOrCreate(sideA, sideB);
            var before = feud.Intensity;

            // Advance *before* AddHeat, because AddHeat's reopen rule asks how long ago
            // this feud was settled and needs today's date to answer.
            //
            // Anything on screen between these two restarts the decay clock, whether or not
            // it added heat — a beatdown on a feud already at Nuclear adds nothing to the
            // number and is still the story being told this week.
            feud.Advance(date);
            feud.AddHeat(heat);

            var newTags = new List<FeudHistoryTag>();
            foreach (var tag in tags ?? Enumerable.Empty<FeudHistoryTag>())
                if (feud.AddTag(tag)) newTags.Add(tag);

            return new FeudUpdate
            {
                Feud          = feud,
                HeatAdded     = heat,
                HeatAfter     = feud.Heat,
                LevelAfter    = feud.Intensity,
                Escalated     = feud.Intensity > before,
                PreviousLevel = before,
                NewTags       = newTags
            };
        }

        /// <summary>
        /// Applies a segment's outcome to every pair of participants in it. A two-hander
        /// builds one feud; a faction beatdown builds one per attacker/victim pairing.
        /// </summary>
        public IReadOnlyList<FeudUpdate> RecordSegment(
            IReadOnlyList<Wrestler> participants, double heat,
            IEnumerable<FeudHistoryTag>? tags = null, DateOnly? date = null)
        {
            var updates = new List<FeudUpdate>();
            if (participants.Count < 2 || heat <= 0) return updates;

            var tagList = tags?.ToList() ?? new List<FeudHistoryTag>();

            // Split the heat across the pairings so a six-man doesn't generate triple heat.
            var pairs = new List<(Wrestler, Wrestler)>();
            for (int i = 0; i < participants.Count; i++)
                for (int j = i + 1; j < participants.Count; j++)
                    pairs.Add((participants[i], participants[j]));

            double perPair = heat / pairs.Count;
            foreach (var (x, y) in pairs)
                updates.Add(Record(x, y, perPair, tagList, date));

            return updates;
        }

        // ── Keying ───────────────────────────────────────────────────────────

        // Keyed on RealName because RingName changes with a gimmick swap and would
        // silently orphan the feud history.
        private static string Key(Wrestler a, Wrestler b) => Key([a], [b]);

        /// <summary>
        /// A side-aware key. Each side's names are sorted so billing order does not matter,
        /// then the two sides are sorted against each other so neither does home advantage.
        ///
        /// A team rivalry gets its own key rather than folding into one of the singles
        /// feuds inside it. That is the point: the crowd's appetite for two teams is not
        /// the same as its appetite for any pair of men in them, and it has to wear out
        /// separately.
        /// </summary>
        private static string Key(IEnumerable<Wrestler> a, IEnumerable<Wrestler> b) =>
            Key([a.ToList(), b.ToList()]);

        /// <summary>
        /// A camp-aware key. Each camp's names are sorted so billing order does not matter,
        /// then the camps are sorted against each other so neither does home advantage.
        ///
        /// A team rivalry gets its own key rather than folding into one of the singles feuds
        /// inside it, and a three-camp story gets its own rather than folding into any of the
        /// pairings inside *it*. That is the point: the crowd's appetite for a triangle is
        /// not the sum of its three pairings, and it has to wear out separately.
        /// </summary>
        private static string Key(IReadOnlyList<IReadOnlyList<Wrestler>> camps) =>
            string.Join("␟", camps.Select(Side).OrderBy(x => x, StringComparer.Ordinal));

        // Length-prefixed rather than plain-joined: a wrestler whose RealName contains the
        // separator would otherwise key identically to the team of the two people whose
        // names sit either side of it, silently merging two rivalries into one.
        private static string Side(IEnumerable<Wrestler> members) =>
            string.Concat(members.Select(w => w.RealName)
                                 .OrderBy(n => n, StringComparer.Ordinal)
                                 .Select(n => $"{n.Length}:{n}"));
    }

    /// <summary>What a single deposit of heat did to a feud.</summary>
    public class FeudUpdate
    {
        public required Feud Feud { get; init; }
        public double HeatAdded { get; init; }

        // Snapshots taken at deposit time. Feud is a live reference, so reading its
        // Heat later would report the end state against every earlier deposit.
        public double HeatAfter { get; init; }
        public FeudIntensity LevelAfter { get; init; }

        public bool Escalated { get; init; }
        public FeudIntensity PreviousLevel { get; init; }
        public List<FeudHistoryTag> NewTags { get; init; } = new();
    }
}
