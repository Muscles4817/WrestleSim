using WrestlingSim.Enums;
using WrestlingSim.Models;

namespace WrestlingSim.Engine
{
    /// <summary>What a result did to one person's standing.</summary>
    public sealed record StatusChange(
        Wrestler Wrestler,
        double OvernessDelta,
        double MomentumDelta,
        string Reason)
    {
        public bool IsMeaningful => Math.Abs(OvernessDelta) >= 0.05 || Math.Abs(MomentumDelta) >= 0.5;
    }

    /// <summary>
    /// Both sides of a match result.
    ///
    /// <see cref="Winner"/> and <see cref="Loser"/> are the two people the fall was
    /// actually between — the pinner and the man who was pinned. In a tag match
    /// <see cref="Partners"/> carries what the result did to everybody else on the two
    /// sides, which is deliberately not the same thing.
    /// </summary>
    public sealed record MatchStatusOutcome(
        StatusChange Winner, StatusChange Loser, IReadOnlyList<StatusChange>? Partners = null)
    {
        public IEnumerable<StatusChange> All =>
            new[] { Winner, Loser }.Concat(Partners ?? []);

        /// <summary>
        /// The winner's overness swing before it was dampened against his own ceiling.
        /// Partners take their share of this rather than of the dampened figure, so a
        /// star's compression is not charged to the man standing next to him.
        /// </summary>
        public double RawWinnerOverness { get; init; }

        /// <summary>The loser's swing before dampening, as a positive magnitude.</summary>
        public double RawLoserOverness { get; init; }
    }

    /// <summary>How decisively a match ended, from the audience's point of view.</summary>
    public enum FinishWeight
    {
        /// <summary>Pinned or submitted clean. The full statement.</summary>
        Decisive,

        /// <summary>A roll-up or a fluke. They were not beaten, exactly.</summary>
        Fluke,

        /// <summary>Interference, DQ, count-out. The audience forgives a loss it understands.</summary>
        Protected
    }

    /// <summary>
    /// The status economy: what a win is worth, and to whom.
    ///
    /// Heat is not created from nothing — it is largely transferred
    /// (docs/wrestling-reference/17-heat-and-getting-over.md §6). The rules this encodes:
    ///
    ///   • You can only take status from someone who has it.
    ///   • Beating someone above you transfers a lot; they can afford it.
    ///   • Beating someone below you gains you almost nothing and costs them a great deal.
    ///     That is a *net destruction of value*, and it is the most common booking waste
    ///     in the business.
    ///   • Two people the audience does not care about generate nothing, however good the
    ///     match is.
    ///   • The audience forgives a loss it understands, so a protected finish costs less.
    ///
    /// Every method here is pure. The caller decides whether to apply the result.
    /// </summary>
    public static class HeatEconomy
    {
        // ── Tuning ───────────────────────────────────────────────────────────

        /// <summary>Most overness a single match can move, before any scaling.</summary>
        private const double OvernessScale = 5.0;

        /// <summary>Momentum swings far harder than overness — it is the flow, not the stock.</summary>
        private const double MomentumScale = 40.0;

        /// <summary>Momentum kept per day. ~0.967 is a three-week half-life.</summary>
        public const double MomentumDailyRetention = 0.967;

        /// <summary>Days off screen before overness itself starts to slip.</summary>
        public const int AbsenceGraceDays = 21;

        /// <summary>Overness lost per day once past the grace period.</summary>
        private const double AbsenceDailyOverness = 0.06;

        // ── Match results ────────────────────────────────────────────────────

        /// <summary>
        /// What a win does to both people. Deltas only — nothing is applied.
        ///
        /// <paramref name="familiarity"/> is how much the crowd still wanted to see this
        /// pairing (docs/wrestling-reference/20-storylines-and-feuds.md §9.1). It damps the
        /// whole transfer, both ways, because a result only moves standing to the extent
        /// the audience registers it — and the fourth time they watch these two, they have
        /// stopped reading the outcome as news. Beating the same man again is not a
        /// statement; losing to him again is not a fall. Nobody gains, and nobody much
        /// loses either, which is precisely why a stale series is dead weight on a card.
        /// </summary>
        /// <summary>
        /// Share of the winner's gain that goes to a partner who did not score the fall.
        /// Half, because being on the winning team is genuinely worth something and
        /// standing on the apron while somebody else wins the match is genuinely worth
        /// less than winning it.
        /// </summary>
        public const double PartnerWinShare = 0.50;

