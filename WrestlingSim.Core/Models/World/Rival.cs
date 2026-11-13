using WrestlingSim.Enums;

namespace WrestlingSim.Models.World
{
    /// <summary>
    /// Why a promotion exists, which predicts how it is run. Doc 01 §7: "whose money is it, and
    /// what do they want it to do?" Each is a package of behaviour, not a stat spread.
    /// </summary>
    public enum FounderMotive
    {
        /// <summary>Cost discipline, value signings, willing to cut favourites. Fails by under-investing.</summary>
        Business,
        /// <summary>An active wrestler's promotion: the owner's favourites are protected. Caps at the owner's draw.</summary>
        Vanity,
        /// <summary>"Wrestling should be X": a coherent, well-worked product with a devoted core. Refuses to broaden.</summary>
        Ideology,
        /// <summary>Inherited: steady, traditional, slow to change.</summary>
        Legacy,
        /// <summary>A wealthy fan: overspends on names, burns capital fast — typically 18–36 months.</summary>
        MarkMoney,
        /// <summary>A media company's content inventory: consistent television, creative subordinate to scheduling.</summary>
        Platform
    }

    /// <summary>
    /// Another promotion in the same world: a business with a roster, books and — at a television
    /// tier — a TV deal, run on the clock. Its shows are abstract (no beats are simulated) but its
    /// money is not: it sells its house through the same box office as the player's, pays the same
    /// prices, and can go broke.
    /// </summary>
    public class RivalPromotion
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public required Promotion Promotion { get; set; }
        public FounderMotive Motive { get; set; }

        public CareerBooks Books { get; set; } = new();
        public TvDeal? Television { get; set; }
        public List<Contract> Contracts { get; set; } = new();

        /// <summary>
        /// What this promotion's own audience has been shown. A wrestler stale here is fresh
        /// to the player's crowd, and the other way about.
        /// </summary>
        public Audience Audience { get; set; } = new();

        /// <summary>
        /// Its match-ups and stories. Mostly a memory of who has met whom and how often — the
        /// thing that goes stale fastest (doc 17 §4.1) — kept in the same book the player's are,
        /// so the box office reads a rival's rematch exactly as it reads the player's.
        /// </summary>
        public Engine.FeudBook Feuds { get; set; } = new();

        /// <summary>Its championships, on the same prestige model as the player's.</summary>
        public TitleRegistry Titles { get; set; } = new();

        /// <summary>
        /// Holds its roster still: no signings, releases, bids or re-signings. For measuring
        /// what a given roster does, not for play. Not saved.
        /// </summary>
        public bool Frozen { get; set; }

        /// <summary>What it wants, if not its founder motive's default. See <see cref="Engine.World.ObjectiveOf"/>.</summary>
        public Objective? Objective { get; set; }

        /// <summary>How it sees the future, if not its founder motive's default.</summary>
        public Disposition? Disposition { get; set; }

        /// <summary>The owner's chosen wrestlers, by id. Empty unless somebody chose some.</summary>
        public List<string> Favourites { get; set; } = new();

        /// <summary>What it decided lately and why, newest last. Capped.</summary>
        public List<RivalDecision> Decisions { get; set; } = new();

        /// <summary>
        /// The situation it last planned in and found nothing worth doing, and when. While
        /// that has not changed it does not plan again for a month. Zero when it has no such memory.
        /// </summary>
        public int QuietAs { get; set; }
        public DateOnly? QuietSince { get; set; }

        /// <summary>When its next show runs. Advanced by its tier's cadence.</summary>
        public DateOnly NextShow { get; set; }

        /// <summary>Its recent nights, newest last. Capped.</summary>
        public List<RivalNight> Nights { get; set; } = new();

        /// <summary>The day it closed, or null while it is still running.</summary>
        public DateOnly? Folded { get; set; }

        public bool IsActive => Folded is null;
        public string Name => Promotion.Name;

        /// <summary>
        /// Their deal here, through an index kept beside the list. Rebuilt whenever the list is
        /// a different list, a different length, or ends in a different contract — every way
        /// the game changes it does one of those.
        /// </summary>
        public Contract? ContractOf(Wrestler w)
        {
            int count = Contracts.Count;
            var last = count == 0 ? null : Contracts[count - 1];
            if (_deals is null || !ReferenceEquals(_dealsOf, Contracts) || _dealsCount != count || !ReferenceEquals(_dealsLast, last))
            {
                _deals = new Dictionary<string, Contract>(count);
                foreach (var c in Contracts) _deals.TryAdd(c.WrestlerId, c);
                (_dealsOf, _dealsCount, _dealsLast) = (Contracts, count, last);
            }
            return _deals.TryGetValue(w.Id, out var deal) ? deal : null;
        }

        private Dictionary<string, Contract>? _deals;
        private List<Contract>? _dealsOf;
        private int _dealsCount;
        private Contract? _dealsLast;
        public decimal DailyPayroll => Contracts.Sum(c => c.DailyCost);
    }

    /// <summary>One of a rival's shows, as the world sees it.</summary>
    public sealed class RivalNight
    {
        public DateOnly Date { get; init; }
        public ShowType Type { get; init; }
        public double Rating { get; init; }
        public int Attendance { get; init; }
        public int Capacity { get; init; }
        public int Viewers { get; init; }
        public decimal Net { get; init; }
        public string Headline { get; init; } = "";

        /// <summary>The card, in running order, as a line each.</summary>
        public List<string> Card { get; init; } = new();

        /// <summary>How much of the slot the card filled.</summary>
        public double Filled { get; init; } = 1.0;
    }

    /// <summary>
    /// Doc 13 §3.2: a released wrestler cannot work anywhere for ninety days. Nobody can sign or
    /// book them until it runs out.
    /// </summary>
    public sealed class NonCompete
    {
        public required string WrestlerId { get; init; }
        public required DateOnly Until { get; init; }
        public required string From { get; init; }
    }

    /// <summary>
    /// An offer for a wrestler whose deal is running out, to take effect when it does. Doc 10 §6.2:
    /// a competing offer is the only real leverage a wrestler has — the incumbent has to match it.
    /// </summary>
    public sealed class Bid
    {
        public required string WrestlerId { get; init; }

        /// <summary>The bidder: a rival's id, or <see cref="Career.PlayerId"/>.</summary>
        public required string Bidder { get; init; }
        public required decimal Annual { get; init; }
        public required int Years { get; init; }
        public required DateOnly Made { get; init; }
    }

    /// <summary>A line of industry news.</summary>
    public sealed class NewsItem
    {
        public required DateOnly Date { get; init; }
        public required string Text { get; init; }

        /// <summary>True when it touches the player's roster, for the dashboard to lead with.</summary>
        public bool AboutYou { get; init; }
    }
}
