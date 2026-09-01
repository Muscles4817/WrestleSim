using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>What a title match, a non-title loss or a vacancy did to a belt.</summary>
    public enum TitleEvent
    {
        /// <summary>The champion beat the challenger. The belt stays.</summary>
        Retained,

        /// <summary>
        /// The challenger won the match but not the title — a disqualification or a
        /// count-out. Doc 21 §8.1: the challenger gets his "he almost did it" moment and
        /// the champion walks out still champion.
        /// </summary>
        RetainedOnATechnicality,

        /// <summary>New champion.</summary>
        Changed,

        /// <summary>A vacant belt was won.</summary>
        Filled,

        /// <summary>The champion lost a match the title was not on the line in (§4.1).</summary>
        NonTitleLoss,

        /// <summary>The belt was stripped rather than lost.</summary>
        Vacated
    }

    /// <summary>
    /// One thing that happened to one title, with the arithmetic shown. Held so the show
    /// report can say why the belt is worth more or less than it was this morning.
    /// </summary>
    public sealed record TitleUpdate
    {
        public required Title Title { get; init; }
        public required TitleEvent Event { get; init; }

        /// <summary>Whoever held it going in. Null when the belt was vacant.</summary>
        public Wrestler? OutgoingChampion { get; init; }

        /// <summary>Whoever holds it coming out. Null when the belt was vacated.</summary>
        public Wrestler? Champion { get; init; }

        public double StandingBefore { get; init; }
        public double StandingAfter { get; init; }
        public double PrestigeBefore { get; init; }
        public double PrestigeAfter { get; init; }

        /// <summary>Length of the reign that ended, in days. Zero when none did.</summary>
        public int OutgoingReignDays { get; init; }

        public required string Reason { get; init; }

        public double PrestigeDelta => PrestigeAfter - PrestigeBefore;

        /// <summary>Extra standing a title win or defence is worth to the person, if any.</summary>
        public StatusChange? StatusBonus { get; init; }

        public bool IsMeaningful =>
            Event is not (TitleEvent.Retained or TitleEvent.NonTitleLoss)
            || Math.Abs(PrestigeDelta) >= 0.05;
    }

    /// <summary>
    /// How a title gains and loses value — docs/wrestling-reference/21-championships.md
    /// §3, §4, §5 and §9.
    ///
    /// The arithmetic is pure and exposed piece by piece so each rule can be tested on
    /// its own. The three entry points that touch a <see cref="Title"/> —
    /// <see cref="ResolveTitleMatch"/>, <see cref="ApplyNonTitleLoss"/> and
    /// <see cref="Vacate"/> — are the only mutating methods, and they say so in their
    /// names.
    /// </summary>
    public static class TitleEconomy
    {
        // ── Tuning ───────────────────────────────────────────────────────────

        /// <summary>
        /// Days a champion has before the belt starts to look undefended. Doc 21 §9 asks
        /// for monthly at minimum on a weekly-television schedule; this allows a little
        /// slack on top of that before the bleed starts.
        /// </summary>
        public const int DefenceGraceDays = 45;

        /// <summary>Standing lost per day once a title has gone undefended past the grace.</summary>
        private const double UndefendedDailyLoss = 0.14;

        /// <summary>Standing lost per day while the belt sits vacant.</summary>
        private const double VacantDailyLoss = 0.06;

        /// <summary>
        /// Fraction of the gap between the belt's standing and the champion's own
        /// standing closed each day, while the belt is being defended. This is doc 21 §3
        /// row one — "a great champion holds it, the title borrows their status" — and it
        /// runs both ways: a champion the audience does not believe in slowly drags the
        /// belt down to their level.
        /// </summary>
        private const double ChampionPullPerDay = 0.0015;

        /// <summary>
        /// Standing a defended reign accrues per day just by continuing. Small on
        /// purpose: only a genuinely long reign collects enough of it to matter, which is
        /// the scarcity argument in doc 21 §5.2.
        /// </summary>
        private const double ScarcityPerDay = 0.008;

        /// <summary>Standing a vacancy costs outright. Every vacancy weakens the lineage (§4).</summary>
        public const double VacancyCost = 5.0;

        // ── The rules, as pure arithmetic ────────────────────────────────────

        /// <summary>
        /// Whether a finish of this weight can move a championship.
        ///
        /// The classic rule: titles do not change hands on a disqualification or a
        /// count-out (doc 21 §8.1). <see cref="HeatEconomy.WeightOf"/> already classifies
        /// those finishes as <see cref="FinishWeight.Protected"/>, so that one check does
        /// the whole job. A roll-up is a legal pinfall and does move the belt — it just
        /// convinces nobody, which <see cref="ChangeDelta"/> charges for separately.
        /// </summary>
        public static bool ChangesHands(FinishWeight finish) => finish != FinishWeight.Protected;

        /// <summary>
        /// What a successful defence is worth. Frequent *credible* defences are what make
        /// a belt look like something worth having (§3) — beating an enhancement talent
        /// once a week is not a defence in any sense the audience recognises.
        /// </summary>
        public static double DefenceGain(double starRating, Wrestler challenger, FinishWeight finish)
        {
            double quality     = Math.Clamp(starRating, 0, 5) / 5.0;
            double credibility = Math.Clamp(challenger.EffectiveOverness / 100.0, 0, 1);

            double gain = 1.6 * quality * (0.35 + 0.65 * credibility);

            // Retaining by count-out or disqualification protects the challenger, not the
            // belt. The travelling-champion finish is a tool, not a free defence (§8.1).
            if (finish == FinishWeight.Protected) gain *= 0.45;

            return gain;
        }

        /// <summary>
        /// What a title change does to the belt. Positive is possible and is the point:
        /// a long chase paid off in a big match to someone the audience has accepted
        /// leaves the title stronger than it was.
        ///
        /// Four forces, all from doc 21:
        ///   • **Churn** (§4) — a belt that changes every few weeks means nothing.
        ///   • **Handback** (§5.2, and the sim note) — the outgoing champion lent the belt
        ///     their status and takes some of it with them, so the bigger the reign the
        ///     bigger the moment of its ending.
        ///   • **Rejection** (§4) — a title won by someone the audience is well below
        ///     believing in transfers that disbelief straight to the belt.
        ///   • **The win itself** (§6.3) — a decisive win in a big match by someone who
        ///     has been built pays for a great deal of the above.
        /// </summary>
        public static double ChangeDelta(
            double prestigeBefore,
            int outgoingReignDays,
            Wrestler newChampion,
            double starRating,
            FinishWeight finish)
        {
            double quality     = Math.Clamp(starRating, 0, 5) / 5.0;
            double credibility = Math.Clamp(newChampion.EffectiveOverness / 100.0, 0, 1);

            // Four months is roughly where a reign stops reading as a placeholder.
            double churn = Math.Clamp(1.0 - outgoingReignDays / 120.0, 0, 1);

            double handback = 2.5 * Math.Clamp(outgoingReignDays / 365.0, 0, 1.2);

            double rejection = Math.Clamp(
                (prestigeBefore - newChampion.EffectiveOverness) / 100.0, 0, 1);

            double loss = 7.0 * churn + handback + 9.0 * rejection;

            // Won on a roll-up. Legal, and nobody believes it settled anything.
            if (finish == FinishWeight.Fluke) loss += 2.5;

            double gain = 5.0 * quality * credibility;

            return gain - loss;
        }

        /// <summary>
        /// What filling a vacancy does to the belt. No reign ended, so there is no churn
        /// and no aura to hand back — the vacancy was already charged for when it
        /// happened (§4). What is left is whether the audience believes the person now
        /// carrying it, and whether the match that settled it was worth watching.
        /// </summary>
        public static double FillDelta(
            double prestigeBefore, Wrestler newChampion, double starRating, FinishWeight finish)
        {
            double quality     = Math.Clamp(starRating, 0, 5) / 5.0;
            double credibility = Math.Clamp(newChampion.EffectiveOverness / 100.0, 0, 1);

            double rejection = Math.Clamp(
                (prestigeBefore - newChampion.EffectiveOverness) / 100.0, 0, 1);

            double delta = 5.0 * quality * credibility - 9.0 * rejection;

            // Settling a vacant championship on a roll-up settles nothing.
            if (finish == FinishWeight.Fluke) delta -= 1.5;

            return delta;
        }

        /// <summary>
        /// A champion losing a match the belt was not on the line in — doc 21 §4.1.
        /// Small and cumulative by design: used once it sets up a challenger, used
        /// routinely it hollows the championship out.
        /// </summary>
        public static double NonTitleLossPenalty(
            Wrestler champion, Wrestler opponent, FinishWeight finish)
        {
            double decisiveness = finish switch
            {
                FinishWeight.Decisive  => 1.00,
                FinishWeight.Fluke     => 0.60,
                _                      => 0.35
            };

            double penalty = 1.5 * decisiveness;

            // Losing to someone well below you says the belt is a technicality rather
            // than a mark of being the best.
            if (opponent.EffectiveOverness < champion.EffectiveOverness - 15)
                penalty += 1.0;

            return penalty;
        }

        // ── Match resolution ─────────────────────────────────────────────────

        /// <summary>
        /// Runs a title match against the belt. Mutates the title: closes or opens a
        /// reign, counts a defence, and moves the standing.
        /// </summary>
        public static TitleUpdate ResolveTitleMatch(
            Title title,
            Wrestler winner,
            Wrestler loser,
            FinishWeight finish,
            double starRating,
            DateOnly date,
            string showName = "")
        {
            double standingBefore = title.Standing;
            double prestigeBefore = title.Prestige;
            var champion = title.Champion;

            // ── Vacant belt: somebody has to win it ──────────────────────────
            if (champion == null)
            {
                title.Standing = Clamp(standingBefore + FillDelta(prestigeBefore, winner, starRating, finish));
                OpenReign(title, winner, date, showName);

                return Build(title, TitleEvent.Filled, null, winner,
                    standingBefore, prestigeBefore, 0,
                    $"{winner.RingName} wins the vacant title.",
                    WinBonus(title, winner));
            }

            bool championWon = winner == champion;

            // ── Champion retained ───────────────────────────────────────────
            if (championWon || !ChangesHands(finish))
            {
                var challenger = championWon ? loser : winner;
                double gain = DefenceGain(starRating, challenger, championWon ? finish : FinishWeight.Protected);

                title.Standing = Clamp(standingBefore + gain);

                var reign = title.CurrentReign!;
                reign.Defences++;
                reign.LastDefended = date;

                var evt = championWon ? TitleEvent.Retained : TitleEvent.RetainedOnATechnicality;
                string reason = championWon
                    ? $"{champion.RingName} defends against {challenger.RingName}."
                    : $"{challenger.RingName} wins the match but not the title — it does not change hands like that.";

                return Build(title, evt, champion, champion,
                    standingBefore, prestigeBefore, 0, reason,
                    DefenceBonus(title, champion));
            }

            // ── New champion ────────────────────────────────────────────────
            var outgoing = title.CurrentReign!;
            int reignDays = outgoing.DaysHeld(date);

            double delta = ChangeDelta(prestigeBefore, reignDays, winner, starRating, finish);
            title.Standing = Clamp(standingBefore + delta);

            CloseReign(outgoing, date, showName);
            OpenReign(title, winner, date, showName);

            return Build(title, TitleEvent.Changed, champion, winner,
                standingBefore, prestigeBefore, reignDays,
                $"{winner.RingName} ends {champion.RingName}'s {reignDays}-day reign.",
                WinBonus(title, winner));
        }

        /// <summary>
        /// Charges a champion's belt for a defeat in a match it was not on the line in.
        /// Mutates the title's standing. Doc 21 §4.1.
        /// </summary>
        public static TitleUpdate ApplyNonTitleLoss(
            Title title, Wrestler opponent, FinishWeight finish)
        {
            var champion = title.Champion
                ?? throw new InvalidOperationException("A vacant title cannot lose a non-title match.");

            double standingBefore = title.Standing;
            double prestigeBefore = title.Prestige;

            double penalty = NonTitleLossPenalty(champion, opponent, finish);
            title.Standing = Clamp(standingBefore - penalty);

            return Build(title, TitleEvent.NonTitleLoss, champion, champion,
                standingBefore, prestigeBefore, 0,
                $"{champion.RingName} lost to {opponent.RingName} with the title not on the line.",
                null);
        }

        /// <summary>
        /// Strips the belt. Every vacancy weakens the lineage (§4), so it always costs,
        /// however good the reason.
        /// </summary>
        public static TitleUpdate Vacate(Title title, DateOnly date, string reason = "Vacated")
        {
            double standingBefore = title.Standing;
            double prestigeBefore = title.Prestige;
            var champion = title.Champion;
            int reignDays = title.CurrentReign?.DaysHeld(date) ?? 0;

            if (title.CurrentReign is { } reign)
            {
                CloseReign(reign, date, reason);
                reign.Vacated = true;
            }

            title.Standing = Clamp(standingBefore - VacancyCost);

            return Build(title, TitleEvent.Vacated, champion, null,
                standingBefore, prestigeBefore, reignDays, reason, null);
        }

        // ── Time ─────────────────────────────────────────────────────────────

        /// <summary>
        /// One day of nothing happening to a title. Mirrors
        /// <see cref="HeatEconomy.ApplyDailyDecay"/>: a belt that is defended borrows its
        /// champion's standing and slowly accrues scarcity; one that is not being
        /// defended, or is sitting vacant, bleeds. Doc 21 §4 — being ignored is the
        /// fastest killer.
        /// </summary>
        public static void ApplyDailyDrift(Title title, DateOnly today)
        {
            if (title.Retired) return;

            if (title.CurrentReign is not { } reign)
            {
                title.Standing = Clamp(title.Standing - VacantDailyLoss);
                return;
            }

            int sinceDefence = today.DayNumber - (reign.LastDefended ?? reign.Won).DayNumber;

            if (sinceDefence > DefenceGraceDays)
            {
                title.Standing = Clamp(title.Standing - UndefendedDailyLoss);
                return;
            }

            double pull = (reign.Champion.EffectiveOverness - title.Standing) * ChampionPullPerDay;
            title.Standing = Clamp(title.Standing + pull + ScarcityPerDay);
        }

        // ── Status transfer ──────────────────────────────────────────────────

        /// <summary>
        /// What winning the belt is worth to the person, on top of what beating the
        /// champion was already worth. Doc 21 §3: "winning it visibly changes a career" —
        /// and it scales with prestige, so taking a devalued belt changes very little.
        /// </summary>
        public static StatusChange WinBonus(Title title, Wrestler winner)
        {
            double share = title.Prestige / 100.0;

            return new StatusChange(
                winner,
                HeatEconomy.DampenGain(winner.Overness, share * 3.0),
                share * 22.0,
                $"Won the {title.Name}");
        }

        /// <summary>A defence is worth keeping warm, not a new career step.</summary>
        public static StatusChange DefenceBonus(Title title, Wrestler champion) =>
            new(champion, 0, title.Prestige / 100.0 * 6.0, $"Defended the {title.Name}");

        // ── Internals ────────────────────────────────────────────────────────

        private static void OpenReign(Title title, Wrestler champion, DateOnly date, string showName)
        {
            title.Lineage.Add(new TitleReign
            {
                Champion     = champion,
                ReignNumber  = title.Lineage.Count + 1,
                Won          = date,
                WonAt        = showName,
                LastDefended = null
            });
        }

        private static void CloseReign(TitleReign reign, DateOnly date, string showName)
        {
            reign.Lost   = date;
            reign.LostAt = showName;
        }

        private static double Clamp(double standing) => Math.Clamp(standing, 0, 100);

        private static TitleUpdate Build(
            Title title, TitleEvent evt, Wrestler? outgoing, Wrestler? champion,
            double standingBefore, double prestigeBefore, int reignDays,
            string reason, StatusChange? bonus) => new()
        {
            Title             = title,
            Event             = evt,
            OutgoingChampion  = outgoing,
            Champion          = champion,
            StandingBefore    = standingBefore,
            StandingAfter     = title.Standing,
            PrestigeBefore    = prestigeBefore,
            PrestigeAfter     = title.Prestige,
            OutgoingReignDays = reignDays,
            Reason            = reason,
            StatusBonus       = bonus
        };
    }
}
