using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>Someone appearing on a show that is not their brand's, and what it costs.</summary>
    public sealed record Crossover(Wrestler Wrestler, Brand Home, Brand ShowBrand, double Cost);

    /// <summary>
    /// What the show simulator needs to know about brands: the split, and which brand's
    /// show this is. Null anywhere the question does not arise — exhibition mode, or a
    /// promotion that has not split — so the engine keeps working with no brands at all.
    /// </summary>
    public sealed record BrandContext(BrandSplit Split, Brand? HomeBrand)
    {
        /// <summary>True when exclusivity is actually being enforced on this show.</summary>
        public bool Enforced => Split.Active && HomeBrand is not null;
    }

    /// <summary>Whether a promotion is deep enough to split, and what is thin about it.</summary>
    public sealed record SplitDepthReport(
        int RosterSize,
        int BrandCount,
        int PerBrand,
        IReadOnlyList<string> Warnings)
    {
        public bool IsThin => Warnings.Count > 0;
    }

    /// <summary>
    /// The economics of a brand split.
    ///
    /// One loop carries the whole of docs/wrestling-reference/22-brand-splits.md:
    ///
    ///   • Putting a star on the other brand's show makes that show better tonight
    ///     (<see cref="AttractionBonus"/>). Every single time, this is the locally correct
    ///     decision — it is the biggest name available on the show that needs it most.
    ///   • It costs split integrity (<see cref="CostOf"/>), and part of that cost is
    ///     permanent (<see cref="PermanentShare"/>).
    ///   • As integrity falls, the attraction bonus falls with it — a crossover stops being
    ///     an event once the audience no longer believes the brands are separate — while
    ///     the cost does not.
    ///   • Low integrity degrades each brand's ability to make its own stars
    ///     (<see cref="StarMakingFactor"/>) and the value of brand-exclusive booking
    ///     (<see cref="ExclusivityBonus"/>), which is doc 22's "the split exists only in
    ///     graphics" end state expressed as numbers.
    ///
    /// Everything here is pure. The caller decides whether to apply any of it.
    /// </summary>
    public static class BrandIntegrity
    {
        // ── Tuning ───────────────────────────────────────────────────────────

        /// <summary>Integrity cost of crossing over a performer nobody is invested in.</summary>
        public const double CostFloor = 1.2;

        /// <summary>Extra cost at the top of the card. The bigger the star, the more it says.</summary>
        public const double CostRange = 2.4;

        /// <summary>
        /// How much of each crossover's cost is permanent. Doc 22 §4.1: the erosion is
        /// one-way. A draft can restore the rest; nothing restores this.
        /// </summary>
        public const double PermanentShare = 0.45;

        /// <summary>Most of an item's score a crossover can add, at full integrity, for the biggest star.</summary>
        private const double MaxAttraction = 0.18;

        /// <summary>Most a fully exclusive brand show gains from being exclusive.</summary>
        private const double MaxExclusivityBonus = 0.05;

        /// <summary>Star-making left when integrity has gone entirely.</summary>
        private const double StarMakingFloor = 0.50;

        /// <summary>Star-making left on a brand the audience has decided is the B-show.</summary>
        private const double SecondaryFloor = 0.80;

        /// <summary>How far below the leading brand's form counts as visibly secondary.</summary>
        private const double SecondaryThreshold = 0.90;

        /// <summary>Shows of form each brand needs before the comparison means anything.</summary>
        public const int FormSampleRequired = 3;

        // ── Crossovers ───────────────────────────────────────────────────────

        /// <summary>
        /// What crossing this person over costs. Scaled by how over they are, because the
        /// statement being made to the audience is proportional to who is making it: a
        /// jobber on the wrong show is a scheduling error, the world's biggest star on the
        /// wrong show is an announcement that the brands are not separate.
        /// </summary>
        public static double CostOf(Wrestler wrestler) =>
            CostFloor + CostRange * Math.Clamp(wrestler.EffectiveOverness / 100.0, 0, 1);

        /// <summary>
        /// Everyone on this card who does not belong on this show. One entry per person,
        /// however many items they work — the audience notices that they are here, not how
        /// many times.
        /// </summary>
        public static IReadOnlyList<Crossover> Detect(
            IEnumerable<ICardItem> card, BrandSplit split, Brand? showBrand)
        {
            var found = new List<Crossover>();
            if (!split.Active || showBrand is null) return found;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var wrestler in card.SelectMany(i => i.Wrestlers))
            {
                if (!seen.Add(wrestler.Id)) continue;

                var home = split.BrandOf(wrestler.Id);
                if (home is null || home.Id == showBrand.Id) continue;

                found.Add(new Crossover(wrestler, home, showBrand, CostOf(wrestler)));
            }

            return found;
        }

        /// <summary>Total integrity a card would cost if it were run as booked.</summary>
        public static double CostOfCard(IEnumerable<ICardItem> card, BrandSplit split, Brand? showBrand) =>
            Detect(card, split, showBrand).Sum(c => c.Cost);

        /// <summary>
        /// The short-term payoff, as a fraction added to an item's score: how much better
        /// the show is tonight for having someone on it who is not supposed to be.
        ///
        /// It scales with integrity, so it is largest the first time and worth nothing once
        /// the split has stopped meaning anything. That asymmetry — a benefit that decays
        /// against a cost that does not — is the trap.
        /// </summary>
        public static double AttractionBonus(Wrestler wrestler, double integrity) =>
            MaxAttraction
            * Math.Clamp(wrestler.EffectiveOverness / 100.0, 0, 1)
            * Math.Clamp(integrity / 100.0, 0, 1);

        // ── What integrity governs ───────────────────────────────────────────

        /// <summary>
        /// Multiplier on overness gained on a brand show. Doc 22 §2.6 and §3.4: the reason
        /// to split is that being top of your own brand makes you a star. When brand
        /// membership means nothing, topping a brand means nothing either.
        /// </summary>
        public static double StarMakingFactor(double integrity) =>
            StarMakingFloor + (1 - StarMakingFloor) * Math.Clamp(integrity / 100.0, 0, 1);

        /// <summary>
        /// Multiplier on overness gained on a specific brand's show, for being on the brand
        /// the audience rates. Doc 22 §4.2, the B-show spiral: a brand that is booked worse
        /// rates worse, and a win on a show nobody rates is worth less, which makes its
        /// roster harder to lift, which makes it rate worse still.
        ///
        /// Returns 1.0 until both brands have run enough shows to be compared.
        /// </summary>
        public static double StandingFactor(Brand brand, BrandSplit split)
        {
            double? own = brand.Form;
            if (own is null || brand.RecentRatings.Count < FormSampleRequired) return 1.0;

            var rivals = split.Brands
                .Where(b => b.Id != brand.Id && b.RecentRatings.Count >= FormSampleRequired)
                .Select(b => b.Form!.Value)
                .ToList();

            if (rivals.Count == 0) return 1.0;

            double best = Math.Max(rivals.Max(), own.Value);
            if (best <= 0) return 1.0;

            // 1.0 for the leading brand, sliding to the floor at 60% of its form.
            double ratio = Math.Clamp(own.Value / best, 0.6, 1.0);
            return SecondaryFloor + (1 - SecondaryFloor) * ((ratio - 0.6) / 0.4);
        }

        /// <summary>
        /// Whether this brand has visibly become the B-show. Advisory: the sim says it out
        /// loud rather than leaving the player to infer it from a slow slide.
        /// </summary>
        public static bool IsSecondary(Brand brand, BrandSplit split)
        {
            if (brand.RecentRatings.Count < FormSampleRequired) return false;

            var rivals = split.Brands
                .Where(b => b.Id != brand.Id && b.RecentRatings.Count >= FormSampleRequired)
                .Select(b => b.Form!.Value)
                .ToList();

            if (rivals.Count == 0) return false;

            return brand.Form!.Value < rivals.Max() * SecondaryThreshold;
        }

        /// <summary>
        /// What a brand show gains for keeping to its own roster. Small, and it goes to zero
        /// with integrity: brand-based stakes only mean something while the brands do.
        /// </summary>
        public static double ExclusivityBonus(double integrity) =>
            MaxExclusivityBonus * Math.Clamp(integrity / 100.0, 0, 1);

        // ── Reporting ────────────────────────────────────────────────────────

        /// <summary>
        /// Where the split sits on doc 22 §4.1's timeline, in the words that document uses.
        /// The bands are the stages of that decay, not arbitrary thresholds.
        /// </summary>
        public static string Phase(double integrity) => integrity switch
        {
            >= 85 => "Enforced",
            >= 65 => "Crossovers for big angles",
            >= 45 => "Crossovers are routine",
            >= 25 => "Top stars work both shows",
            _     => "The split exists in the graphics"
        };

        /// <summary>A plain sentence on what the current integrity means for the promotion.</summary>
        public static string PhaseNote(double integrity) => integrity switch
        {
            >= 85 => "Both brands read as separate companies. This is the state the split is worth having in.",
            >= 65 => "Still holding, but the audience has been shown that the line can move.",
            >= 45 => "Brand membership is a graphic more than a rule. Exclusive booking is worth much less than it was.",
            >= 25 => "The brands share their top of the card. Neither can make a star the other does not already have.",
            _     => "There is no split left to defend. Ending it formally would change almost nothing."
        };

        // ── Depth ────────────────────────────────────────────────────────────

        /// <summary>
        /// Whether the roster can carry a split, per doc 22 §3.6: roughly 25–40 usable
        /// performers per brand out of 60–80 total, including four to six credible
        /// main-eventers each.
        ///
        /// This warns and never blocks. Splitting a roster that is too thin produces two
        /// thin shows instead of one strong one (§4.4) — which is a thing promotions
        /// genuinely do, so the honest simulation is to let the player do it and find out.
        /// </summary>
        public static SplitDepthReport Depth(IReadOnlyList<Wrestler> roster, int brandCount)
        {
            brandCount = Math.Max(1, brandCount);

            int perBrand = roster.Count / brandCount;
            var warnings = new List<string>();

            if (roster.Count < 60)
                warnings.Add(
                    $"A split wants 60–80 usable performers. You have {roster.Count}.");

            if (perBrand < 25)
                warnings.Add(
                    $"That is about {perBrand} per brand, against the 25–40 a brand needs to fill "
                    + "its own card without repeating itself every week.");

            int top = roster.Count(w => w.CardPosition >= CardPosition.UpperCard);
            int wantedTop = 4 * brandCount;
            if (top < wantedTop)
                warnings.Add(
                    $"{top} performer{(top == 1 ? "" : "s")} the audience treats as upper card or above, "
                    + $"against the {wantedTop} a {brandCount}-brand split needs — four to six credible "
                    + "main-eventers each.");

            foreach (var division in new[] { Division.Womens, Division.Mens })
            {
                int size = roster.Count(w => w.Division == division);
                if (size > 0 && size / brandCount < 6)
                    warnings.Add(
                        $"The {DivisionName(division)} division is {size} deep, so each brand gets "
                        + $"about {size / brandCount}. That is not a division, it is a feud.");
            }

            return new SplitDepthReport(roster.Count, brandCount, perBrand, warnings);
        }

        private static string DivisionName(Division division) =>
            division == Division.Womens ? "women's" : "men's";
    }
}
