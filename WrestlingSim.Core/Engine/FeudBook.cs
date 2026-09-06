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

        /// <summary>Returns the feud between two wrestlers, or null if they have no history.</summary>
        public Feud? Find(Wrestler a, Wrestler b) =>
            _feuds.TryGetValue(Key(a, b), out var feud) ? feud : null;

        /// <summary>The rivalry between these two sides, if it has been booked before.</summary>
        public Feud? Find(IReadOnlyList<Wrestler> sideA, IReadOnlyList<Wrestler> sideB) =>
            _feuds.TryGetValue(Key(sideA, sideB), out var feud) ? feud : null;

        /// <summary>
        /// Returns the feud between two wrestlers, creating a dormant one if it doesn't exist.
        /// A newly created feud starts at None intensity with zero heat.
        /// </summary>
        public Feud GetOrCreate(Wrestler a, Wrestler b) => GetOrCreate([a], [b]);

        /// <summary>The rivalry between these two sides, created if it is new.</summary>
        public Feud GetOrCreate(IReadOnlyList<Wrestler> sideA, IReadOnlyList<Wrestler> sideB)
        {
            string key = Key(sideA, sideB);
            if (_feuds.TryGetValue(key, out var existing)) return existing;

            // Stable ordering so the same pairing always maps to the same feud whichever
            // way round it is booked.
            var (first, second) = Ordered(sideA, sideB);
            var feud = new Feud
            {
                SideA     = first.ToList(),
                SideB     = second.ToList(),
                Intensity = FeudIntensity.None
            };
            _feuds[key] = feud;
            return feud;
        }

        private static (IReadOnlyList<Wrestler>, IReadOnlyList<Wrestler>) Ordered(
            IReadOnlyList<Wrestler> a, IReadOnlyList<Wrestler> b)
        {
            static string Name(IReadOnlyList<Wrestler> side) =>
                string.Join("+", side.Select(w => w.RealName).OrderBy(n => n, StringComparer.Ordinal));

            return string.CompareOrdinal(Name(a), Name(b)) <= 0 ? (a, b) : (b, a);
        }

        /// <summary>
        /// Deposits heat and history tags from a booked segment or match.
        /// Returns the affected feud so the caller can report what changed.
        /// </summary>
        public FeudUpdate Record(Wrestler a, Wrestler b, double heat, IEnumerable<FeudHistoryTag>? tags = null) =>
            Record([a], [b], heat, tags);

        /// <summary>Deposits heat into the rivalry between two sides.</summary>
        public FeudUpdate Record(
            IReadOnlyList<Wrestler> sideA, IReadOnlyList<Wrestler> sideB,
            double heat, IEnumerable<FeudHistoryTag>? tags = null)
        {
            var feud = GetOrCreate(sideA, sideB);
            var before = feud.Intensity;

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
            IReadOnlyList<Wrestler> participants, double heat, IEnumerable<FeudHistoryTag>? tags = null)
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
                updates.Add(Record(x, y, perPair, tagList));

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
        private static string Key(IEnumerable<Wrestler> a, IEnumerable<Wrestler> b)
        {
            string sideA = Side(a), sideB = Side(b);
            return string.CompareOrdinal(sideA, sideB) <= 0
                ? $"{sideA}␟{sideB}"
                : $"{sideB}␟{sideA}";

            // Length-prefixed rather than plain-joined: a wrestler whose RealName contains
            // the separator would otherwise key identically to the team of the two people
            // whose names sit either side of it, silently merging two rivalries into one.
            static string Side(IEnumerable<Wrestler> members) =>
                string.Concat(members.Select(w => w.RealName)
                                     .OrderBy(n => n, StringComparer.Ordinal)
                                     .Select(n => $"{n.Length}:{n}"));
        }
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
