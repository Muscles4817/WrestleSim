using WrestlingSim.Enums;

namespace WrestlingSim.Models.World
{
    /// <summary>
    /// A championship. A belt is a prop; what a title is worth is entirely what the
    /// audience believes about it — docs/wrestling-reference/21-championships.md §1.
    ///
    /// Two numbers do the work, and they are deliberately separate:
    ///
    ///   • <see cref="Standing"/> is what this title has *earned* on its own history —
    ///     long reigns, credible defences, big matches, a champion the audience believes
    ///     in, minus the churn, the non-title losses and the vacancies (§3, §4).
    ///
    ///   • <see cref="Prestige"/> is what that standing is actually worth once it is
    ///     competing for a finite audience. Attention is a fixed pool divided among the
    ///     belts (§2.1), so a fourth title makes the other three worth less without any
    ///     of them having done anything wrong. That dilution is set by
    ///     <see cref="TitleRegistry"/>, which is the only thing that can see the whole
    ///     slate.
    ///
    /// Prestige is what everything else reads: the crowd's investment in a title match,
    /// the status a challenger takes from winning one, and the size of the hole left when
    /// the belt changes hands.
    /// </summary>
    public class Title
    {
        /// <summary>Stable id so saves and card items can refer to this belt.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = "Championship";

        public TitleTier Tier { get; set; } = TitleTier.World;

        /// <summary>
        /// Which division contests it. Not enforced in booking — an intergender title
        /// match is a legitimate if unusual call, the same rule the match builder uses.
        /// </summary>
        public Division Division { get; set; } = Division.Mens;

        /// <summary>When the belt was introduced. Lineage is measured from here.</summary>
        public DateOnly Established { get; set; }

        /// <summary>
        /// 0–100. What this title has earned. See the class remarks for why this is not
        /// the same thing as prestige.
        /// </summary>
        public double Standing { get; set; }

        /// <summary>
        /// Share of its own standing this belt actually gets to keep, 0–1. Owned by
        /// <see cref="TitleRegistry"/> and recalculated whenever the slate changes; the
        /// default of 1 means a title held on its own in a test is undiluted.
        /// </summary>
        public double Dilution { get; internal set; } = 1.0;

        /// <summary>A retired title keeps its lineage and stops claiming any attention.</summary>
        public bool Retired { get; set; }

        public DateOnly? RetiredOn { get; set; }

        /// <summary>Every reign, oldest first. Closed reigns are never removed (§7).</summary>
        public List<TitleReign> Lineage { get; set; } = new();

        // ── Baselines ────────────────────────────────────────────────────────

        /// <summary>
        /// Standing a title of this tier carries when the promotion has always had it.
        /// The world title is the top of the card by definition, so it starts ahead.
        /// </summary>
        public static double FoundingStandingFor(TitleTier tier) => tier switch
        {
            TitleTier.World     => 62,
            TitleTier.Secondary => 44,
            _                   => 32
        };

        /// <summary>
        /// Where a belt introduced mid-career starts. Deliberately low whatever tier it
        /// is declared at: a new title has no lineage, and history is most of what a
        /// title is worth (§7). It has to be built.
        /// </summary>
        public const double NewTitleStanding = 18;

        /// <summary>
        /// How much of the audience's attention this tier lays claim to. A second world
        /// title costs nearly three times what a specialty belt costs, which is doc 21
        /// §2.1's central point stated as a number.
        /// </summary>
        public double AttentionClaim => Retired ? 0 : Tier switch
        {
            TitleTier.World     => 1.00,
            TitleTier.Secondary => 0.60,
            _                   => 0.35
        };

        // ── Derived ──────────────────────────────────────────────────────────

        /// <summary>What the title is worth once the slate's dilution is applied, 0–100.</summary>
        public double Prestige => Math.Clamp(Standing * Dilution, 0, 100);

        /// <summary>The running reign, or null when the title is vacant.</summary>
        public TitleReign? CurrentReign =>
            Lineage.Count > 0 && Lineage[^1].IsCurrent ? Lineage[^1] : null;

        public Wrestler? Champion => CurrentReign?.Champion;

        public bool IsVacant => CurrentReign == null;

        /// <summary>How many people have held it. "The 42nd champion" (§7).</summary>
        public int ReignCount => Lineage.Count;

        /// <summary>Defences under the current champion. Zero for a vacant title.</summary>
        public int CurrentDefences => CurrentReign?.Defences ?? 0;

        public int CurrentReignDays(DateOnly today) => CurrentReign?.DaysHeld(today) ?? 0;

        /// <summary>
        /// Extra crowd energy a match carries because this is on the line. Scaled to sit
        /// alongside a feud's starting bonus rather than dwarf it — a title creates
        /// automatic stakes (§1.1), it does not manufacture a reaction on its own.
        /// </summary>
        public double StakesBonus => Prestige / 100.0 * 14.0;

        public string TierLabel => Tier switch
        {
            TitleTier.World     => "World",
            TitleTier.Secondary => "Secondary",
            _                   => "Specialty"
        };

        /// <summary>Plain reading of where the belt currently stands, for the UI.</summary>
        public string PrestigeLabel => Prestige switch
        {
            >= 80 => "The prize in the business",
            >= 65 => "Genuinely prestigious",
            >= 50 => "Respected",
            >= 35 => "Means something",
            >= 20 => "A prop with a name on it",
            _     => "Nobody is chasing it"
        };

        // ── Lineage helpers ──────────────────────────────────────────────────

        public IEnumerable<TitleReign> ReignsOf(Wrestler w) =>
            Lineage.Where(r => r.Champion == w);

        /// <summary>The longest reign in the belt's history, for the lineage display.</summary>
        public TitleReign? LongestReign(DateOnly today) =>
            Lineage.OrderByDescending(r => r.DaysHeld(today)).FirstOrDefault();
    }
}
