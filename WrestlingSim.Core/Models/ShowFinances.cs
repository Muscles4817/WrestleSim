namespace WrestlingSim.Models
{
    /// <summary>
    /// What one card item did to make people buy a ticket. Worked out from the card as it
    /// was advertised, before the bell — a ticket is bought for the match it promises, never
    /// for the match it turns out to be (docs/wrestling-reference/18-match-craft.md §7).
    /// </summary>
    public sealed class ItemDraw
    {
        public required string Label { get; init; }

        /// <summary>The names on the poster: the most over person on each side, best first.</summary>
        public List<string> Headliners { get; init; } = new();

        /// <summary>How over the people in it are, 0–1. "Both performers are over."</summary>
        public double StarPower { get; init; }

        /// <summary>
        /// How much of the headliners' pull the audience still feels, already inside
        /// <see cref="StarPower"/>. Below 1.0 when their act has gone stale with the people
        /// watching. Kept so the report can say so.
        /// </summary>
        public double Acts { get; init; } = 1.0;

        /// <summary>What the feud adds: 1.0 with no story, up to 1.5 for a hot one the crowd trusts.</summary>
        public double Build { get; init; } = 1.0;

        /// <summary>What the pairing is still worth against the first time the crowd saw it.</summary>
        public double Freshness { get; init; } = 1.0;

        /// <summary>What a belt or a stipulation adds. 1.0 when nothing is on the line.</summary>
        public double Stakes { get; init; } = 1.0;

        /// <summary>How much of a reason to buy a ticket this is. The product of the four.</summary>
        public double Pull => StarPower * Build * Freshness * Stakes;
    }

    /// <summary>
    /// A night's books. Everything in here except <see cref="ReputationAfter"/> and
    /// <see cref="CashAfter"/> is known before the show is run — which is the whole point:
    /// story sells the ticket, and quality only sells the next one.
    /// </summary>
    public sealed class ShowFinances
    {
        // ── The house ────────────────────────────────────────────────────────

        /// <summary>Every item, strongest reason to buy a ticket first.</summary>
        public List<ItemDraw> Draws { get; init; } = new();

        /// <summary>The card's pull: 60% the top item, 25% the next, 15% the third.</summary>
        public double CardPull { get; init; }

        /// <summary>
        /// What the length of the card did to the house, 0.5–1.0. Below 1.0 when the card
        /// leaves the slot under-filled — see <see cref="Engine.AudienceModel.CardDepth"/>.
        /// </summary>
        public double CardDepth { get; init; } = 1.0;

        public int Capacity { get; init; }

        /// <summary>How many wanted to come, before the building said no to any of them.</summary>
        public int Demand { get; init; }

        public int Attendance => Math.Min(Capacity, Demand);
        public int TurnedAway => Math.Max(0, Demand - Capacity);
        public bool SoldOut => Demand >= Capacity && Capacity > 0;
        public double Fill => Capacity == 0 ? 0 : Attendance / (double)Capacity;

        /// <summary>Net to the promotion per head, after fees.</summary>
        public decimal TicketPrice { get; init; }

        // ── Money in ─────────────────────────────────────────────────────────

        public decimal Gate { get; init; }
        public decimal Merchandise { get; init; }

        /// <summary>
        /// The television money: a flat fee per episode, the same whatever is on the card.
        /// Flat on purpose and for now — the renewal that makes booking move it is not built.
        /// </summary>
        public decimal MediaRights { get; init; }

        // ── Money out ────────────────────────────────────────────────────────

        /// <summary>Appearance fees, paid to the people who worked tonight and nobody else.</summary>
        public decimal Talent { get; init; }

        /// <summary>The building, the ring crew, security.</summary>
        public decimal Operations { get; init; }

        /// <summary>Trucks, crew and set for a televised night. Zero off television.</summary>
        public decimal Production { get; init; }

        /// <summary>
        /// How many chose to watch a televised episode, on the card as advertised. Zero on
        /// anything that was not a televised episode.
        /// </summary>
        public int TuneIn { get; init; }

        /// <summary>
        /// The average audience once the show's quality had kept or lost them. Set after the
        /// bell; this, not the tune-in, is what the renewal counts.
        /// </summary>
        public int Viewers { get; set; }

        public bool WasTelevised => TuneIn > 0;

        /// <summary>
        /// The run of towns the rest of the roster worked, when there was one. Settable so a
        /// settled run can replace the projected one without copying the rest of the night.
        /// </summary>
        public RoadTakings? Road { get; set; }

        public decimal CardRevenue => Gate + Merchandise + MediaRights;
        public decimal CardCosts => Talent + Operations + Production;

        public decimal Revenue => CardRevenue + (Road?.Revenue ?? 0);
        public decimal Costs => CardCosts + (Road?.Costs ?? 0);
        public decimal Net => Revenue - Costs;

        // ── After the bell ───────────────────────────────────────────────────

        public double ReputationBefore { get; init; }

        /// <summary>Where reputation stood once the show's quality had been counted.</summary>
        public double ReputationAfter { get; set; }

        /// <summary>The promotion's balance once tonight's net was banked.</summary>
        public decimal CashAfter { get; set; }

        /// <summary>
        /// Whether the card was booked at all, as opposed to a date that was only a run of
        /// towns. A night with no card sells no tickets and moves no reputation. Read off the
        /// draws, not the capacity: a card run in a building with no seats recorded is still a
        /// card, with a TV fee, costs and a rating.
        /// </summary>
        public bool HadCard => Draws.Count > 0;
    }

    /// <summary>A run of towns, as money: the same house show sold night after night.</summary>
    public sealed class RoadTakings
    {
        public int Towns { get; init; }
        public int CapacityPerNight { get; init; }
        public int AttendancePerNight { get; init; }

        /// <summary>The names on the house-show poster.</summary>
        public List<string> Headliners { get; init; } = new();

        public decimal Gate { get; init; }
        public decimal Merchandise { get; init; }
        public decimal Talent { get; init; }
        public decimal Operations { get; init; }

        public decimal Revenue => Gate + Merchandise;
        public decimal Costs => Talent + Operations;
        public decimal Net => Revenue - Costs;
    }
}
