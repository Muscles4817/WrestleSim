using WrestlingSim.Enums;

namespace WrestlingSim.Models.World
{
    /// <summary>
    /// Every belt the promotion has, and the one rule that can only be enforced across
    /// all of them: **the audience's attention is finite**.
    ///
    /// docs/wrestling-reference/21-championships.md §2.1 — "each additional title divides
    /// the prestige available… the audience's attention is finite, and a title it isn't
    /// paying attention to isn't a title." So the registry keeps a fixed capacity, adds up
    /// what the active slate is asking for, and hands every title the share of its own
    /// standing it is allowed to keep.
    ///
    /// The consequence the design wants: adding a fourth belt costs prestige on the three
    /// you already had, without any of them having been booked badly. "Should we add a
    /// title?" is a decision with a price on it.
    /// </summary>
    public class TitleRegistry
    {
        private readonly List<Title> _titles = new();

        /// <summary>
        /// How much championship a promotion's audience can hold in its head at once,
        /// in units of <see cref="Title.AttentionClaim"/>.
        ///
        /// Set to exactly the doc's recommended slate — one world title (1.00), one
        /// secondary (0.60) and a women's world title (1.00) — so the standard three
        /// belts are fully affordable and the fourth is not. Doc 21 §2 gives that shape
        /// as the ideal count.
        /// </summary>
        public const double AttentionCapacity = 2.60;

        public IReadOnlyList<Title> All => _titles;

        public IReadOnlyList<Title> Active =>
            _titles.Where(t => !t.Retired).ToList();

        public IReadOnlyList<Title> Retired =>
            _titles.Where(t => t.Retired).ToList();

        public int Count => _titles.Count;

        // ── The fixed pool ───────────────────────────────────────────────────

        /// <summary>Total attention the active slate is asking for.</summary>
        public double Demand => _titles.Sum(t => t.AttentionClaim);

        /// <summary>
        /// Share of its own standing each title keeps, 0–1. One while the slate fits
        /// inside the capacity — a lean slate is never penalised, but neither can it
        /// manufacture prestige out of scarcity alone; that has to be earned in the ring.
        /// </summary>
        public double Dilution =>
            Demand <= AttentionCapacity ? 1.0 : AttentionCapacity / Demand;

        /// <summary>
        /// Attention left before the next belt starts costing the others anything.
        /// Negative once the slate is over-committed.
        /// </summary>
        public double SpareAttention => AttentionCapacity - Demand;

        /// <summary>
        /// What adding a belt of this tier would do to every existing title's prestige,
        /// as a fraction. Zero means it is free; 0.19 means everything loses 19%.
        /// This is the number the "add a title" screen has to show before you commit.
        /// </summary>
        public double DilutionCostOfAdding(TitleTier tier)
        {
            double claim = tier switch
            {
                TitleTier.World     => 1.00,
                TitleTier.Secondary => 0.60,
                _                   => 0.35
            };

            double after = Demand + claim;
            double dilutionAfter = after <= AttentionCapacity ? 1.0 : AttentionCapacity / after;
            return Math.Max(0, Dilution - dilutionAfter) / Math.Max(Dilution, 0.0001);
        }

        /// <summary>
        /// Pushes the current dilution onto every title. Called after anything that
        /// changes the slate. Mutation is explicit rather than a back-pointer so a Title
        /// stays a plain serialisable object.
        /// </summary>
        public void Rebalance()
        {
            double dilution = Dilution;
            foreach (var title in _titles) title.Dilution = dilution;
        }

        // ── Slate ────────────────────────────────────────────────────────────

        public Title Add(Title title)
        {
            _titles.Add(title);
            Rebalance();
            return title;
        }

        /// <summary>
        /// Introduces a belt mid-career. It starts with no lineage and a standing to
        /// match — doc 21 §7: replacing a title trades accumulated history for a fresh
        /// start, and that is almost always a net loss.
        /// </summary>
        public Title Create(string name, TitleTier tier, Division division, DateOnly on) =>
            Add(new Title
            {
                Name        = string.IsNullOrWhiteSpace(name) ? "New Championship" : name.Trim(),
                Tier        = tier,
                Division    = division,
                Established = on,
                Standing    = Title.NewTitleStanding
            });

        /// <summary>
        /// Retires a belt. The lineage stays — history is not deleted — but it stops
        /// claiming attention, so the titles left behind immediately get more of it.
        /// A running reign is closed as a vacancy.
        /// </summary>
        public void Retire(Title title, DateOnly on)
        {
            if (title.Retired) return;

            if (title.CurrentReign is { } reign)
            {
                reign.Lost    = on;
                reign.Vacated = true;
                reign.LostAt  = "Title retired";
            }

            title.Retired  = true;
            title.RetiredOn = on;
            Rebalance();
        }

        /// <summary>Brings a retired belt back with its lineage intact.</summary>
        public void Revive(Title title)
        {
            title.Retired   = false;
            title.RetiredOn = null;
            Rebalance();
        }

        /// <summary>Removes a belt and its history outright. Only for undoing a mistake.</summary>
        public bool Remove(Title title)
        {
            bool removed = _titles.Remove(title);
            if (removed) Rebalance();
            return removed;
        }

        // ── Queries ──────────────────────────────────────────────────────────

        public Title? Find(string id) =>
            _titles.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>Belts this person currently holds. Usually none or one.</summary>
        public IReadOnlyList<Title> HeldBy(Wrestler w) =>
            _titles.Where(t => !t.Retired && t.Champion == w).ToList();

        public bool IsChampion(Wrestler w) => HeldBy(w).Count > 0;

        /// <summary>Active belts, most prestigious first — the promotion's real hierarchy.</summary>
        public IReadOnlyList<Title> ByPrestige =>
            Active.OrderByDescending(t => t.Prestige).ThenBy(t => t.Name).ToList();

        // ── The shipped slate ────────────────────────────────────────────────

        /// <summary>
        /// The three belts a promotion starts with: a world title, a secondary title as
        /// an elevation tool, and a women's world title. Doc 21 §2's ideal count, and
        /// exactly the attention capacity — so the player's first title decision is
        /// whether a fourth is worth what it costs the other three.
        ///
        /// All singles. The match engine is strictly one against one, so there is no tag
        /// division for a tag title to define.
        /// </summary>
        public void SeedDefaults(string promotionName, DateOnly established)
        {
            string prefix = string.IsNullOrWhiteSpace(promotionName) ? "" : promotionName.Trim() + " ";

            Add(new Title
            {
                Name        = $"{prefix}World Heavyweight Championship",
                Tier        = TitleTier.World,
                Division    = Division.Mens,
                Established = established,
                Standing    = Title.FoundingStandingFor(TitleTier.World)
            });

            Add(new Title
            {
                Name        = $"{prefix}Television Championship",
                Tier        = TitleTier.Secondary,
                Division    = Division.Mens,
                Established = established,
                Standing    = Title.FoundingStandingFor(TitleTier.Secondary)
            });

            Add(new Title
            {
                Name        = $"{prefix}Women's World Championship",
                Tier        = TitleTier.World,
                Division    = Division.Womens,
                Established = established,
                Standing    = Title.FoundingStandingFor(TitleTier.World)
            });

            Rebalance();
        }
    }
}