        /// <summary>
        /// Share of the loser's hit that a partner takes when somebody else ate the fall.
        ///
        /// This asymmetry is the point of the whole method. At 0.35 a booker can protect
        /// someone by having their partner take the pin, which is the single most common
        /// use a tag match is put to
        /// (docs/wrestling-reference/12-pushes-and-positioning.md §6.1) — and it still
        /// costs something, so it is a lever rather than a free pass.
        /// </summary>
        public const double PartnerLossShare = 0.35;

        /// <summary>
        /// What a side is worth to the audience.
        ///
        /// Top-weighted rather than averaged, for the same reason the match engine reads a
        /// side that way: beating a team reads as beating the team, and a team is mostly
        /// its best man. A flat mean would make adding a jobber to a main-eventer's side a
        /// way of quietly halving what beating them is worth.
        /// </summary>
        public static double SideStanding(IReadOnlyList<Wrestler> side, double chemistry = 0.0)
        {
            if (side.Count == 0) return 0;
            if (side.Count == 1) return side[0].EffectiveOverness;

            double best = side.Max(w => w.EffectiveOverness);
            double mean = side.Average(w => w.EffectiveOverness);
            return best + (mean - best) * DragFor(chemistry);
        }

        /// <summary>
        /// Mirrors <c>MatchEngine.Ctx</c>, including its chemistry lift — an established
        /// team reads as one act to the status economy for exactly the reason it does to
        /// the crowd. These were allowed to drift apart once already: this method shipped
        /// with a flat 0.5 and a comment claiming it mirrored a term that by then had a
        /// chemistry factor in it, so the engine read a drilled star-and-rookie side at
        /// 84.75 and the heat economy read the same side at 72.50.
        /// </summary>
        public static double DragFor(double chemistry) =>
            SideDragWeight * (1.0 - SideChemistryLift * Math.Clamp(chemistry, 0, 1));

        public const double SideDragWeight   = 0.5;
        public const double SideChemistryLift = 0.7;

        /// <summary>
        /// What a result did to everybody in a tag match.
        ///
        /// The fall itself is priced exactly as a singles match between the two men in it,
        /// against the two *sides'* standing rather than their own — so beating a team of
        /// mid-carders is not the same statement as beating one main-eventer. Then the
        /// pinner and the man who was pinned take it in full, and their partners take a
        /// share.
        /// </summary>
        public static MatchStatusOutcome ForSides(
            IReadOnlyList<Wrestler> winningSide, Wrestler pinner,
            IReadOnlyList<Wrestler> losingSide, Wrestler pinned,
            double starRating, FinishWeight finish, double familiarity = 1.0,
            double winningChemistry = 0.0, double losingChemistry = 0.0)
        {
            // ForMatch is protected by MatchPlan.Validate upstream; this is public and has
            // no such guard, and the degenerate calls are not harmlessly wrong — an empty
            // winning side reads as a maximum upset and pays about five times a normal win,
            // and a pinner who is not on either side has three people paid for one result.
            if (winningSide.Count == 0) throw new ArgumentException("The winning side is empty.", nameof(winningSide));
            if (losingSide.Count == 0)  throw new ArgumentException("The losing side is empty.", nameof(losingSide));
            if (!winningSide.Contains(pinner))
                throw new ArgumentException(
                    $"{pinner.RingName} scored the fall but is not on the winning side.", nameof(pinner));
            if (!losingSide.Contains(pinned))
                throw new ArgumentException(
                    $"{pinned.RingName} took the fall but is not on the losing side.", nameof(pinned));
            if (winningSide.Intersect(losingSide).Any())
                throw new ArgumentException("A wrestler cannot be on both sides.", nameof(winningSide));

            var core = ForMatch(
                pinner, pinned, starRating, finish, familiarity,
                winnerStanding: SideStanding(winningSide, winningChemistry),
                loserStanding:  SideStanding(losingSide, losingChemistry));

            var partners = new List<StatusChange>();

            // The share is taken from the *undampened* swing, then dampened once against
            // this partner's own ceiling.
            //
            // Taking it from core.Winner.OvernessDelta instead charged the partner for the
            // pinner's ceiling compression as well as his own, and the worst case was the
            // one this whole method exists for: a rookie partnered with a 95-overness star
            // received about a sixth of what the constant says, because the star's own
            // compression had already eaten it. The nominal 50%/35% were coming out as
            // 24%/30%, and 47%/11% at the extremes.
            foreach (var w in winningSide.Where(m => m != pinner))
                partners.Add(new StatusChange(
                    w,
                    DampenGain(w.Overness, core.RawWinnerOverness * PartnerWinShare),
                    core.Winner.MomentumDelta * PartnerWinShare,
                    $"On the winning team, but {pinner.RingName} scored the fall."));

            foreach (var w in losingSide.Where(m => m != pinned))
                partners.Add(new StatusChange(
                    w,
                    -DampenLoss(w.Overness, core.RawLoserOverness * PartnerLossShare),
                    core.Loser.MomentumDelta * PartnerLossShare,
                    $"On the losing team, but {pinned.RingName} took the fall."));

            return core with { Partners = partners };
        }

