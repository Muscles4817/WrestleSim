using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// What a promotion expects of itself with a given roster, over the period it plans for.
    /// The planner compares two of these — the roster it has and the roster a move would give
    /// it — and nothing else.
    /// </summary>
    public sealed record Outlook(
        double  CardPull,
        double  Rating,
        double  Fill,
        int     Viewers,
        decimal RevenuePerShow,
        decimal NetPerShow,           // before running costs and guarantees; includes its optimism
        decimal DailyFixed,           // running costs and guarantees, a day
        double  ShowsInHorizon,
        decimal CashAtHorizon,
        decimal Equity,               // what its contracts are worth beyond what they cost
        double  ReputationAtHorizon,
        double  RenewalMultiple,      // 1.0 below television
        double  YearsToRenewal,
        int     RunwayDays,           // days until it would have to close; capped at 9999
        double  FavourShare,
        double  Filled);              // how much of its slot the cards filled

    public static partial class World
    {
        /// <summary>How many shows ahead the forecast books. Enough for a thin roster's match-ups to start repeating.</summary>
        public const int CycleShows = 4;

        /// <summary>Forecasts run on this thread since the counter was last cleared. A planner's cost is counted, not timed.</summary>
        [ThreadStatic] public static int ForecastsRun;

        /// <summary>
        /// Where the forecasts went, by the size of promotion, when somebody is asking: count
        /// and stopwatch ticks. Null — and costing nothing — unless a measurement sets it.
        /// </summary>
        [ThreadStatic] public static Dictionary<PromotionTier, (int Count, long Ticks)>? ForecastCost;

        /// <summary>What kind of show a rival's next one is, given the shows before it.</summary>
        public static ShowType NextShowType(Promotion promotion, DateOnly date, IEnumerable<(DateOnly Date, ShowType Type)> before)
        {
            if (!promotion.HasTelevision) return ShowType.PremiumEvent;
            var past = before.ToList();
            // With television, the big night comes round about once a month (doc 29 §1: 12–16
            // major events a year at the top), not every fourth show.
            bool due = past.Count >= 3
                && !past.Any(n => n.Type == ShowType.PremiumEvent && date.DayNumber - n.Date.DayNumber < 28);
            return due ? ShowType.PremiumEvent : ShowType.Television;
        }

        /// <summary>
        /// Whether adding this wrestler to the roster a forecast was made for could have changed
        /// any card in it — read off the forecast's own cut lines, without forecasting again.
        ///
        /// **False is exact, not an estimate.** If they could not have got onto the first card
        /// it is the card that was booked, so the audience remembers the same things going
        /// into the second, and so on down the month: every card is the same card, and the
        /// only thing the wrestler changes is what they are paid. A promotion can turn them
        /// down on that alone. True means only "book it and see".
        /// </summary>
        public static bool CouldAppear(RivalPromotion rival, Wrestler w, IReadOnlyList<CutLine> cuts)
        {
            // Nobody the forecast imagined booking is them, so their act only rests.
            double sameness = rival.Audience.SamenessOf(w);
            int streak = rival.Audience.ActOf(w)?.Streak ?? 0;
            double rest = Math.Pow(0.5, Math.Max(1, rival.Promotion.TypicalShowIntervalDays) / AudienceModel.ActHalfLifeDays);

            foreach (var cut in cuts)
            {
                double felt = w.EffectiveOverness * AudienceModel.ResponseAt(w, sameness, streak, rival.Promotion.Tier, cut.Type);
                if (cut.CouldHaveBooked(w.Division, felt)) return true;
                sameness *= rest;
            }
            return cuts.Count == 0;
        }

        /// <summary>
        /// What this roster would do. **Pure**: nothing about the career, the rival or any
        /// wrestler is changed.
        ///
        /// It books the next <see cref="CycleShows"/> shows with <see cref="BookCard"/> — the
        /// booker the world itself uses — and sells each through <see cref="BoxOffice.Night"/>,
        /// carrying its own count of the match-ups and appearances those shows would add, so a
        /// thin roster's third show is staler than its first. Then it scales that to the horizon.
        /// The only thing it does not know is the night's luck.
        ///
        /// <paramref name="assume"/> names wrestlers to treat as free to work even though the
        /// world says otherwise — somebody still under contract elsewhere that it is thinking
        /// of bidding for.
        /// </summary>
        public static Outlook Forecast(
            Career career, RivalPromotion rival, IReadOnlyList<Contract> roster,
            Disposition disposition, decimal oneOffCost = 0m, IReadOnlySet<string>? assume = null,
            List<(ShowType Type, double Pull, List<ICardItem> Card)>? cards = null,
            List<CutLine>? cuts = null)
        {
            ForecastsRun++;
            long started = ForecastCost is null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();

            var today = career.CurrentDate;
            var promotion = rival.Promotion;
            var tier = promotion.Tier;
            int interval = Math.Max(1, promotion.TypicalShowIntervalDays);
            var traits = Profile(rival.Motive);
            var economics = BoxOffice.For(tier);

            var guaranteed = roster.Where(c => c.IsGuaranteed).Select(c => c.WrestlerId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var people = roster
                .Select(c => career.FindWrestler(c.WrestlerId))
                .OfType<Wrestler>()
                .Where(w => !w.IsInjured(today) && career.NonCompeteOf(w) is null
                         && (assume?.Contains(w.Id) == true
                             || (!career.IsGuaranteed(w) && (HolderOf(career, w) is not { } holder || holder == rival))))
                .ToList();

            // Its own count of what the shows it is imagining would add.
            var sameness = people.ToDictionary(w => w.Id, w => rival.Audience.SamenessOf(w), StringComparer.OrdinalIgnoreCase);
            var streaks = people.ToDictionary(w => w.Id, w => rival.Audience.ActOf(w)?.Streak ?? 0, StringComparer.OrdinalIgnoreCase);
            var extra = new Dictionary<string, double>();
            static string KeyOf(BookedMatch m)
            {
                var w = m.Wrestlers;
                if (w.Count == 2)
                    return string.CompareOrdinal(w[0].Id, w[1].Id) <= 0 ? w[0].Id + "|" + w[1].Id : w[1].Id + "|" + w[0].Id;
                return string.Join("|", w.Select(x => x.Id).OrderBy(id => id, StringComparer.Ordinal));
            }

            var nights = rival.Nights.Select(n => (n.Date, n.Type)).ToList();
            var first = rival.NextShow > today ? rival.NextShow : today.AddDays(interval);

            double pull = 0, rating = 0, fill = 0, filled = 0, favour = 0;
            long viewers = 0;
            int televised = 0, ran = 0;
            decimal revenue = 0, net = 0;
            ShowType usual = promotion.HasTelevision ? ShowType.Television : ShowType.PremiumEvent;

            for (int k = 0; k < CycleShows; k++)
            {
                var date = first.AddDays(k * interval);
                var type = NextShowType(promotion, date, nights);
                nights.Add((date, type));

                double Met(BookedMatch m) =>
                    (BoxOffice.StoryBetween(m.Plan, rival.Feuds)?.MeetingsRemembered(date) ?? 0)
                    + (extra.Count == 0 ? 0 : extra.GetValueOrDefault(KeyOf(m)));
                double Freshness(BookedMatch m) => Feud.FamiliarityFor(Met(m) + 1.0);
                double Response(Wrestler w) =>
                    AudienceModel.ResponseAt(w, sameness.GetValueOrDefault(w.Id), streaks.GetValueOrDefault(w.Id), tier, type);

                CutLine? cut = cuts is null ? null : new CutLine { Type = type };
                cuts?.Add(cut!);
                var card = BookCard(rival, people, type, date, rival.Nights.Count + k, Response, Freshness, cut);
                if (card.Count == 0) continue;

                var show = new ScheduledShow
                {
                    Date = date, Type = type,
                    Attendance = promotion.TypicalAttendanceFor(type),
                    RuntimeMinutes = promotion.DefaultRuntimeFor(type),
                    Name = rival.Name
                };
                show.Card.AddRange(card);

                var f = BoxOffice.Night(show, promotion, rival.Books.Reputation, null, rival.Television,
                    w => guaranteed.Contains(w.Id) ? 0m : career.PerDateAsk(w), career.Interest, Response, Freshness);

                double total = 0, weights = 0;
                for (int i = 0; i < card.Count; i++)
                {
                    double weight = card[i].DurationMinutes * (i == card.Count - 1 ? 1.5 : 1.0);
                    total += ExpectedItemRating(rival, card[i], Response, Freshness) * weight;
                    weights += weight;
                }
                double slot = show.BookedMinutes / (double)show.RuntimeMinutes;
                double night = (weights <= 0 ? 0 : total / weights)
                             * (1 - AudienceModel.UnderrunPenalty(slot))
                             * (1 - AudienceModel.SamenessPenalty(rival.Audience.ShowSameness));
                night = Math.Clamp(night, 5, 95);

                // What it believes it will take: the gate and the merchandise seen through its
                // optimism. Rights are a contract and are not a matter of opinion.
                decimal believed = (f.Gate + f.Merchandise) * (decimal)disposition.Optimism + f.MediaRights;
                revenue += believed;
                net += believed - f.CardCosts;
                pull += f.CardPull;
                cards?.Add((type, f.CardPull, card));
                rating += night;
                fill += f.Fill;
                filled += slot;
                if (f.WasTelevised)
                {
                    viewers += (long)Math.Round(f.TuneIn * Television.Retention(night));
                    televised++;
                }
                if (rival.Favourites.Count > 0 && f.Draws.FirstOrDefault() is { } top)
                {
                    var names = rival.Favourites.Select(id => career.FindWrestler(id)?.RingName).ToHashSet();
                    if (top.Headliners.Any(names.Contains)) favour++;
                }
                ran++;

                // The shows it has imagined so far, added to what it will remember by the next.
                double rest = Math.Pow(0.5, interval / AudienceModel.ActHalfLifeDays);
                foreach (var id in sameness.Keys.ToList()) sameness[id] *= rest;
                foreach (var item in card)
                {
                    double novelty = 0.15;
                    if (item is BookedMatch m)
                    {
                        double met = Met(m);
                        novelty = met < 0.5 ? 1.0 : met < 1.5 ? 0.5 : met < 2.5 ? 0.25 : 0.0;
                        extra[KeyOf(m)] = extra.GetValueOrDefault(KeyOf(m)) + 1;
                    }
                    foreach (var w in item.Wrestlers)
                        sameness[w.Id] = Math.Clamp(
                            sameness.GetValueOrDefault(w.Id)
                            + AudienceModel.Gain * AudienceModel.Rate(w) * (1 - novelty) - AudienceModel.Relief * novelty,
                            0, 100);
                }
            }

            int n = Math.Max(1, ran);
            double avgRating = ran == 0 ? 0 : rating / n;
            decimal revenuePerShow = ran == 0 ? 0 : revenue / n;
            decimal netPerShow = ran == 0 ? 0 : net / n;
            int avgViewers = televised == 0 ? 0 : (int)(viewers / televised);

            int horizon = Math.Max(interval, disposition.HorizonDays);
            double shows = ran == 0 ? 0 : horizon / (double)interval;

            decimal payroll = roster.Sum(c => c.DailyCost);
            decimal dailyFixed = economics.DailyOverhead * (decimal)traits.Overhead + payroll;
            decimal cashAtHorizon = rival.Books.Cash + (decimal)shows * netPerShow - horizon * dailyFixed - oneOffCost;

            double reputation = rival.Books.Reputation;
            for (int i = 0; i < (int)shows; i++)
                reputation = BoxOffice.ReputationAfter(reputation, avgRating, usual);

            double multiple = 1.0, years = 0;
            if (rival.Television is { } deal)
            {
                multiple = Television.ProjectedMultiple(deal, tier, avgViewers, interval, today);
                years = deal.DaysLeft(today) / 365.0;
            }

            decimal foldLine = rival.Books.OpeningCash * FoldAt;
            decimal dailyNet = (decimal)(shows / horizon) * netPerShow - dailyFixed;
            int runway = dailyNet >= 0
                ? 9999
                : (int)Math.Clamp((rival.Books.Cash - oneOffCost - foldLine) / -dailyNet, 0m, 9999m);

            // What it holds beyond what it pays for: a contract signed before somebody got
            // hotter is an asset, and letting it go gives that up.
            decimal equity = 0;
            foreach (var c in roster.Where(c => c.IsGuaranteed))
                if (career.FindWrestler(c.WrestlerId) is { } w)
                {
                    double yearsLeft = Math.Min((c.DaysLeft(today) ?? 0) / 365.0, horizon / 365.0);
                    equity += (TalentMarket.WorthToday(w, c, career.WageFactor) - c.AnnualGuarantee) * (decimal)yearsLeft;
                }

            if (ForecastCost is { } cost)
            {
                var (count, ticks) = cost.GetValueOrDefault(tier);
                cost[tier] = (count + 1, ticks + System.Diagnostics.Stopwatch.GetTimestamp() - started);
            }

            return new Outlook(
                CardPull: ran == 0 ? 0 : pull / n, Rating: avgRating, Fill: ran == 0 ? 0 : fill / n,
                Viewers: avgViewers, RevenuePerShow: revenuePerShow, NetPerShow: netPerShow,
                DailyFixed: dailyFixed, ShowsInHorizon: shows, CashAtHorizon: cashAtHorizon, Equity: equity,
                ReputationAtHorizon: reputation, RenewalMultiple: multiple, YearsToRenewal: years,
                RunwayDays: runway, FavourShare: ran == 0 ? 0 : favour / n, Filled: ran == 0 ? 0 : filled / n);
        }
    }
}
