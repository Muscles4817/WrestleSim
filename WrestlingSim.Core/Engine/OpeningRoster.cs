using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Who a new promotion opens with. A source rather than a rule, because the long-term
    /// answer is data: a custom database names who each promotion has signed and on what terms
    /// (WWE opens with WWE's people). <see cref="TierOpeningRoster"/> is the default for the
    /// shipped roster, which belongs to nobody.
    /// </summary>
    public interface IOpeningRoster
    {
        IReadOnlyList<Contract> Open(Promotion promotion, IReadOnlyList<Wrestler> world, DateOnly on);
    }

    /// <summary>
    /// A roster sized and budgeted for the tier, balanced across the divisions.
    ///
    /// Quality is not chosen — it falls out of the budget, because prices are the same for
    /// every buyer. A Local promotion's budget buys the least over end of a major-league
    /// roster; a Global one's buys everybody.
    ///
    /// Below television nobody is signed (doc 11 §2.4): the opening roster is the promotion's
    /// regulars, booked and paid by the night. At television tiers it is guaranteed deals with
    /// staggered terms, so the renewals arrive a few at a time rather than all in one year.
    /// </summary>
    public sealed class TierOpeningRoster : IOpeningRoster
    {
        /// <summary>
        /// How many, and what for. For a television tier the budget is a year's guarantees —
        /// doc 09 §2 puts talent at 25–35% of revenue. For a tier without television it is
        /// the most a regular is paid for one night, which is the indie ceiling of doc 29 §4.2
        /// for that tier.
        /// </summary>
        public static (int Size, decimal Budget) For(PromotionTier tier) => tier switch
        {
            PromotionTier.Local       => (12,        250m),
            PromotionTier.Independent => (16,        600m),
            PromotionTier.SuperIndie  => (22,      2_000m),
            PromotionTier.Established => (40,  7_000_000m),
            PromotionTier.National    => (65, 30_000_000m),
            PromotionTier.Global      => (120, 150_000_000m),
            _                         => (16,        600m)
        };

        public IReadOnlyList<Contract> Open(Promotion promotion, IReadOnlyList<Wrestler> world, DateOnly on)
        {
            var (size, budget) = For(promotion.Tier);
            bool guaranteed = promotion.HasTelevision;

            // Best first within each division, then taken alternately, so neither division
            // eats the budget. Ties broken by id so the pick is the same every time.
            var queues = world
                .GroupBy(w => w.Division)
                .OrderBy(g => g.Key)
                .Select(g => new Queue<Wrestler>(g
                    .OrderByDescending(w => w.EffectiveOverness)
                    .ThenBy(w => w.Id, StringComparer.Ordinal)))
                .ToList();

            var picked = new List<Contract>();
            decimal spent = 0;
            bool progress = true;

            while (picked.Count < size && progress)
            {
                progress = false;
                foreach (var queue in queues)
                {
                    if (picked.Count >= size) break;

                    // The best in this division the budget still covers. Anybody too dear now is
                    // set aside rather than dropped — the share grows as the roster fills.
                    var skipped = new List<Wrestler>();
                    while (queue.Count > 0)
                    {
                        var w = queue.Dequeue();
                        int years = 1 + picked.Count % 4;

                        if (guaranteed)
                        {
                            var annual = TalentMarket.GuaranteeAsk(w, years);

                            // Spread the budget over the roster rather than spending it on the
                            // first names that fit: each pick may take up to three times its
                            // fair share of what is left. That buys a few stars at the top and
                            // still fills the card underneath them.
                            int slotsLeft = size - picked.Count;
                            decimal share = (budget - spent) / slotsLeft * 3m;
                            if (spent + annual > budget || annual > share) { skipped.Add(w); continue; }
                            spent += annual;
                            picked.Add(new Contract
                            {
                                WrestlerId      = w.Id,
                                Kind            = ContractKind.Guaranteed,
                                AnnualGuarantee = annual,
                                Start           = on,
                                End             = on.AddYears(years)
                            });
                        }
                        else
                        {
                            if (TalentMarket.PerDateAsk(w) > budget) { skipped.Add(w); continue; }
                            picked.Add(new Contract { WrestlerId = w.Id, Kind = ContractKind.Regular, Start = on });
                        }

                        progress = true;
                        break;
                    }

                    // Put the passed-over back, best first, for the next round.
                    foreach (var w in skipped.AsEnumerable().Reverse()) Requeue(queue, w);
                }
            }

            return picked;
        }

        /// <summary>Puts somebody back at the front of their division's queue.</summary>
        private static void Requeue(Queue<Wrestler> queue, Wrestler w)
        {
            var rest = queue.ToList();
            queue.Clear();
            queue.Enqueue(w);
            foreach (var r in rest) queue.Enqueue(r);
        }
    }
}