        public static MatchStatusOutcome ForMatch(
            Wrestler winner, Wrestler loser, double starRating, FinishWeight finish,
            double familiarity = 1.0,
            double? winnerStanding = null, double? loserStanding = null)
        {
            // Normally each man's own standing. A tag match passes its sides' standing
            // instead, because that is what the audience is weighing.
            double w = winnerStanding ?? winner.EffectiveOverness;
            double l = loserStanding  ?? loser.EffectiveOverness;

            // You can only take status from someone who has it. Beating a nobody is worth
            // nothing however cleanly you do it.
            double prize = Math.Clamp(l / 100.0, 0, 1);

            // How much of an upset this was. Positive = beat someone above you.
            double gap = (l - w) / 100.0;

            // A great match is worth more than a bad one, but it is a multiplier on the
            // status swing, never a substitute for it.
            double quality = 0.6 + Math.Clamp(starRating, 0, 5) / 5.0 * 0.8;

            double decisiveness = finish switch
            {
                FinishWeight.Decisive  => 1.00,
                FinishWeight.Fluke     => 0.50,
                FinishWeight.Protected => 0.35,
                _                      => 1.00
            };

            // Overness: beating someone below you pays nothing (the floor is zero);
            // losing to someone below you is expensive.
            double winnerOvernessScale = Math.Clamp(0.28 + gap * 1.50, 0.00, 1.40);
            double loserOvernessScale  = Math.Clamp(0.30 - gap * 0.85, 0.05, 1.20);

            // Momentum keeps a small floor for the winner — a win is a win, it just may
            // not have meant much.
            double winnerMomentumScale = Math.Clamp(0.35 + gap * 1.40, 0.12, 1.40);
            double loserMomentumScale  = Math.Clamp(0.45 - gap * 0.80, 0.10, 1.20);

            double common = prize * quality * decisiveness * Math.Clamp(familiarity, 0.0, 1.5);

            double winnerOverness = OvernessScale * common * winnerOvernessScale;
            double loserOverness  = OvernessScale * common * loserOvernessScale;
            double winnerMomentum = MomentumScale * common * winnerMomentumScale;
            double loserMomentum  = MomentumScale * common * loserMomentumScale;

            // An outstanding match lifts both people a little regardless of the result:
            // quality builds reputation even in defeat. It is momentum, not overness —
            // respect is not the same as the audience caring who you are.
            double showcase = Math.Max(0, starRating - 3.5) / 1.5 * 6.0;

            // Approaching the ceiling is much harder than leaving the floor, and someone
            // the crowd already ignores has little further to fall.
            double rawWinnerOverness = winnerOverness;
            double rawLoserOverness  = loserOverness;

            winnerOverness = DampenGain(winner.Overness, winnerOverness);
            loserOverness  = DampenLoss(loser.Overness, loserOverness);

            return new MatchStatusOutcome(
                new StatusChange(winner, winnerOverness, winnerMomentum + showcase,
                    DescribeWin(gap, prize, finish)),
                new StatusChange(loser, -loserOverness, -loserMomentum + showcase,
                    DescribeLoss(gap, prize, finish)))
            {
                RawWinnerOverness = rawWinnerOverness,
                RawLoserOverness  = rawLoserOverness
            };
        }

