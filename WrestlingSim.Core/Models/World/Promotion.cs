using WrestlingSim.Enums;

namespace WrestlingSim.Models.World
{
    /// <summary>
    /// The company the player runs. Tier is the load-bearing field: it derives the
    /// scheduling and presentation constraints the rest of a career operates inside.
    ///
    /// See docs/wrestling-reference/01-industry-map.md for the tier definitions and
    /// docs/wrestling-reference/06-schedule-and-cadence.md for the cadence figures.
    /// </summary>
    public class Promotion
    {
        public string Name { get; set; } = "New Promotion";
        public PromotionTier Tier { get; set; } = PromotionTier.Independent;

        // ── Tier-derived constraints ─────────────────────────────────────────

        /// <summary>
        /// Whether this tier carries weekly television. Below Established a promotion has
        /// no guaranteed income and every show is an independent financial event — the
        /// dividing line described in doc 01 §3.
        /// </summary>
        public bool HasTelevision => Tier >= PromotionTier.Established;

        /// <summary>Nights between television episodes. Zero when the tier has no TV.</summary>
        public int TelevisionIntervalDays => HasTelevision ? 7 : 0;

        /// <summary>
        /// Roughly how often this tier runs anything at all, in days. Used to seed a new
        /// save's calendar and to warn when a player is over-running their markets.
        /// </summary>
        public int TypicalShowIntervalDays => Tier switch
        {
            PromotionTier.Local       => 45,
            PromotionTier.Independent => 30,
            PromotionTier.SuperIndie  => 21,
            PromotionTier.Established => 7,
            PromotionTier.National    => 4,
            PromotionTier.Global      => 3,
            _                         => 30
        };

        /// <summary>Default runtime budget in minutes for a show of the given type.</summary>
        public int DefaultRuntimeFor(ShowType type) => type switch
        {
            ShowType.Television  => Tier >= PromotionTier.National ? 120 : 90,
            ShowType.PremiumEvent => Tier switch
            {
                PromotionTier.Global      => 240,
                PromotionTier.National    => 210,
                PromotionTier.Established => 180,
                _                         => 150
            },
            ShowType.HouseShow => 135,
            _                  => 120
        };

        /// <summary>
        /// What the audience expects on a card of this type at this tier. Advisory — the
        /// runtime budget is the hard constraint — but it is what the UI guides toward.
        /// </summary>
        public int TypicalCardSizeFor(ShowType type) => type switch
        {
            ShowType.Television   => Tier >= PromotionTier.National ? 7 : 5,
            ShowType.PremiumEvent => Tier >= PromotionTier.Established ? 8 : 7,
            ShowType.HouseShow    => 8,
            _                     => 6
        };

        /// <summary>Building capacity this tier can plausibly fill for a given show type.</summary>
        public int TypicalAttendanceFor(ShowType type)
        {
            int baseline = Tier switch
            {
                PromotionTier.Local       => 150,
                PromotionTier.Independent => 450,
                PromotionTier.SuperIndie  => 1_400,
                PromotionTier.Established => 4_000,
                PromotionTier.National    => 8_000,
                PromotionTier.Global      => 13_000,
                _                         => 500
            };

            return type switch
            {
                // A premium event draws better than a weekly show; a house show worse.
                ShowType.PremiumEvent => (int)(baseline * 1.6),
                ShowType.HouseShow    => (int)(baseline * 0.65),
                _                     => baseline
            };
        }

        /// <summary>Show types this tier can actually run.</summary>
        public IEnumerable<ShowType> AvailableShowTypes
        {
            get
            {
                if (HasTelevision) yield return ShowType.Television;
                yield return ShowType.PremiumEvent;
                yield return ShowType.HouseShow;
            }
        }

        public string TierLabel => Tier switch
        {
            PromotionTier.Local       => "Local",
            PromotionTier.Independent => "Regional Independent",
            PromotionTier.SuperIndie  => "Super Indie",
            PromotionTier.Established => "Established",
            PromotionTier.National    => "National",
            PromotionTier.Global      => "Global Major",
            _                         => Tier.ToString()
        };

        public string TierDescription => Tier switch
        {
            PromotionTier.Local       => "A room, a ring, and whoever turns up. Every show is its own gamble.",
            PromotionTier.Independent => "A few towns on a rotation. Per-date talent, cash on the night.",
            PromotionTier.SuperIndie  => "A reputation beyond your region and a streaming audience that travels.",
            PromotionTier.Established => "Weekly television and guaranteed income. You can plan now.",
            PromotionTier.National    => "A genuine national challenger. Deep roster, more TV than you can fill.",
            PromotionTier.Global      => "The brand outdraws the roster. You cannot fail quickly — only slowly.",
            _                         => string.Empty
        };
    }
}
