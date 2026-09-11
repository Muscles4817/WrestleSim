using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Person;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Who gets hurt, how badly, and why.
    ///
    /// Doc 15 §3.1 gives a table of risk multipliers and calls them "directly usable", so
    /// this is that table rather than a curve somebody liked the shape of. What it does not
    /// contain is anything about *rating* — an injury is not a punishment for booking a bad
    /// match, it is the physical cost of having had one at all.
    ///
    /// **Fatigue is the link this closes.** Until now a booker could work their main
    /// eventer three times a night, every week, for a year, and the only cost was a rating.
    /// Doc 15 puts match volume first in its table and calls the relationship "roughly
    /// linear: 200 dates is ~2× the risk of 100" — and the game already carries volume as
    /// a meter. Being cooked is now dangerous rather than merely expensive.
    ///
    /// **What is not here, and why.** Doc 15 §3.1 also names age over 35 (1.5–2× and
    /// rising) and ring quality. There is no age on a wrestler and no venue condition, so
    /// neither is modelled — inventing a proxy for age out of stats that only correlate
    /// with it would produce a number that looks authoritative and is not. Opponent safety
    /// is proxied from ring psychology, which is defensible: a wrestler who does not know
    /// where he is putting people is the one who hurts them.
    /// </summary>
    public static class InjuryRisk
    {
        /// <summary>
        /// Chance per beat that somebody gets hurt, before any multiplier.
        ///
        /// Anchored to doc 15 §3's headline rate — 35–55% of full-time performers miss time
        /// in a given year — against doc 06's 100–150 dates a year at roughly ten beats a
        /// match. Measured rather than assumed; see the tests, which run a synthetic season
        /// and check the annual figure lands in the reference's band.
        ///
        /// The first value was three and a half times this and put 85% of a roster on the
        /// shelf inside a year, over half of them for three months or more. The multipliers
        /// compound harder than they look: a mid-card wrestler on an ordinary night is
        /// already carrying four of them at once.
        /// </summary>
        public const double BaseBeatRisk = 0.00013;

        // ── The multipliers (doc 15 §3.1) ────────────────────────────────────

        /// <summary>
        /// Style. Doc 15: high-flying, hardcore and high-impact work runs 1.5–2.5×.
        ///
        /// Note this does **not** rank the same way as either fatigue load. A powerhouse is
        /// punished by the clock and a striker by the pace, but what gets a high-flyer hurt
        /// is the landing — three different questions, three different orderings, which is
        /// the reason none of them is a single "how hard is this style" number.
        /// </summary>
        public static double StyleRisk(WrestlingStyle style) => style switch
        {
            WrestlingStyle.HighFlyer  => 2.30,
            WrestlingStyle.Powerhouse => 1.45,
            WrestlingStyle.Brawler    => 1.25,
            WrestlingStyle.Striker    => 1.15,
            WrestlingStyle.Grappler   => 0.90,
            WrestlingStyle.Technical  => 0.80,
            _                         => 1.00
        };

        /// <summary>
        /// Intensity. Doc 18 already grades a beat on how hard it is being worked, and the
        /// hardest beats are where people get hurt.
        /// </summary>
        public static double IntensityRisk(BeatIntensity intensity) => intensity switch
        {
            BeatIntensity.Low     => 0.45,
            BeatIntensity.Medium  => 1.00,
            BeatIntensity.High    => 1.75,
            BeatIntensity.Extreme => 3.10,
            _                     => 1.00
        };

        /// <summary>
        /// Fatigue, and the one doc 15 is most specific about: "poor conditioning sharply
        /// increases **late-match** injury risk".
        ///
        /// So this takes both — how tired the wrestler arrived and how deep into the match
        /// it is — and the two compound. A fresh wrestler in the tenth minute is fine and a
        /// cooked one is not, which is the whole reason the meter was worth building.
        /// </summary>
        public static double FatigueRisk(double fatigue, int beatIndex, double conditioning)
        {
            double carried = 1.0 + Math.Clamp(fatigue, 0, 100) / 100.0 * 1.30;

            // Nothing before the sixth beat is deep enough for this to bite — the same
            // threshold FadeFactor uses, so the engine has one idea of "late" rather than two.
            double late = Math.Max(0, beatIndex - 5) * 0.09
                          * (2.0 - Math.Clamp(conditioning, 0.55, 1.35));

            return carried * (1.0 + Math.Max(0, late));
        }

        /// <summary>
        /// Size. Doc 15: very large performers carry higher risk — "more force, more joint
        /// load, worse landing mechanics". Size is a 1–5 scale.
        /// </summary>
        public static double SizeRisk(int size) =>
            1.0 + Math.Clamp(size - 3, 0, 2) * 0.22;

        /// <summary>
        /// The opponent. Doc 15 lists working with an unsafe opponent as "large and real",
        /// and there is no safety stat — so it is read off ring psychology, on the grounds
        /// that a wrestler who does not know where he is putting people is the one who
        /// hurts them.
        ///
        /// A proxy, and flagged as one: it will read a green athletic worker as more
        /// dangerous than he might be, and a cautious limited one as safer.
        /// </summary>
        public static double OpponentRisk(int opponentPsychology, int opponentRingIQ) =>
            Math.Clamp(1.9 - (opponentPsychology * 0.6 + opponentRingIQ * 0.4) / 100.0 * 1.1,
                       0.75, 1.65);

        /// <summary>
        /// History. Doc 15 calls this "the strongest single predictor" and says a prior back
        /// or neck injury roughly doubles recurrence risk — and the sim implications name
        /// injury history as "a permanent, compounding attribute … the most important
        /// detail".
        ///
        /// So it compounds and never goes away. Each previous injury to the same place is
        /// worth most; everything else in the file is worth something, because a body that
        /// has been broken before breaks again.
        /// </summary>
        public static double HistoryRisk(IReadOnlyCollection<Injury> history, BodyPart part)
        {
            if (history.Count == 0) return 1.0;

            int samePlace = history.Count(i => i.Part == part);
            int elsewhere = history.Count - samePlace;

            // The reference is specific about which: "a prior **back or neck** injury roughly
            // doubles recurrence risk". So those two double on the first recurrence and the
            // rest are milder — a wrestler who has hurt an ankle once is not carrying the
            // same thing as a wrestler with a fused neck.
            double weight = part is BodyPart.Back or BodyPart.Neck ? 2.20 : 1.30;

            // Saturating rather than unbounded: a wrestler with nine prior knee injuries is
            // not nine times likelier than one with one — he is somebody whose knee is gone,
            // and the model says that once.
            double same  = 1.0 + weight * (1.0 - Math.Pow(0.55, samePlace));
            double other = 1.0 + 0.35  * (1.0 - Math.Pow(0.70, elsewhere));

            return same * other;
        }

        /// <summary>
        /// The ring itself. Doc 15: "a hard or badly-maintained ring is a genuine injury
        /// multiplier; indie rings are frequently much worse." Read off the promotion's
        /// tier, which is the only thing in the model that knows what a venue is like.
        /// </summary>
        public static double RingRisk(PromotionTier tier) => tier switch
        {
            PromotionTier.Local        => 1.35,
            PromotionTier.Independent  => 1.25,
            PromotionTier.SuperIndie   => 1.12,
            PromotionTier.Established  => 1.00,
            PromotionTier.National     => 0.95,
            PromotionTier.Global       => 0.90,
            _                          => 1.00
        };

        // ── Putting it together ──────────────────────────────────────────────

        /// <summary>
        /// The chance this wrestler is hurt on this beat.
        ///
        /// Multiplicative because the reference's table is multiplicative: a tired
        /// high-flyer with a bad back working an extreme spot in the tenth minute on an
        /// indie ring is not the sum of those things, he is the product of them, and that
        /// is the wrestler doc 15 is describing when it says the rate is 35–55% a year.
        /// </summary>
        public static double PerBeat(
            Wrestler wrestler, BeatIntensity intensity, int beatIndex,
            double conditioning, Wrestler? opponent, PromotionTier tier,
            BodyPart likeliest)
        {
            return BaseBeatRisk
                 * StyleRisk(wrestler.Style)
                 * IntensityRisk(intensity)
                 * FatigueRisk(wrestler.Fatigue, beatIndex, conditioning)
                 * SizeRisk(wrestler.Physical?.Size ?? 3)
                 * OpponentRisk(opponent?.Mental?.Psychology ?? 75, opponent?.Mental?.RingIQ ?? 75)
                 * HistoryRisk(wrestler.InjuryHistory, likeliest)
                 * RingRisk(tier);
        }

        // ── What gets hurt, and for how long ─────────────────────────────────

        private static readonly BodyPart[] Parts = Enum.GetValues<BodyPart>();

        /// <summary>
        /// The weight table for one kind of body, built once.
        ///
        /// This is called for every wrestler on every beat of every match, which the first
        /// version did with <c>Enum.GetValues</c> and three LINQ passes — reflection and four
        /// allocations per wrestler per beat, and measurably slower across a card. The table
        /// only depends on style and size, and there are thirty of those.
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<
            (WrestlingStyle Style, int Size), (double[] Weights, double Total)> BodyOdds = new();

        private static (double[] Weights, double Total) OddsFor(WrestlingStyle style, int size) =>
            BodyOdds.GetOrAdd((style, size), key =>
            {
                var weights = new double[Parts.Length];
                double total = 0;
                for (int i = 0; i < Parts.Length; i++)
                {
                    weights[i] = Likelihood(Parts[i], key.Style, key.Size);
                    total += weights[i];
                }
                return (weights, total);
            });

        /// <summary>
        /// Which injury this wrestler is most likely to pick up, weighted per doc 15 §2.1.
        ///
        /// Here rather than in <c>MatchEngine</c> because a house show loop hurts people the
        /// same way a televised match does — it is the same body in the same ring, and the
        /// only difference is that nobody filmed it.
        /// </summary>
        public static BodyPart PickPart(Models.Wrestler wrestler, Random rand)
        {
            var (weights, total) = OddsFor(wrestler.Style, wrestler.Physical?.Size ?? 3);

            double roll = rand.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll <= 0) return Parts[i];
            }
            return Parts[^1];
        }


        /// <summary>
        /// How often each injury happens, before style and size tilt it. Doc 15 §2.1's
        /// frequency column, as weights.
        /// </summary>
        private static double BaseFrequency(BodyPart part) => part switch
        {
            BodyPart.Concussion => 26,   // "very common"
            BodyPart.Back       => 24,   // "extremely common"
            BodyPart.Ankle      => 16,   // "common", and the quickest one back
            BodyPart.Bone       => 12,   // "occasional"
            BodyPart.Shoulder   => 9,    // "very common", but four months when it happens
            BodyPart.Knee       => 5,
            BodyPart.Elbow      => 4,
            BodyPart.Pectoral   => 3,
            BodyPart.Neck       => 1.5,  // rare, and the one that defines a career
            BodyPart.Achilles   => 1,    // "occasional, devastating"
            _                   => 1
        };

        /// <summary>
        /// How a style and a body tilt which injury you get. The landing hurts a high-flyer's
        /// knees and head; the weight hurts a powerhouse's back and chest — doc 15 names the
        /// pectoral tear as "common in powerlifting-built performers" specifically.
        /// </summary>
        public static double Likelihood(BodyPart part, WrestlingStyle style, int size)
        {
            double weight = BaseFrequency(part);

            weight *= (style, part) switch
            {
                (WrestlingStyle.HighFlyer, BodyPart.Knee)       => 2.0,
                (WrestlingStyle.HighFlyer, BodyPart.Ankle)      => 2.0,
                (WrestlingStyle.HighFlyer, BodyPart.Concussion) => 1.5,
                (WrestlingStyle.HighFlyer, BodyPart.Neck)       => 1.4,
                (WrestlingStyle.Powerhouse, BodyPart.Back)      => 1.8,
                (WrestlingStyle.Powerhouse, BodyPart.Pectoral)  => 2.2,
                (WrestlingStyle.Powerhouse, BodyPart.Knee)      => 1.3,
                (WrestlingStyle.Brawler, BodyPart.Concussion)   => 1.6,
                (WrestlingStyle.Brawler, BodyPart.Bone)         => 1.5,
                (WrestlingStyle.Striker, BodyPart.Concussion)   => 1.7,
                (WrestlingStyle.Striker, BodyPart.Bone)         => 1.4,
                (WrestlingStyle.Grappler, BodyPart.Shoulder)    => 1.4,
                (WrestlingStyle.Grappler, BodyPart.Elbow)       => 1.5,
                (WrestlingStyle.Technical, BodyPart.Shoulder)   => 1.3,
                _                                               => 1.0
            };

            // Big men tear pectorals and wreck backs. Doc 15 §3.1 on size, §2.1 on the pec.
            if (size >= 4 && part is BodyPart.Pectoral or BodyPart.Back)
                weight *= 1.0 + (size - 3) * 0.45;

            return weight;
        }

        /// <summary>
        /// Weeks out, as a band. Doc 15 §2.1's "typical time out" column, converted from
        /// months and truncated at the top: the reference says a neck can be eighteen months
        /// and often career-ending, and this game cannot end a career, so the long injuries
        /// stop where the model can still tell the truth about what happens next.
        ///
        /// The **frequency weights above carry the shape of the year**, not these bands.
        /// Doc 15 §3 says 35–55% of performers miss time but only 10–18% lose three months
        /// or more, which means most of what happens has to be short — the concussion, the
        /// tweaked back, the rolled ankle. Weighting the shoulder and the knee by how often
        /// the reference calls them "common" rather than by how much time they actually
        /// account for put nearly half of all injuries past three months.
        /// </summary>
        public static (int Low, int High) WeeksOut(BodyPart part) => part switch
        {
            BodyPart.Ankle      => (2, 8),
            BodyPart.Bone       => (3, 12),
            BodyPart.Concussion => (1, 14),    // "days to months"
            BodyPart.Back       => (2, 14),    // "weeks to career-ending"; the weeks end
            BodyPart.Elbow      => (16, 26),
            BodyPart.Pectoral   => (16, 26),
            BodyPart.Shoulder   => (16, 39),
            BodyPart.Knee       => (24, 52),   // an ACL is most of a year
            BodyPart.Achilles   => (39, 52),
            BodyPart.Neck       => (26, 52),
            _                   => (2, 8)
        };
    }
}
