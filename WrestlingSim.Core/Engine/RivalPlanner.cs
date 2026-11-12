using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// How a rival decides. docs/rival-ai-plan.md §5–6.
    ///
    /// **There are no rules in this file about what a promotion should do.** Each week it lists
    /// the moves open to it, forecasts each with the game's own economics
    /// (<see cref="World.Forecast"/>), scores the result against doing nothing by what the
    /// promotion wants (<see cref="Objective"/>) and how it sees (<see cref="Disposition"/>),
    /// and takes the best one that is worth making. The same code runs for every promotion;
    /// nothing here asks what kind of owner it is planning for.
    ///
    /// When a promotion does something stupid, the fix is the model it forecasts with or the
    /// objective it is given — never a rule added here. And no outcome is to be tuned for: a
    /// result that surprises is something to look at.
    /// </summary>
    public static partial class World
    {
        /// <summary>How many moves a promotion makes in a week at most.</summary>
        public const int MaxMovesPerWeek = 3;

        /// <summary>Friction: a move has to be worth making. Global — a dislike of change is an objective's Stability.</summary>
        public const double Epsilon = 0.002;

        /// <summary>
        /// How much a television renewal a year away is worth against one today. The same
        /// audience gain counts for more as the deal it feeds gets closer.
        /// </summary>
        public const double RenewalDiscount = 0.5;

        /// <summary>A move's score against holding, and what each thing the promotion cares about contributed.</summary>
        public sealed record Score(double Total, IReadOnlyDictionary<string, double> Terms)
        {
            /// <summary>The largest thing in its favour.</summary>
            public string Dominant =>
                Terms.Where(t => t.Value > 0).OrderByDescending(t => t.Value).Select(t => t.Key).FirstOrDefault() ?? "";
        }

        /// <summary>
        /// Scores the outlook after a move against the outlook of holding. Each term is a
        /// relative change, so the weights mean something against each other.
        /// </summary>
        public static Score ScoreOf(
            Outlook hold, Outlook after, Objective wants, Disposition sees, PromotionTier tier, double continuityLost = 0)
        {
            double revenue = Math.Max(1.0, (double)hold.RevenuePerShow * hold.ShowsInHorizon);

            double Barrier(Outlook o) =>
                -sees.RiskAversion * Math.Pow(Math.Max(0, (sees.RunwayFloorDays - o.RunwayDays) / (double)Math.Max(1, sees.RunwayFloorDays)), 2);

            var terms = new Dictionary<string, double>
            {
                ["wealth"]     = wants.Wealth * (double)((after.CashAtHorizon + after.Equity) - (hold.CashAtHorizon + hold.Equity)) / revenue,
                ["franchise"]  = wants.Franchise * Math.Pow(RenewalDiscount, after.YearsToRenewal) * (after.RenewalMultiple - hold.RenewalMultiple),
                ["stars"]      = wants.Stars * (after.CardPull - hold.CardPull) / BoxOffice.TypicalCardPullFor(tier),
                ["quality"]    = wants.Quality * (after.Rating - hold.Rating) / 50.0,
                ["reputation"] = wants.Reputation * (after.ReputationAtHorizon - hold.ReputationAtHorizon) / 50.0,
                ["favour"]     = wants.Favour * (after.FavourShare - hold.FavourShare),
                // Continuity: losing somebody the audience is used to, by how long they have
                // been there. Filling a hole costs nothing.
                ["stability"]  = -wants.Stability * continuityLost / 10.0,
                // Not weighted by what it wants: this is about staying alive, in proportion to
                // how much this promotion fears not doing so.
                ["survival"]   = Barrier(after) - Barrier(hold)
            };
            return new Score(terms.Values.Sum(), terms);
        }

        /// <summary>
        /// Having somebody cannot make a card worse than not having them: anything a promotion
        /// could put on without them, it could put on with them sitting at home. So letting
        /// somebody go is never credited with better shows — only with the money.
        ///
        /// This is here because the booker (<see cref="BookCard"/>) is a recipe and does not
        /// honour that: it defends a title because it is the big night, and builds a tag match
        /// out of whoever is fourth to seventh in a division, so taking a person away can
        /// shuffle it into a card that draws slightly more. Left alone, a promotion that does
        /// not count money released people week after week for gains that were the recipe's
        /// noise, and released a stale champion because it could not think of beating her.
        /// The proper fix is a booker that chooses its card's make-up (plan §8); until then
        /// this states the thing the recipe gets wrong.
        /// </summary>
        public static Outlook NoBetterWithout(Outlook with, Outlook without) => without with
        {
            CardPull            = Math.Min(with.CardPull, without.CardPull),
            Rating              = Math.Min(with.Rating, without.Rating),
            ReputationAtHorizon = Math.Min(with.ReputationAtHorizon, without.ReputationAtHorizon),
            RenewalMultiple     = Math.Min(with.RenewalMultiple, without.RenewalMultiple),
            FavourShare         = Math.Min(with.FavourShare, without.FavourShare)
        };

        private sealed record Candidate(
            string Move, Wrestler Who, List<Contract> Roster, decimal Annual, decimal OneOff,
            double ContinuityLost, IReadOnlySet<string>? Assume, Action Apply, IReadOnlyList<Wrestler>? With = null,
            // The most this could score, where that can be known without a forecast; null where it cannot.
            double? AtMost = null);

        /// <summary>The week's decisions for one rival.</summary>
        public static void Plan(Career career, RivalPromotion rival)
        {
            var wants = ObjectiveOf(rival);
            var sees = DispositionOf(rival);
            var tier = rival.Promotion.Tier;
            var today = career.CurrentDate;

            for (int round = 0; round < MaxMovesPerWeek; round++)
            {
                // Nothing it could act on has changed since it last looked and found nothing
                // worth doing: the same people on its books, the same people on offer, about
                // the same money. It looks again after a month regardless, because its
                // audience and its cash drift under it. A cost device — a week with nothing to
                // decide should cost nothing — and the reason a save carries it, so that a
                // loaded world plans on the same days as the one that was never saved.
                int situation = SituationOf(career, rival);
                if (round == 0 && rival.QuietAs == situation && rival.QuietSince is { } since
                    && today.DayNumber - since.DayNumber < QuietRecheckDays)
                    return;

                var hold = Forecast(career, rival, rival.Contracts, sees);

                var scored = Candidates(career, rival, hold, wants, sees)
                    .Where(c => c.AtMost is not { } most || most > Epsilon)
                    .Select(c =>
                    {
                        var after = Forecast(career, rival, c.Roster, sees, c.OneOff, c.Assume);
                        if (c.ContinuityLost > 0) after = NoBetterWithout(hold, after);
                        return (Candidate: c, Score: ScoreOf(hold, after, wants, sees, tier, c.ContinuityLost));
                    })
                    .OrderByDescending(x => x.Score.Total)
                    .ThenBy(x => x.Candidate.Who.Id, StringComparer.Ordinal)
                    .ThenBy(x => x.Candidate.Move, StringComparer.Ordinal)
                    .ToList();

                if (scored.Count == 0 || scored[0].Score.Total <= Epsilon)
                {
                    if (round == 0 && rival.QuietAs != situation) (rival.QuietAs, rival.QuietSince) = (situation, today);
                    break;
                }
                (rival.QuietAs, rival.QuietSince) = (0, null);

                var (best, score) = scored[0];
                best.Apply();
                var turnedDown = scored.Skip(1).Take(3)
                    .Select(x => $"{x.Candidate.Move} {x.Candidate.Who.RingName} ({x.Score.Total:+0.000;-0.000})").ToList();
                Remember(career, rival, best.Move, best.Who, best.Annual, score, hold, turnedDown);
                foreach (var other in best.With ?? [])
                    Remember(career, rival, best.Move, other, best.Annual, score, hold, turnedDown);
            }
        }

        /// <summary>How long a promotion with nothing to decide goes before looking again anyway.</summary>
        public const int QuietRecheckDays = 28;

        /// <summary>
        /// What a rival's decisions this week depend on that can change from outside: who it
        /// has, who is on offer, whose deals with the player are running out, and roughly how
        /// much money it has. Two weeks with the same answer here are, near enough, the same
        /// week to plan.
        /// </summary>
        private static int SituationOf(Career career, RivalPromotion rival)
        {
            var parts = rival.Contracts.Select(c => c.WrestlerId).OrderBy(id => id, StringComparer.Ordinal)
                .Append("|")
                .Concat(FreeAgents(career).Where(w => rival.ContractOf(w) is null).Select(w => w.Id).OrderBy(id => id, StringComparer.Ordinal))
                .Append("|")
                .Concat(career.ExpiringWithin(TalentMarket.ExpiryWarningDays).Where(c => c.IsGuaranteed).Select(c => c.WrestlerId)
                              .OrderBy(id => id, StringComparer.Ordinal))
                .Append("|")
                // Money in steps of a tenth of what it opened with: a promotion that has lost
                // that much is in a different position, one that has lost a week's takings is not.
                .Append(((int)Math.Floor(rival.Books.Cash / Math.Max(1m, rival.Books.OpeningCash) * 10m)).ToString());
            int hash = StableSeed.From(string.Join(",", parts));
            return hash == 0 ? 1 : hash;
        }

        /// <summary>
        /// The moves open to a rival this week. The shortlists are a cost device, not behaviour:
        /// three different ways of ranking free agents so that every objective's best candidate
        /// is on the list. The objective chooses.
        /// </summary>
        private static IEnumerable<Candidate> Candidates(
            Career career, RivalPromotion rival, Outlook hold, Objective wants, Disposition sees)
        {
            var today = career.CurrentDate;
            bool signs = rival.Promotion.HasTelevision;

            var free = FreeAgents(career).Where(w => rival.ContractOf(w) is null).ToList();
            var shortlist = free.Take(5)
                .Concat(free.OrderByDescending(w => w.RingSkills.GetOverallSkill()).ThenBy(w => w.Id, StringComparer.Ordinal).Take(3))
                .Concat(free.OrderByDescending(w => w.EffectiveOverness / (double)Math.Max(1m, career.PerDateAsk(w)))
                            .ThenBy(w => w.Id, StringComparer.Ordinal).Take(3))
                .Distinct()
                .ToList();

            foreach (var w in shortlist)
            {
                var contract = signs
                    ? new Contract
                    {
                        WrestlerId = w.Id, Kind = ContractKind.Guaranteed,
                        AnnualGuarantee = career.GuaranteeAsk(w, 3), Start = today, End = today.AddYears(3)
                    }
                    : new Contract { WrestlerId = w.Id, Kind = ContractKind.Regular, Start = today };

                yield return new Candidate(
                    signs ? "signed" : "added", w, [.. rival.Contracts, contract], contract.AnnualGuarantee, 0m, 0, null,
                    () =>
                    {
                        rival.Contracts.RemoveAll(c => c.WrestlerId == w.Id);
                        rival.Contracts.Add(contract);
                        if (contract.IsGuaranteed) EndArrangements(career, rival, w);
                    });
            }

            // Several at once, for a promotion that cannot put on a show. One more person does
            // nothing for a roster of none — it takes two to have a match, and more than one
            // match to have a show worth opening the doors for — so looking one move ahead
            // every addition is worth nothing or less, and it would sit shut until the rent
            // closed it with free agents walking past. A search that shallow is the planner's
            // limit and not the promotion's, so where no card can be made it looks at booking
            // a roster: the first two of its shortlist, the first four, and so on.
            if (hold.ShowsInHorizon <= 0 && !signs)
                for (int size = 2; size <= shortlist.Count; size += 2)
                {
                    var group = shortlist.Take(size).ToList();
                    var deals = group
                        .Select(w => new Contract { WrestlerId = w.Id, Kind = ContractKind.Regular, Start = today }).ToList();
                    yield return new Candidate(
                        "added", group[0], [.. rival.Contracts, .. deals], 0m, 0m, 0, null,
                        () =>
                        {
                            rival.Contracts.RemoveAll(c => group.Any(w => w.Id == c.WrestlerId));
                            rival.Contracts.AddRange(deals);
                        },
                        group.Skip(1).ToList());
                }

            // Letting somebody go. The ones worth least for what they cost, and the dearest.
            var held = rival.Contracts
                .Select(c => (Contract: c, Wrestler: career.FindWrestler(c.WrestlerId)))
                .Where(x => x.Wrestler is not null)
                .ToList();
            var goable = signs
                ? held.Where(x => x.Contract.IsGuaranteed)
                      .OrderBy(x => x.Wrestler!.EffectiveOverness / (double)Math.Max(1m, x.Contract.AnnualGuarantee)).Take(10)
                      .Concat(held.Where(x => x.Contract.IsGuaranteed).OrderByDescending(x => x.Contract.AnnualGuarantee).Take(2))
                      .Distinct()
                : held.OrderBy(x => x.Wrestler!.EffectiveOverness).Take(4);

            foreach (var (contract, w) in goable.ToList())
            {
                decimal payout = contract.IsGuaranteed
                    ? Math.Round(contract.DailyCost * Math.Min(TalentMarket.ReleasePayoutDays, contract.DaysLeft(today) ?? 0))
                    : 0m;
                double tenure = 0.5 + Math.Max(0, today.DayNumber - contract.Start.DayNumber) / 365.0;

                // Letting somebody go is credited with the money and with nothing else
                // (NoBetterWithout), so the most it can be worth is known before forecasting
                // it: the pay it stops, to a promotion that counts money, and the way back to
                // safety, to one that is short of it. A promotion in no trouble does not
                // forecast the release of people whose pay it would not notice.
                double? atMost = null;
                if (contract.IsGuaranteed)
                {
                    int paidFor = Math.Min(sees.HorizonDays, contract.DaysLeft(today) ?? sees.HorizonDays);
                    double saved = (double)Math.Max(0m, contract.DailyCost * paidFor - payout);
                    double revenue = Math.Max(1.0, (double)hold.RevenuePerShow * hold.ShowsInHorizon);
                    double danger = sees.RiskAversion * Math.Pow(
                        Math.Max(0, (sees.RunwayFloorDays - hold.RunwayDays) / (double)Math.Max(1, sees.RunwayFloorDays)), 2);
                    atMost = wants.Wealth * saved / revenue + danger;
                }

                yield return new Candidate(
                    contract.IsGuaranteed ? "released" : "dropped", w!, rival.Contracts.Where(c => c != contract).ToList(),
                    0m, payout, tenure, null,
                    AtMost: atMost,
                    Apply: () =>
                    {
                        rival.Books.Cash -= payout;
                        rival.Contracts.Remove(contract);
                        Vacate(career, rival, w!, contract.IsGuaranteed ? "Released" : "No longer booked");
                        if (contract.IsGuaranteed)
                            career.NonCompetes.Add(new NonCompete
                            {
                                WrestlerId = w!.Id, Until = today.AddDays(TalentMarket.ReleasePayoutDays), From = rival.Id
                            });
                    });
            }

            // The player's people whose deals are running out (doc 10 §2.6). It bids what they
            // are worth to it — part of the way from the asking price to the most it would pay.
            if (signs)
                foreach (var theirs in career.ExpiringWithin(TalentMarket.ExpiryWarningDays).Where(c => c.IsGuaranteed).ToList())
                {
                    var w = career.FindWrestler(theirs.WrestlerId);
                    if (w is null || career.Bids.Any(b => b.WrestlerId == w.Id && b.Bidder == rival.Id)) continue;

                    decimal ask = career.GuaranteeAsk(w, 3);
                    decimal most = Reservation(career, rival, w, hold, wants, sees);
                    if (most < ask) continue;

                    decimal bid = Math.Round((ask + 0.5m * (most - ask)) / 1_000m) * 1_000m;
                    var contract = new Contract
                    {
                        WrestlerId = w.Id, Kind = ContractKind.Guaranteed, AnnualGuarantee = bid,
                        Start = today, End = today.AddYears(3)
                    };
                    yield return new Candidate(
                        "bid for", w, [.. rival.Contracts, contract], bid, 0m, 0, new HashSet<string> { w.Id },
                        () => career.Bids.Add(new Bid { WrestlerId = w.Id, Bidder = rival.Id, Annual = bid, Years = 3, Made = today }));
                }
        }

        /// <summary>
        /// The most a rival would pay a year for somebody: the guarantee at which signing them
        /// stops being better than not. Zero when they are not worth their asking price to it.
        ///
        /// This is what replaced a fixed bid premium. A promotion that values somebody more
        /// pays more, and how much it values them comes from what it wants: one that barely
        /// counts money will pay almost anything for a name it wants.
        /// </summary>
        public static decimal Reservation(
            Career career, RivalPromotion rival, Wrestler w, Outlook hold, Objective wants, Disposition sees)
        {
            var today = career.CurrentDate;
            decimal ask = career.GuaranteeAsk(w, 3);
            var contract = new Contract
            {
                WrestlerId = w.Id, Kind = ContractKind.Guaranteed, AnnualGuarantee = ask,
                Start = today, End = today.AddYears(3)
            };
            var roster = rival.Contracts.Where(c => c.WrestlerId != w.Id).Append(contract).ToList();
            var after = Forecast(career, rival, roster, sees, 0m, new HashSet<string> { w.Id });
            var score = ScoreOf(hold, after, wants, sees, rival.Promotion.Tier);
            if (score.Total <= 0) return 0m;

            // Each extra dollar a year costs it this much, through the money it would have left.
            double revenue = Math.Max(1.0, (double)hold.RevenuePerShow * hold.ShowsInHorizon);
            double perDollar = wants.Wealth * (sees.HorizonDays / 365.0) / revenue;
            decimal ceiling = ask * 3m;
            if (perDollar <= 1e-12) return ceiling;

            decimal most = ask + (decimal)Math.Min(score.Total / perDollar, (double)(ceiling - ask));
            return Math.Round(Math.Min(most, ceiling) / 1_000m) * 1_000m;
        }

        /// <summary>
        /// A rival's deals as they run out: kept if keeping them is worth their price to it,
        /// lost to the player if the player has offered more than it would pay, let go otherwise.
        /// </summary>
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

                var wants = ObjectiveOf(rival);
                var sees = DispositionOf(rival);
                var without = Forecast(career, rival, rival.Contracts, sees);
                decimal ask = career.GuaranteeAsk(w, 3);
                decimal most = Reservation(career, rival, w, without, wants, sees);
                var mine = career.PlayerBid(w);

                if (mine is not null && mine.Annual > most)
                {
                    career.Contracts.Add(new Contract
                    {
                        WrestlerId = w.Id, Kind = ContractKind.Guaranteed, AnnualGuarantee = mine.Annual,
                        Start = today, End = today.AddYears(mine.Years)
                    });
                    Vacate(career, rival, w, $"Left for {career.Promotion.Name}");
                    career.Log(w, $"joined from {rival.Name} at ${mine.Annual:N0} a year");
                    career.AddNews($"{w.RingName} has left {rival.Name} for {career.Promotion.Name}.", aboutYou: true);
                }
                else if (most >= ask)
                {
                    // It matches the player's offer where there is one; it would have paid more.
                    decimal keep = mine is null ? ask : Math.Max(ask, mine.Annual);
                    rival.Contracts.Add(new Contract
                    {
                        WrestlerId = w.Id, Kind = ContractKind.Guaranteed, AnnualGuarantee = keep,
                        Start = today, End = today.AddYears(3)
                    });
                    if (mine is not null)
                    {
                        career.Log(w, $"stayed with {rival.Name} at ${keep:N0} a year — it matched your offer");
                        career.AddNews($"{w.RingName} re-signed with {rival.Name}, turning down {career.Promotion.Name}.", aboutYou: true);
                    }
                }
                else
                {
                    Vacate(career, rival, w, "Contract ran out");
                    career.AddNews($"{w.RingName}'s deal with {rival.Name} ran out — not worth the price to it. A free agent.");
                }
                career.Bids.RemoveAll(b => b.WrestlerId == w.Id);
            }
        }

        /// <summary>
        /// An exclusive deal ends whatever by-the-night arrangements they had elsewhere. Not a
        /// decision anybody makes: nobody can book them any more, and a list that still had
        /// them on it would be a roster that is not one.
        /// </summary>
        private static void EndArrangements(Career career, RivalPromotion holder, Wrestler w)
        {
            foreach (var other in career.Rivals.Where(r => r != holder))
                if (other.Contracts.RemoveAll(c => c.WrestlerId == w.Id && !c.IsGuaranteed) > 0)
                    Vacate(career, other, w, $"Signed with {holder.Name}");

            if (career.Contracts.RemoveAll(c => c.WrestlerId == w.Id && !c.IsGuaranteed) > 0)
            {
                career.VacateTitles(w, $"Signed with {holder.Name}");
                career.Log(w, $"signed exclusively with {holder.Name} — no longer available to you");
                career.AddNews($"{w.RingName}, one of your regulars, has signed exclusively with {holder.Name}.", aboutYou: true);
            }
        }

        // ── Saying why ───────────────────────────────────────────────────────

        /// <summary>The largest reason for a move, in words.</summary>
        public static string ReasonFor(string move, Score score, Outlook hold) => score.Dominant switch
        {
            "survival"   => "it is running out of money",
            "wealth"     => move is "released" or "dropped" ? "they cost more than they bring in" : "it expects them to pay for themselves",
            "franchise"  => $"it expects a bigger audience into a television renewal {Math.Max(1, (int)Math.Round(hold.YearsToRenewal * 12))} months out",
            "stars"      => "it wants the name on its cards",
            "quality"    => "it makes for better shows",
            "reputation" => "for its name for good shows",
            "favour"     => "its owner wants them on top",
            _            => "it judged it worth doing"
        };

        /// <summary>What a promotion is trying to get, most wanted first, in words.</summary>
        public static IReadOnlyList<string> WantsInWords(Objective wants) =>
            new (double Weight, string Words)[]
            {
                (wants.Wealth,     "to make money"),
                (wants.Franchise,  "a bigger television deal"),
                (wants.Stars,      "names on its cards"),
                (wants.Quality,    "good shows"),
                (wants.Reputation, "a name for good shows"),
                (wants.Favour,     "its owner's people on top"),
                (wants.Stability,  "to keep the people it has")
            }
            .Where(x => x.Weight >= 0.3)
            .OrderByDescending(x => x.Weight)
            .Select(x => x.Words)
            .ToList();

        /// <summary>How it sees and carries risk, in words; empty for an ordinary, sober owner.</summary>
        public static string TemperamentInWords(Disposition sees) =>
            string.Join(" ", new[]
            {
                sees.Optimism >= 1.1 ? "Expects its shows to take more than they do." : sees.Optimism <= 0.95 ? "Counts on less than it takes." : "",
                sees.HorizonDays <= 180 ? $"Looks about {Math.Max(1, sees.HorizonDays / 30)} months ahead." : "",
                sees.RunwayFloorDays <= 60 ? "Does not worry about money until it has nearly gone." : sees.RunwayFloorDays >= 365 ? "Wants a year's money in hand." : ""
            }.Where(x => x.Length > 0));

        /// <summary>What a promotion expects of itself, in a line.</summary>
        public static string Believes(Outlook o) =>
            $"expects a {o.Fill:P0} house" +
            (o.Viewers > 0 ? $", {o.Viewers:N0} watching" : "") +
            (o.RunwayDays >= 9999 ? ", and to make money" : $", and about {Math.Max(1, o.RunwayDays / 30)} months of money at this rate");

        private static void Remember(
            Career career, RivalPromotion rival, string move, Wrestler who, decimal annual,
            Score score, Outlook hold, IEnumerable<string> rejected)
        {
            string reason = ReasonFor(move, score, hold);

            rival.Decisions.Add(new RivalDecision
            {
                Date = career.CurrentDate, Move = move, WrestlerId = who.Id, Annual = annual,
                Reason = reason, Score = Math.Round(score.Total, 4),
                Terms = score.Terms.Where(t => Math.Abs(t.Value) >= 0.0005)
                                   .ToDictionary(t => t.Key, t => Math.Round(t.Value, 4)),
                Believed = Believes(hold),
                Rejected = rejected.ToList()
            });
            if (rival.Decisions.Count > 20) rival.Decisions.RemoveAt(0);

            // The industry hears about contracts, not about who an indie stopped booking.
            switch (move)
            {
                case "signed":
                    career.AddNews($"{rival.Name} signed {who.RingName} for three years — {reason}.");
                    break;
                case "released":
                    career.AddNews($"{rival.Name} released {who.RingName} — {reason}. Free in ninety days.");
                    break;
                case "bid for":
                    var deal = career.ContractOf(who);
                    career.AddNews(
                        $"{rival.Name} has offered {who.RingName} ${annual:N0} a year to leave when the deal ends" +
                        (deal?.End is { } end ? $" on {end:d MMM}" : "") + $" — {reason}.", aboutYou: true);
                    break;
            }
        }
    }
}
