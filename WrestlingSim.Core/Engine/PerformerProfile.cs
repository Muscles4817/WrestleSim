using WrestlingSim.Enums;
using WrestlingSim.Models;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// The engine's read of what a wrestler is actually good at, resolved once per match.
    ///
    /// Every factor is centred on 1.00 for a competent main-roster regular and spans
    /// roughly 0.5–1.4. Beat handlers multiply their raw output by the factors that are
    /// relevant to what the beat is doing, so two wrestlers with the same overall skill
    /// but different profiles produce genuinely different matches.
    ///
    /// The reference points below define what "1.00" means. They are deliberately set to
    /// a solid-but-unspectacular television wrestler, not to the midpoint of each stat's
    /// legal range — otherwise a roster clustered at the top would all read as identical.
    /// </summary>
    internal sealed class PerformerProfile
    {
        // ── What "average" means. Move these and the whole roster shifts. ─────
        private const double RefConnection   = 0.8125; // pop 80 / appeal 0.85 / charisma 4.0
        private const double RefWorkrate     = 0.70;   // skill 3.5 across the board
        private const double RefRingPsych    = 0.82;   // Psychology 82 / RingIQ 82
        private const double RefSelling      = 0.80;   // Selling 80
        private const double RefResilience   = 0.85;   // Toughness 85
        private const double RefConditioning = 0.82;   // Stamina 82
        private const double RefAthleticism  = 0.75;   // Agility 75 / Speed 75
        private const double RefPower        = 0.75;   // Strength 75 / Size 3

        // ── How hard each stat swings around that reference. ─────────────────
        private const double GainConnection   = 1.60;
        // Workrate swings hardest of the in-ring stats: the gap between a 4.7 grappler and
        // a 2.5 one is the single most visible thing in a match built on mat work.
        private const double GainWorkrate     = 2.00;
        private const double GainRingPsych    = 1.10;
        private const double GainSelling      = 0.90;
        private const double GainResilience   = 0.80;
        private const double GainConditioning = 1.20;
        private const double GainAthleticism  = 1.00;
        private const double GainPower        = 0.90;

        /// <summary>
        /// How much the crowd cares about this person. Popularity, gimmick appeal and
        /// charisma. This is the axis that separates a great worker nobody reacts to
        /// from a limited worker the building loves.
        /// </summary>
        public double Connection { get; }

        /// <summary>In-ring quality for the style being worked. Drives Technical score.</summary>
        public double Workrate { get; }

        /// <summary>Psychology + RingIQ. Does the match hang together as a story.</summary>
        public double RingPsych { get; }

        /// <summary>How well this wrestler makes an opponent's offence look like it hurts.</summary>
        public double Selling { get; }

        /// <summary>Toughness. Whether the crowd believes this person can survive a finisher.</summary>
        public double Resilience { get; }

        /// <summary>Stamina. Resists the late-match fade in long bouts.</summary>
        public double Conditioning { get; }

        /// <summary>
        /// Agility and Speed. Whether fast offence — aerials, comeback flurries, a hot
        /// opening — looks crisp or laboured.
        /// </summary>
        public double Athleticism { get; }

        /// <summary>
        /// Strength and Size. Makes power offence land, and makes a super-finisher on a
        /// big opponent read as a genuine feat.
        /// </summary>
        public double Power { get; }

        /// <summary>Raw 0–1 crowd disposition, kept for the beats that reason about it directly.</summary>
        public double Disposition { get; }

        /// <summary>What rust is doing to this wrestler's craft. Kept for <see cref="WorkrateFor"/>.</summary>
        private readonly double _craft;

        /// <summary>
        /// The gas tank before fatigue is taken off it.
        ///
        /// <see cref="RingCondition"/> has to price a match and a rest day against what a
        /// wrestler's stamina actually is, not against what today's fatigue has left of it.
        /// Reading the adjusted figure made fatigue compound on itself twice over — a tired
        /// wrestler paid more for the same match and recovered slower from it, neither of
        /// which anybody designed.
        /// </summary>
        public double BaseConditioning { get; }

        /// <summary>
        /// 0–1: how much the room is *on this person's side*, as opposed to how much it
        /// cares about them at all (<see cref="Connection"/>) or how popular they are
        /// (<see cref="Disposition"/>).
        ///
        /// This exists because the reaction vector shipped without it and got the taxonomy
        /// backwards. Reading "liked" off popularity alone means the engine's Heat is
        /// really *unpopular* — so a huge heel recorded Pop and an unloved babyface
        /// recorded Heat. Doc 16 §2 defines heat as "the audience hates this person and
        /// wants them beaten", which is a statement about alignment, and it flags the two
        /// readings that break a match — cheers for a heel, boos for a babyface — as the
        /// ones that matter most. Neither is expressible from popularity alone.
        ///
        /// So: the gimmick's alignment sets the intent, and disposition can override it.
        /// A heel the crowd adores crosses over into cheers; a babyface it has turned on
        /// crosses the other way. That crossover is the point.
        /// </summary>
        public double Favour { get; }

        private readonly Wrestler _wrestler;

        public PerformerProfile(Wrestler w)
        {
            _wrestler = w;

            // EffectiveOverness, not raw Overness: a performer on a hot streak reads to
            // the crowd as bigger than their standing, and a cold one reads smaller.
            double popNorm    = Math.Clamp(w.EffectiveOverness / 100.0, 0, 1);
            double appealNorm = AverageAppeal(w) ?? popNorm;
            double chaNorm    = Math.Clamp(w.Charisma / 5.0, 0, 1);

            Disposition = (popNorm + appealNorm) / 2.0;

            double intent = w.Gimmick?.NaturalAlignment switch
            {
                Alignment.Face => 0.85,
                Alignment.Heel => 0.15,
                _              => 0.50   // a tweener is whatever the room decides they are
            };

            // Logistic rather than a clamped line, because the clamped line had a plateau
            // and people landed on it. Review measured **15 of 35 shipped babyfaces at
            // exactly Favour = 1.000**, which does not mean "very much cheered" — it means
            // `heated = engaged × (1 − favour) = 0`, so those fifteen could never record a
            // single unit of heat under any booking. Seven heels sat at exactly 0.000 and
            // could never record pop. That is the same degenerate-clamp shape review caught
            // in round 1 on the investment multiplier, in a different place.
            //
            // The curve saturates asymptotically instead, so nobody is ever categorically
            // incapable of the other reading. 4.0 sets how firmly the gimmick's alignment
            // states its intent; 5.0 sets how far the crowd can overrule it.
            double lean = (intent - 0.5) * 4.0 + (Disposition - 0.55) * 5.0;
            Favour = 1.0 / (1.0 + Math.Exp(-lean));

            // Charisma is weighted heaviest because it is the sharpest discriminator on a
            // roster where everyone is reasonably popular.
            double connectionRaw = popNorm * 0.35 + appealNorm * 0.25 + chaNorm * 0.40;

            double styleNorm = Math.Clamp(
                (w.RingSkills.GetStyleProficiency(w.Style) * 0.6 + w.RingSkills.GetOverallSkill() * 0.4) / 5.0, 0, 1);

            double psychNorm = Math.Clamp((w.Mental.Psychology * 0.6 + w.Mental.RingIQ * 0.4) / 100.0, 0, 1);

            Connection   = Centre(connectionRaw, RefConnection,   GainConnection,   0.30, 1.45);
            Workrate     = Centre(styleNorm,     RefWorkrate,     GainWorkrate,     0.30, 1.55);
            RingPsych    = Centre(psychNorm,     RefRingPsych,    GainRingPsych,    0.50, 1.30);
            Selling      = Centre(w.Mental.Selling   / 100.0, RefSelling,      GainSelling,      0.55, 1.25);
            Resilience   = Centre(w.Mental.Toughness / 100.0, RefResilience,   GainResilience,   0.60, 1.20);
            Conditioning = Centre(w.Physical.Stamina / 100.0, RefConditioning, GainConditioning, 0.55, 1.25);
            BaseConditioning = Conditioning;

            // ── The two meters ───────────────────────────────────────────────
            //
            // Applied here, at the one point every beat handler reads a wrestler through,
            // so nothing downstream needs to know these exist.
            //
            // Fatigue goes on conditioning because conditioning is already wired to the
            // three places being tired shows up — the late-match fade, the penalty for
            // asking a long match of somebody who cannot go long, and the extra wear of
            // being outnumbered. Rust goes on the craft stats, because what goes when you
            // have been away is timing rather than gas.
            //
            // Neither touches Connection, deliberately. Rusty and forgotten are different
            // things and the game already has one of them.
            Conditioning *= RingCondition.ConditioningFactor(w.Fatigue);

            _craft = RingCondition.CraftFactor(w.Sharpness);
            Workrate  *= _craft;
            RingPsych *= _craft;

            double athleticNorm = Math.Clamp(
                (w.Physical.Agility * 0.6 + w.Physical.Speed * 0.4) / 100.0, 0, 1);
            Athleticism = Centre(athleticNorm, RefAthleticism, GainAthleticism, 0.55, 1.30);

            // A smaller bite than conditioning takes: tired legs are slower, but a wrestler
            // does not stop being athletic because they worked on Tuesday.
            Athleticism *= 1.0 - (1.0 - RingCondition.ConditioningFactor(w.Fatigue)) * 0.5;

            // Size is a 1–5 scale; normalise it onto the same 0–1 footing as Strength.
            double powerNorm = Math.Clamp(
                (w.Physical.Strength * 0.7 + Math.Clamp(w.Physical.Size, 0, 5) * 20.0 * 0.3) / 100.0, 0, 1);
            Power = Centre(powerNorm, RefPower, GainPower, 0.60, 1.25);
        }

        /// <summary>
        /// Workrate for a specific style, so a beat's StyleHint reaches the right stat.
        /// Technical Dissection asks for Technical skill even from a powerhouse.
        /// </summary>
        public double WorkrateFor(WrestlingStyle style)
        {
            double norm = Math.Clamp(
                (_wrestler.RingSkills.GetStyleProficiency(style) * 0.6
                 + _wrestler.RingSkills.GetOverallSkill() * 0.4) / 5.0, 0, 1);
            // Rust applies here too. Without it a beat carrying a StyleHint read the raw
            // skills and quietly routed around the meter — the one path where being ring
            // rusty would have cost nothing.
            return Centre(norm, RefWorkrate, GainWorkrate, 0.30, 1.55) * _craft;
        }

        /// <summary>
        /// Blends a factor toward 1.0. Used so a beat is influenced by a stat without any
        /// single weak stat wiping the beat out — weight 0.5 means the factor is half-applied.
        /// </summary>
        public static double Blend(double factor, double weight) => 1.0 - weight + factor * weight;

        private static double Centre(double norm, double reference, double gain, double lo, double hi) =>
            Math.Clamp(1.0 + (norm - reference) * gain, lo, hi);

        private static double? AverageAppeal(Wrestler w)
        {
            var ratings = w.Gimmick?.AppealRatings;
            if (ratings == null || ratings.Count == 0) return null;
            return Math.Clamp(ratings.Average(a => a.AppealScore), 0, 1);
        }
    }
}
