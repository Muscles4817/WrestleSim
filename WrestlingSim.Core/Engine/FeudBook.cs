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

        /// <summary>
        /// Returns the feud between two wrestlers, creating a dormant one if it doesn't exist.
        /// A newly created feud starts at None intensity with zero heat.
        /// </summary>
        public Feud GetOrCreate(Wrestler a, Wrestler b)
        {
            string key = Key(a, b);
            if (_feuds.TryGetValue(key, out var existing)) return existing;

            // Preserve a stable A/B ordering so the same pair always maps to the same feud.
            var (first, second) = Ordered(a, b);
            var feud = new Feud { WrestlerA = first, WrestlerB = second, Intensity = FeudIntensity.None };
            _feuds[key] = feud;
            return feud;
        }

        /// <summary>
        /// Deposits heat and history tags from a booked segment or match.
        /// Returns the affected feud so the caller can report what changed.
        /// </summary>
        public FeudUpdate Record(Wrestler a, Wrestler b, double heat, IEnumerable<FeudHistoryTag>? tags = null)
        {
            var feud = GetOrCreate(a, b);
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
        private static string Key(Wrestler a, Wrestler b)
        {
            var (first, second) = Ordered(a, b);
            return $"{first.RealName}␟{second.RealName}";
        }

        private static (Wrestler, Wrestler) Ordered(Wrestler a, Wrestler b) =>
            string.CompareOrdinal(a.RealName, b.RealName) <= 0 ? (a, b) : (b, a);
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