        /// <summary>Reads a finish beat as how decisive the audience found it.</summary>
        public static FinishWeight WeightOf(BeatType finishBeat) => finishBeat switch
        {
            BeatType.FinishClean or BeatType.FinishSubmission or BeatType.FinishSuperFinisher
                => FinishWeight.Decisive,

            BeatType.FinishRollup => FinishWeight.Fluke,

            BeatType.FinishInterference or BeatType.FinishDQ or BeatType.FinishCountout
                => FinishWeight.Protected,

            _ => FinishWeight.Decisive
        };

        // ── Appearances ──────────────────────────────────────────────────────

        /// <summary>
        /// What being on a show is worth by itself. Small, and it is momentum rather than
        /// overness — exposure keeps you warm, it does not make the audience care.
        /// </summary>
        public static StatusChange ForAppearance(Wrestler wrestler, double showScore) =>
            new(wrestler, 0, Math.Clamp(showScore / 100.0 * 4.0 - 1.0, -1.0, 3.0),
                "Appeared on the show");

        // ── Time ─────────────────────────────────────────────────────────────

        /// <summary>
        /// One day of nothing happening. Momentum bleeds toward zero always; overness only
        /// slips once someone has been off screen long enough for the audience to start
        /// forgetting (doc 17 §3.9).
        /// </summary>
        public static void ApplyDailyDecay(Wrestler wrestler, DateOnly today)
        {
            wrestler.Momentum *= MomentumDailyRetention;

            // Stop it creeping around zero forever.
            if (Math.Abs(wrestler.Momentum) < 0.05) wrestler.Momentum = 0;

            int absent = wrestler.LastAppearance is { } last
                ? today.DayNumber - last.DayNumber
                : 0;

            if (absent <= AbsenceGraceDays) return;

            // Cold performers have little further to fall.
            double slip = DampenLoss(wrestler.Overness, AbsenceDailyOverness);
            wrestler.Overness = Math.Clamp(wrestler.Overness - slip, 0, 100);
        }

        // ── Applying ─────────────────────────────────────────────────────────

        /// <summary>Commits a change to the wrestler. The only mutating method here.</summary>
        public static void Apply(StatusChange change)
        {
            var w = change.Wrestler;

            w.Overness = Math.Clamp(w.Overness + change.OvernessDelta, 0, 100);
            w.Momentum = Math.Clamp(w.Momentum + change.MomentumDelta, -100, 100);
        }

        // ── Curves ───────────────────────────────────────────────────────────

        /// <summary>
        /// Gains compress as someone approaches the ceiling. The last ten points of
        /// overness are far harder to buy than the first ten.
        /// </summary>
        public static double DampenGain(double current, double raw) =>
            raw * Math.Pow(Math.Clamp(1.0 - current / 100.0, 0, 1), 0.6);

        /// <summary>
        /// Losses compress near the floor. Someone the audience already ignores cannot be
        /// meaningfully buried any further.
        /// </summary>
        public static double DampenLoss(double current, double raw) =>
            raw * Math.Pow(Math.Clamp(current / 100.0, 0, 1), 0.4);

        // ── Commentary ───────────────────────────────────────────────────────

        private static string DescribeWin(double gap, double prize, FinishWeight finish)
        {
            if (prize < 0.25) return "Beat someone the audience has no investment in";
            if (gap > 0.20) return finish == FinishWeight.Decisive
                ? "Beat a bigger name clean — the win the audience remembers"
                : "Got past a bigger name, but not cleanly";
            if (gap < -0.20) return "Beat someone well below them — worth little";
            return "A win over a peer";
        }

        /// <summary>
        /// Note the sign convention: <paramref name="gap"/> is measured from the winner's
        /// point of view, so a positive gap means the winner was the *smaller* name. The
        /// loser's story is therefore the mirror of the winner's.
        /// </summary>
        private static string DescribeLoss(double gap, double prize, FinishWeight finish)
        {
            if (finish == FinishWeight.Protected) return "Lost, but not cleanly — the audience understands";
            if (gap > 0.20) return "Lost to someone the audience rates below them";
            if (gap < -0.20) return "Lost to a bigger name — little damage";
            return "Lost to a peer";
        }
    }
}
