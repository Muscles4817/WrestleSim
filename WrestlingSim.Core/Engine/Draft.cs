using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>One selection in a draft.</summary>
    public sealed record DraftPick(Wrestler Wrestler, Brand To, Brand? From, int Round)
    {
        /// <summary>True when this pick actually moved someone.</summary>
        public bool IsMove => From is null || From.Id != To.Id;
    }

    /// <summary>What a completed draft did to the world.</summary>
    public sealed class DraftOutcome
    {
        public required IReadOnlyList<DraftPick> Picks { get; init; }

        /// <summary>Picks that changed someone's brand.</summary>
        public int Moved { get; init; }

        /// <summary>Feuds ended because the two are no longer on the same show.</summary>
        public int FeudsEnded { get; init; }

        /// <summary>Pairings whose match-count fatigue was cleared.</summary>
        public int PairingsRefreshed { get; init; }

        public double IntegrityBefore { get; init; }
        public double IntegrityAfter { get; init; }
        public double Ceiling { get; init; }

        /// <summary>Days since the previous draft, or since the split began.</summary>
        public int DaysSincePrevious { get; init; }

        /// <summary>True when this draft came round sooner than a year.</summary>
        public bool WasEarly { get; init; }
    }

    /// <summary>
    /// A draft in progress. Brands take turns; the player picks for whoever is on the
    /// clock, or lets the board pick for them.
    ///
    /// Snake order (A, B, B, A, A, …) rather than straight alternation, because straight
    /// alternation hands the same brand every odd-ranked performer and produces a permanent
    /// imbalance — which is the B-show problem of doc 22 §3.3 designed in from the start.
    /// The weaker brand picks first for the same reason: the draft is the promotion's tool
    /// for correcting imbalance between brands (doc 22 §5.1).
    /// </summary>
    public sealed class DraftBoard
    {
        private readonly Random _rng;

        private DraftBoard(IReadOnlyList<Brand> brands, List<Wrestler> available, Random rng)
        {
            Brands = brands;
            Available = available;
            _rng = rng;
        }

        public IReadOnlyList<Brand> Brands { get; }

        /// <summary>Everyone still undrafted, most over first.</summary>
        public List<Wrestler> Available { get; }

        public List<DraftPick> Picks { get; } = new();

        /// <summary>Where each person started, so the board can report who actually moved.</summary>
        private readonly Dictionary<string, Brand?> _origin = new(StringComparer.OrdinalIgnoreCase);

        public bool Complete => Available.Count == 0;

        public int Round => Brands.Count == 0 ? 1 : Picks.Count / Brands.Count + 1;

        /// <summary>The brand whose pick it is, or null once the board is empty.</summary>
        public Brand? OnTheClock =>
            Complete || Brands.Count == 0 ? null : Brands[SnakeIndex(Picks.Count, Brands.Count)];

        /// <summary>
        /// Snake ordering: forwards through the brands, then backwards, repeating. The brand
        /// at index 0 therefore picks first and last in each pair of rounds.
        /// </summary>
        public static int SnakeIndex(int pickNumber, int brandCount)
        {
            if (brandCount <= 1) return 0;

            int round = pickNumber / brandCount;
            int within = pickNumber % brandCount;
            return round % 2 == 0 ? within : brandCount - 1 - within;
        }

        public static DraftBoard Create(Career career, int? seed = null) =>
            Create(career.Brands.Brands, career.Roster, career.Brands, seed);

        /// <summary>
        /// A board over an arbitrary set of brands. The split is read only for where people
        /// currently are, so this also serves the first division of a roster — which is the
        /// same operation as a draft, just with nowhere to have come from.
        /// </summary>
        public static DraftBoard Create(
            IReadOnlyList<Brand> brands,
            IEnumerable<Wrestler> roster,
            BrandSplit split,
            int? seed = null)
        {
            // The brand with the weaker recent form picks first. A brand that has never run
            // sorts as weakest, which is the right answer for a split's first draft too.
            var order = brands
                .OrderBy(b => b.Form ?? double.MinValue)
                .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var available = roster
                .OrderByDescending(w => w.EffectiveOverness)
                .ThenBy(w => w.RealName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var board = new DraftBoard(order, available, seed is { } s ? new Random(s) : new Random());

            foreach (var w in available) board._origin[w.Id] = split.BrandOf(w.Id);

            return board;
        }

        /// <summary>Takes a specific person with the current pick. Ignored if they are gone.</summary>
        public DraftPick? Pick(Wrestler wrestler)
        {
            var brand = OnTheClock;
            if (brand is null || !Available.Remove(wrestler)) return null;

            _origin.TryGetValue(wrestler.Id, out var from);
            var pick = new DraftPick(wrestler, brand, from, Round);
            Picks.Add(pick);
            return pick;
        }

        /// <summary>
        /// Takes one of the best available, with a little noise. Not simply the top name:
        /// a draft that always resolves the same way is a sorting algorithm, and half the
        /// point of the event is that nobody quite knows where anyone will end up.
        /// </summary>
        public DraftPick? AutoPick()
        {
            if (Complete) return null;

            int window = Math.Min(3, Available.Count);
            return Pick(Available[_rng.Next(window)]);
        }

        public void AutoComplete()
        {
            while (!Complete) AutoPick();
        }
    }

    /// <summary>
    /// The draft: the single best tool for sustaining a brand split
    /// (docs/wrestling-reference/22-brand-splits.md §5.1). It refreshes every roster and
    /// every possible pairing at once, resolves stale feuds, lets the promotion correct
    /// imbalance between brands, and re-establishes that brand membership is real.
    ///
    /// Annual cadence is right. More often destabilises, less often lets rosters stagnate,
    /// so the repair a draft delivers scales with how long it has been.
    /// </summary>
    public static class Draft
    {
        /// <summary>Days between drafts the split is tuned for.</summary>
        public const int AnnualDays = 365;

        /// <summary>Share of the lost integrity a same-week draft brings back.</summary>
        private const double MinRepairShare = 0.20;

        /// <summary>Share of the lost integrity a draft held on time brings back.</summary>
        private const double MaxRepairShare = 0.65;

        /// <summary>When the next draft is due, or null before the promotion has split.</summary>
        public static DateOnly? DueOn(BrandSplit split)
        {
            var anchor = split.LastDraftOn ?? split.StartedOn;
            return anchor?.AddDays(AnnualDays);
        }

        /// <summary>Days since the last draft, or since the split began if there has not been one.</summary>
        public static int DaysSince(BrandSplit split, DateOnly today)
        {
            var anchor = split.LastDraftOn ?? split.StartedOn;
            return anchor is null ? 0 : Math.Max(0, today.DayNumber - anchor.Value.DayNumber);
        }

        public static bool IsDue(BrandSplit split, DateOnly today) =>
            DueOn(split) is { } due && today >= due;

        /// <summary>
        /// How much of the gap between integrity and its ceiling a draft closes. A draft
        /// held on schedule is an event and does most of the work; one held three months
        /// after the last is a roster shuffle and does very little.
        /// </summary>
        public static double RepairShare(int daysSincePrevious) =>
            MinRepairShare
            + (MaxRepairShare - MinRepairShare) * Math.Clamp(daysSincePrevious / (double)AnnualDays, 0, 1);

        /// <summary>
        /// Commits a completed board. Assigns every pick, clears the pairing fatigue that a
        /// reshuffled roster has made irrelevant, ends feuds the draft has separated, and
        /// restores integrity toward its ceiling.
        ///
        /// The ceiling is untouched. A draft re-establishes that brand membership is real;
        /// it does not un-tell the audience what the crossovers already told them.
        /// </summary>
        public static DraftOutcome Apply(Career career, DraftBoard board)
        {
            var split = career.Brands;
            double before = split.Integrity;
            int daysSince = DaysSince(split, career.CurrentDate);

            int moved = 0;
            foreach (var pick in board.Picks)
            {
                if (pick.IsMove) moved++;
                split.Assign(pick.Wrestler, pick.To);
            }

            // Every pairing is fresh again: the audience has not seen these two on the same
            // show since the reshuffle, whoever they used to work with. Doc 22 §5.1.
            int refreshed = 0;
            int ended = 0;

            foreach (var feud in career.FeudBook.AllIncludingDormant)
            {
                if (feud.MatchCount > 0)
                {
                    feud.MatchCount = 0;
                    refreshed++;
                }

                var a = split.BrandOf(feud.WrestlerA.Id);
                var b = split.BrandOf(feud.WrestlerB.Id);

                // Split across brands, so there is no show left on which to continue it.
                if (a is not null && b is not null && a.Id != b.Id && feud.Heat > 0)
                {
                    feud.Conclude();
                    ended++;
                }
            }

            double repair = (split.Ceiling - split.Integrity) * RepairShare(daysSince);
            split.Integrity = Math.Clamp(split.Integrity + repair, 0, split.Ceiling);
            split.LastDraftOn = career.CurrentDate;

            return new DraftOutcome
            {
                Picks             = board.Picks.ToList(),
                Moved             = moved,
                FeudsEnded        = ended,
                PairingsRefreshed = refreshed,
                IntegrityBefore   = before,
                IntegrityAfter    = split.Integrity,
                Ceiling           = split.Ceiling,
                DaysSincePrevious = daysSince,
                WasEarly          = daysSince < AnnualDays
            };
        }
    }
}
