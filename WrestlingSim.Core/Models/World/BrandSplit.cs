using WrestlingSim.Models;

namespace WrestlingSim.Models.World
{
    /// <summary>
    /// A single crossover: someone worked a show that was not their brand's.
    ///
    /// Kept as a ledger rather than a counter because the point of the mechanic is that no
    /// individual entry looks wrong. The player needs to be able to read back the list of
    /// decisions that each seemed correct at the time
    /// (docs/wrestling-reference/22-brand-splits.md §4.1).
    /// </summary>
    public sealed class CrossoverRecord
    {
        public required string WrestlerId { get; init; }
        public required string WrestlerName { get; init; }
        public required string HomeBrandName { get; init; }
        public required string ShowBrandName { get; init; }
        public required string ShowName { get; init; }
        public DateOnly Date { get; init; }

        /// <summary>Split integrity this appearance cost.</summary>
        public double Cost { get; init; }
    }

    /// <summary>
    /// The promotion's brand structure, and how much of it is still real.
    ///
    /// <see cref="Integrity"/> is the headline number. It starts whole and falls every time
    /// someone appears on a show that is not their brand's. Doc 22 §4.1 describes the
    /// failure mode this models: every individual crossover is locally defensible — the
    /// biggest star on the show that needs the rating always is — and the aggregate of
    /// locally correct decisions destroys the structure.
    ///
    /// Part of every crossover is permanent. <see cref="Ceiling"/> is what integrity can be
    /// restored to, and it only ever falls. A draft can bring integrity back up to the
    /// ceiling (doc 22 §5.1) but nothing brings the ceiling back, which is why a split can
    /// be run carefully for years and still end up meaning nothing.
    /// </summary>
    public class BrandSplit
    {
        /// <summary>Whether the promotion is currently split. False means one roster.</summary>
        public bool Active { get; set; }

        public List<Brand> Brands { get; set; } = new();

        /// <summary>0–100. How much the split still means. Never rises above <see cref="Ceiling"/>.</summary>
        public double Integrity { get; set; } = 100;

        /// <summary>The share of past crossovers that cannot be undone. Only ever grows.</summary>
        public double PermanentErosion { get; set; }

        public DateOnly? StartedOn { get; set; }
        public DateOnly? LastDraftOn { get; set; }

        /// <summary>Every crossover the player has booked, oldest first. Trimmed for save size.</summary>
        public List<CrossoverRecord> Crossovers { get; set; } = new();

        /// <summary>Total crossovers ever booked, including ones trimmed off the ledger.</summary>
        public int CrossoverCount { get; set; }

        /// <summary>Ledger entries kept. Older ones stop being informative and bloat the save.</summary>
        public const int LedgerLimit = 60;

        /// <summary>The highest integrity can be restored to. Falls with every crossover.</summary>
        public double Ceiling => Math.Clamp(100 - PermanentErosion, 0, 100);

        // ── Queries ──────────────────────────────────────────────────────────

        public Brand? Find(string? brandId) =>
            brandId is null ? null : Brands.FirstOrDefault(b => b.Id == brandId);

        /// <summary>The brand this person belongs to, or null if they are unassigned.</summary>
        public Brand? BrandOf(string wrestlerId) =>
            Brands.FirstOrDefault(b => b.Contains(wrestlerId));

        public Brand? BrandOf(Wrestler wrestler) => BrandOf(wrestler.Id);

        /// <summary>Roster members who have not been put on a brand.</summary>
        public IEnumerable<Wrestler> Unassigned(IEnumerable<Wrestler> roster) =>
            roster.Where(w => BrandOf(w.Id) is null);

        /// <summary>
        /// True when this person appearing on <paramref name="showBrand"/> is a crossover.
        ///
        /// A show with no brand — the company-wide premium event, the inter-brand supercard
        /// of doc 22 §3.4 — is never a crossover. That is the legitimate release valve, and
        /// having one is most of the difference between a split with discipline and a split
        /// without.
        /// </summary>
        public bool IsCrossover(Wrestler wrestler, Brand? showBrand)
        {
            if (!Active || showBrand is null) return false;

            var home = BrandOf(wrestler.Id);

            // Unassigned people are free agents, not trespassers.
            return home is not null && home.Id != showBrand.Id;
        }

        // ── Mutation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Puts someone on a brand, taking them off any other. Passing null leaves them
        /// unassigned.
        /// </summary>
        public void Assign(Wrestler wrestler, Brand? brand)
        {
            foreach (var b in Brands) b.RosterIds.Remove(wrestler.Id);
            brand?.RosterIds.Add(wrestler.Id);
        }

        /// <summary>
        /// Records a crossover and takes its cost off integrity. The only place integrity
        /// falls, and the only place the ceiling moves.
        /// </summary>
        public void ApplyCrossover(CrossoverRecord record, double permanentShare)
        {
            Crossovers.Add(record);
            CrossoverCount++;
            while (Crossovers.Count > LedgerLimit) Crossovers.RemoveAt(0);

            PermanentErosion = Math.Clamp(PermanentErosion + record.Cost * permanentShare, 0, 100);
            Integrity = Math.Clamp(Integrity - record.Cost, 0, Ceiling);
        }

        /// <summary>
        /// Ends the split. Brand membership is cleared, but the erosion is kept: a promotion
        /// that abandoned one split and started another does not get to pretend the audience
        /// forgot the first.
        /// </summary>
        public void End()
        {
            Active = false;
            Brands.Clear();
            StartedOn = null;
            LastDraftOn = null;
        }
    }
}
