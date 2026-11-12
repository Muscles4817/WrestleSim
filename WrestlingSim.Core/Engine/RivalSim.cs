using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// The rivals' day. Their shows are abstract — no beats are worked — but their business is
    /// not: the house is sold through the same box office as the player's, at the same prices,
    /// against the same competition, and a promotion that runs out of money closes.
    /// </summary>
    public static partial class World
    {
        /// <summary>How far below zero a rival's cash can go before it closes: half what it opened with.</summary>
        public const decimal FoldAt = -0.5m;

        public static void Tick(Career career)
        {
            var today = career.CurrentDate;

            foreach (var rival in career.Rivals.Where(r => r.IsActive).ToList())
            {
                var profile = Profile(rival.Motive);
                var economics = BoxOffice.For(rival.Promotion.Tier);

                // The office and the guarantees, every day.
                var overhead = economics.DailyOverhead * (decimal)profile.Overhead;
                var payroll = rival.DailyPayroll;
                rival.Books.Cash -= overhead + payroll;
                rival.Books.Overheads += overhead;
                rival.Books.Payroll += payroll;

                SettleContracts(career, rival);

                while (rival.Television is { } deal && today >= deal.End)
                {
                    var term = Television.Renew(deal, rival.Promotion.Tier);
                    career.AddNews($"{rival.Name} renewed its television deal at ×{term.RenewedAt:0.00}.");
                }

                if (today >= rival.NextShow)
                {
                    RunNight(career, rival, today);
                    rival.NextShow = today.AddDays(rival.Promotion.TypicalShowIntervalDays);
                }

                // Talent business once a week, on a day of its own.
                if ((today.DayNumber + Math.Abs(StableSeed.From(rival.Id))) % 7 == 0)
                    Business(career, rival);

                if (rival.Books.Cash < rival.Books.OpeningCash * FoldAt)
                    Fold(career, rival);
            }
        }

        // ── The night ────────────────────────────────────────────────────────

        private static void RunNight(Career career, RivalPromotion rival, DateOnly today)
        {
            var available = rival.Contracts
                .Select(c => career.FindWrestler(c.WrestlerId))
                .OfType<Wrestler>()
                .Where(w => !w.IsInjured(today)
                         && career.NonCompeteOf(w) is null
                         && !career.IsGuaranteed(w)
                         && (career.SignedTo(w) is null || career.SignedTo(w) == rival))
                .ToList();

            var type = rival.Promotion.HasTelevision
                ? (rival.Nights.Count % 4 == 3 ? ShowType.PremiumEvent : ShowType.Television)
                : ShowType.PremiumEvent;

            // A card the size its audience expects, in the slot its tier runs. A thin card now
            // costs the house (AudienceModel.CardDepth), for rivals as for the player.
            var card = PairUp(available, rival.Promotion.TypicalCardSizeFor(type));
            if (card.Count == 0) return;

            var show = new ScheduledShow
            {
                Date = today, Type = type,
                Attendance = rival.Promotion.TypicalAttendanceFor(type),
                RuntimeMinutes = rival.Promotion.DefaultRuntimeFor(type),
                Name = rival.Name
            };
            show.Card.AddRange(card);

            var f = BoxOffice.Night(show, rival.Promotion, rival.Books.Reputation, null, rival.Television,
                w => rival.ContractOf(w)?.IsGuaranteed == true ? 0m : career.PerDateAsk(w), career.Interest);

            var worked = card.SelectMany(i => i.Wrestlers).ToList();
            double rating = Rating(rival, worked, today);

            rival.Books.Cash += f.Net;
            rival.Books.Reputation = BoxOffice.ReputationAfter(rival.Books.Reputation, rating, type);

            int viewers = 0;
            if (f.WasTelevised)
            {
                viewers = (int)Math.Round(f.TuneIn * Television.Retention(rating));
                rival.Television?.Episodes.Add(new EpisodeRating { Date = today, Viewers = viewers });
            }

            // On screen somewhere, so not forgotten (doc 17 §4) — the off-screen decay is for
            // the people nobody is booking.
            foreach (var w in worked) w.LastAppearance = today;

            rival.Nights.Add(new RivalNight
            {
                Date = today, Type = type, Rating = Math.Round(rating, 1),
                Attendance = f.Attendance, Capacity = f.Capacity, Viewers = viewers, Net = f.Net,
                Headline = f.Draws.FirstOrDefault()?.Label ?? ""
            });
            if (rival.Nights.Count > 12) rival.Nights.RemoveAt(0);
        }

        /// <summary>A card from a roster: each division paired off best against next-best.</summary>
        public static List<ICardItem> PairUp(IEnumerable<Wrestler> roster, int matches) =>
            roster.GroupBy(w => w.Division)
                .SelectMany(g => g.OrderByDescending(w => w.EffectiveOverness).ThenBy(w => w.Id, StringComparer.Ordinal)
                                  .Chunk(2).Where(p => p.Length == 2))
                .OrderByDescending(p => p.Max(w => w.EffectiveOverness))
                .Take(matches)
                .Select(p => (ICardItem)new BookedMatch
                {
                    Plan = new Models.MatchPlan.MatchPlan { WrestlerA = p[0], WrestlerB = p[1] },
                    StructureName = "Abstract"
                })
                .ToList();

        /// <summary>
        /// An abstract show's rating, on the same 0–100 scale as the player's: who worked it,
        /// how good they are, how over they are, what the promotion is for, and a seeded night's
        /// variance. A typical card from a typical roster lands near fifty.
        /// </summary>
        public static double Rating(RivalPromotion rival, IReadOnlyList<Wrestler> worked, DateOnly on)
        {
            if (worked.Count == 0) return 0;
            double skill = worked.Average(w => w.RingSkills.GetOverallSkill());
            double over = worked.Average(w => w.EffectiveOverness);
            var rng = new Random(StableSeed.From(rival.Id, on.DayNumber));
            double noise = (rng.NextDouble() * 2 - 1) * 8;
            return Math.Clamp(22 + 6 * skill + 0.2 * over + Profile(rival.Motive).RatingBias + noise, 5, 95);
        }

        // ── Talent ───────────────────────────────────────────────────────────

        private static decimal BudgetOf(RivalPromotion rival) =>
            TierOpeningRoster.For(rival.Promotion.Tier).Budget * (decimal)Profile(rival.Motive).Budget;

        /// <summary>Whether a rival would keep or chase somebody: at least as over as its middle.</summary>
        private static bool Wants(Career career, RivalPromotion rival, Wrestler w)
        {
            var overs = rival.Contracts
                .Select(c => career.FindWrestler(c.WrestlerId)?.EffectiveOverness ?? 0)
                .OrderBy(o => o).ToList();
            var (size, _) = TierOpeningRoster.For(rival.Promotion.Tier);
            if (overs.Count < size) return true;
            return w.EffectiveOverness >= overs[overs.Count / 2];
        }

        /// <summary>A rival's expiring deals: re-signed, lost to a better offer from the player, or let go.</summary>
        private static void SettleContracts(Career career, RivalPromotion rival)
        {
            var today = career.CurrentDate;
            foreach (var c in rival.Contracts.Where(c => c.End is not null).ToList())
            {
                var w = career.FindWrestler(c.WrestlerId);
                if (w is not null && w.IsInjured(today))
                {
                    c.End = c.End!.Value.AddDays(1);
                    c.DaysTolled++;
                    continue;
                }
                if (today < c.End) continue;

                rival.Contracts.Remove(c);
                if (w is null) continue;

                var profile = Profile(rival.Motive);
                decimal payroll = rival.Contracts.Where(x => x.IsGuaranteed).Sum(x => x.AnnualGuarantee);
                decimal keep = Math.Round(career.GuaranteeAsk(w, 3) * (decimal)profile.BidPremium / 1_000m) * 1_000m;
                bool wants = Wants(career, rival, w) && payroll + keep <= BudgetOf(rival) * 1.1m;
                var mine = career.PlayerBid(w);

                if (mine is not null && mine.Annual > (wants ? keep : 0m))
                {
                    career.Contracts.Add(new Contract
                    {
                        WrestlerId = w.Id, Kind = ContractKind.Guaranteed, AnnualGuarantee = mine.Annual,
                        Start = today, End = today.AddYears(mine.Years)
                    });
                    career.Log(w, $"joined from {rival.Name} at ${mine.Annual:N0} a year");
                    career.AddNews($"{w.RingName} has left {rival.Name} for {career.Promotion.Name}.", aboutYou: true);
                }
                else if (wants)
                {
                    rival.Contracts.Add(new Contract
                    {
                        WrestlerId = w.Id, Kind = ContractKind.Guaranteed, AnnualGuarantee = keep,
                        Start = today, End = today.AddYears(3)
                    });
                    if (mine is not null)
                    {
                        career.Log(w, $"stayed with {rival.Name} at ${keep:N0} a year — your offer was beaten");
                        career.AddNews($"{w.RingName} re-signed with {rival.Name}, turning down {career.Promotion.Name}.", aboutYou: true);
                    }
                }
                else
                {
                    career.AddNews($"{w.RingName}'s deal with {rival.Name} ran out. A free agent.");
                }
                career.Bids.RemoveAll(b => b.WrestlerId == w.Id);
            }
        }

        /// <summary>The week's business: releases if the money is short, bids, and signings.</summary>
        private static void Business(Career career, RivalPromotion rival)
        {
            var today = career.CurrentDate;
            var profile = Profile(rival.Motive);
            var (size, _) = TierOpeningRoster.For(rival.Promotion.Tier);
            var budget = BudgetOf(rival);

            // Regulars somebody else has since signed are not regulars any more.
            rival.Contracts.RemoveAll(c => !c.IsGuaranteed
                && career.FindWrestler(c.WrestlerId) is { } w
                && (career.IsGuaranteed(w) || (career.SignedTo(w) is { } other && other != rival)));

            if (!rival.Promotion.HasTelevision)
            {
                // Below television: keep a list of regulars, booked by the night.
                var ceiling = TierOpeningRoster.For(rival.Promotion.Tier).Budget;
                foreach (var w in FreeAgents(career).Where(w => rival.ContractOf(w) is null && career.PerDateAsk(w) <= ceiling)
                                                    .Take(Math.Max(0, size - rival.Contracts.Count)))
                    rival.Contracts.Add(new Contract { WrestlerId = w.Id, Kind = ContractKind.Regular, Start = today });
                return;
            }

            decimal Payroll() => rival.Contracts.Where(c => c.IsGuaranteed).Sum(c => c.AnnualGuarantee);

            // Doc 13 §2.1: budget releases — the worst value first, a couple at a time.
            int released = 0;
            while ((rival.Books.Cash < 0 || Payroll() > budget * 1.15m) && released < 2)
            {
                var worst = rival.Contracts.Where(c => c.IsGuaranteed)
                    .Select(c => (c, w: career.FindWrestler(c.WrestlerId)))
                    .Where(x => x.w is not null)
                    .OrderBy(x => x.w!.EffectiveOverness / (double)Math.Max(1m, x.c.AnnualGuarantee))
                    .FirstOrDefault();
                if (worst.w is null) break;

                var days = Math.Min(TalentMarket.ReleasePayoutDays, worst.c.DaysLeft(today) ?? 0);
                rival.Books.Cash -= Math.Round(worst.c.DailyCost * days);
                rival.Contracts.Remove(worst.c);
                career.NonCompetes.Add(new NonCompete { WrestlerId = worst.w.Id, Until = today.AddDays(TalentMarket.ReleasePayoutDays), From = rival.Id });
                career.AddNews($"{rival.Name} released {worst.w.RingName}. Free in ninety days.");
                released++;
            }
            if (released > 0) return;

            // Doc 10 §2.6: bids for the player's people whose deals are running out.
            foreach (var c in career.ExpiringWithin(TalentMarket.ExpiryWarningDays).Where(c => c.IsGuaranteed).ToList())
            {
                var w = career.FindWrestler(c.WrestlerId);
                if (w is null || career.Bids.Any(b => b.WrestlerId == w.Id && b.Bidder == rival.Id)) continue;
                if (!Wants(career, rival, w)) continue;

                var bid = Math.Round(career.GuaranteeAsk(w, 3) * (decimal)profile.BidPremium / 1_000m) * 1_000m;
                if (Payroll() + bid > budget * 1.1m) continue;

                career.Bids.Add(new Bid { WrestlerId = w.Id, Bidder = rival.Id, Annual = bid, Years = 3, Made = today });
                career.AddNews($"{rival.Name} has offered {w.RingName} ${bid:N0} a year to leave when the deal ends on {c.End:d MMM}.", aboutYou: true);
            }

            // Signings, one or two a week, while there is room and money — and never while the
            // cash is short. A broke promotion that releases on Sunday and signs on Monday was
            // churning the same names through the door every week.
            bool flush = rival.Books.Cash > rival.Books.OpeningCash * 0.1m;
            for (int i = 0; flush && i < 2 && rival.Contracts.Count(c => c.IsGuaranteed) < size; i++)
            {
                decimal left = budget - Payroll();
                int slots = size - rival.Contracts.Count(c => c.IsGuaranteed);
                decimal share = left / Math.Max(1, slots) * 3m;

                var pick = FreeAgents(career)
                    .Select(w => (w, ask: career.GuaranteeAsk(w, 3)))
                    .Where(x => x.ask <= left && x.ask <= share)
                    .OrderByDescending(x => profile.BuysStars
                        ? x.w.EffectiveOverness
                        : x.w.EffectiveOverness / (double)Math.Max(1m, x.ask) * 1_000_000)
                    .FirstOrDefault();
                if (pick.w is null) break;

                rival.Contracts.RemoveAll(c => c.WrestlerId == pick.w.Id);
                rival.Contracts.Add(new Contract
                {
                    WrestlerId = pick.w.Id, Kind = ContractKind.Guaranteed, AnnualGuarantee = pick.ask,
                    Start = today, End = today.AddYears(3)
                });
                career.AddNews($"{rival.Name} signed {pick.w.RingName} for three years.");
            }
        }

        /// <summary>Anybody nobody holds exclusively, and who is free to work.</summary>
        private static IEnumerable<Wrestler> FreeAgents(Career career) =>
            career.Roster.Where(w => !career.IsGuaranteed(w)
                                  && career.SignedTo(w) is null
                                  && career.NonCompeteOf(w) is null
                                  && career.PlayerBid(w) is null)
                         .OrderByDescending(w => w.EffectiveOverness)
                         .ThenBy(w => w.Id, StringComparer.Ordinal);

        /// <summary>
        /// Out of money. Everybody it held is a free agent at once — a closed promotion has no
        /// non-compete to enforce — and the world has one employer fewer.
        /// </summary>
        private static void Fold(Career career, RivalPromotion rival)
        {
            int freed = rival.Contracts.Count(c => c.IsGuaranteed);
            rival.Folded = career.CurrentDate;
            rival.Contracts.Clear();
            rival.Television = null;
            career.Bids.RemoveAll(b => b.Bidder == rival.Id);
            career.AddNews($"{rival.Name} has closed. {freed} wrestler{(freed == 1 ? " is" : "s are")} free agents.", aboutYou: true);
        }
    }
}
