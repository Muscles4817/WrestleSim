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

                if (!rival.Frozen) SettleContracts(career, rival);

                // Its audience and its belts live on the same clock as the player's.
                AudienceModel.Decay(rival.Audience);
                foreach (var title in rival.Titles.Active) TitleEconomy.ApplyDailyDrift(title, today);

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
                if (!rival.Frozen && (today.DayNumber + Math.Abs(StableSeed.From(rival.Id))) % 7 == 0)
                    Business(career, rival);

                if (rival.Books.Cash < rival.Books.OpeningCash * FoldAt)
                    Fold(career, rival);
            }
        }

        // ── The night ────────────────────────────────────────────────────────

        /// <summary>
        /// A rival's show. **Its card is a real card** — the same item types the player books:
        /// singles, tag and three-way matches, title matches, promos — sold through the same box
        /// office, in front of the rival's own audience, with its own match-up memory and its own
        /// belts. "Abstract" means one thing only: the matches are not worked beat by beat. An
        /// item's rating comes from who is in it and what kind of item it is, not from
        /// <see cref="MatchEngine"/>.
        ///
        /// It used to be five singles pairings and nothing else. That was a shortcut taken under
        /// the word "abstract", and it left a rival with no way to vary a show, defend a title,
        /// or pay for a thin roster.
        /// </summary>
        private static void RunNight(Career career, RivalPromotion rival, DateOnly today)
        {
            // With television, the big night comes round about once a month (doc 29 §1: 12–16
            // major events a year at the top), not every fourth show — at a show every three
            // days that was a premium event every twelve, and a title defended thirty times a
            // year changed hands so often its prestige went to nothing.
            bool bigNightDue = rival.Nights.Count >= 3
                && !rival.Nights.Any(n => n.Type == ShowType.PremiumEvent && today.DayNumber - n.Date.DayNumber < 28);
            var type = !rival.Promotion.HasTelevision || bigNightDue ? ShowType.PremiumEvent : ShowType.Television;

            var available = rival.Contracts
                .Select(c => career.FindWrestler(c.WrestlerId))
                .OfType<Wrestler>()
                .Where(w => !w.IsInjured(today)
                         && career.NonCompeteOf(w) is null
                         && !career.IsGuaranteed(w)
                         && (career.SignedTo(w) is null || career.SignedTo(w) == rival))
                .ToList();

            double Response(Wrestler w) => AudienceModel.Response(rival.Audience, w, rival.Promotion.Tier, type);

            var show = new ScheduledShow
            {
                Date = today, Type = type,
                Attendance = rival.Promotion.TypicalAttendanceFor(type),
                RuntimeMinutes = rival.Promotion.DefaultRuntimeFor(type),
                Name = rival.Name
            };
            show.Card.AddRange(BookCard(rival, available, type, today, Response));
            if (show.Card.Count == 0) return;

            // Read before anything is worked, like the player's: the house is sold on the card as
            // advertised, and how new each act's part is depends on memory the night will change.
            var novelty = AudienceModel.Novelty(rival.Audience, show.Card, rival.Feuds, today);
            var f = BoxOffice.Night(show, rival.Promotion, rival.Books.Reputation, rival.Feuds, rival.Television,
                w => rival.ContractOf(w)?.IsGuaranteed == true ? 0m : career.PerDateAsk(w), career.Interest, Response);

            var rng = new Random(StableSeed.From(rival.Id, today.DayNumber));
            var result = new ShowResult { BookedMinutes = show.BookedMinutes, BudgetMinutes = show.RuntimeMinutes };
            var winners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = new List<string>();
            double total = 0, weights = 0;

            for (int i = 0; i < show.Card.Count; i++)
            {
                var item = show.Card[i];
                double itemRating = ExpectedItemRating(rival, item, today, Response);

                if (item is BookedMatch match)
                {
                    var sides = match.Plan.Sides;

                    // The favourite — the champion, or the bigger name — wins most nights.
                    int favourite = match.Plan.TitleAtStake?.Champion is { } champ
                        ? Math.Max(0, sides.FindIndex(sd => sd.Members.Contains(champ)))
                        : sides.Select((sd, n) => (n, Top: sd.Members.Max(w => w.EffectiveOverness)))
                               .OrderByDescending(x => x.Top).First().n;
                    // A champion is harder to beat than a favourite: belts that change hands every
                    // other month are worth nothing (doc 21 §4).
                    double holds = match.Plan.TitleAtStake?.Champion is null ? 0.72 : 0.85;
                    int win = rng.NextDouble() < holds ? favourite : (favourite + 1 + rng.Next(sides.Count - 1)) % sides.Count;
                    foreach (var w in sides[win].Members) winners.Add(w.Id);

                    // The match-up is remembered, which is what makes the rematch worth less.
                    rival.Feuds.GetOrCreate(sides.Select(sd => (IReadOnlyList<Wrestler>)sd.Members).ToList())
                         .RecordMatch(today);

                    if (match.Plan.TitleAtStake is { } title)
                    {
                        var lost = sides[win == 0 ? 1 : 0];
                        result.TitleUpdates.Add(TitleEconomy.ResolveTitleMatch(
                            title, sides[win].Members, lost.Members, sides[win].Members[0], lost.Members[0],
                            FinishWeight.Decisive, itemRating / 20.0, today, rival.Name));
                    }
                }

                lines.Add(Describe(item));
                double weight = item.DurationMinutes * (i == show.Card.Count - 1 ? 1.5 : 1.0);
                total += itemRating * weight;
                weights += weight;
            }

            double rating = weights <= 0 ? 0 : total / weights;
            rating += (rng.NextDouble() * 2 - 1) * 6;      // a night's variance

            // What the night did to this audience's patience, and what their patience did to it.
            var crowd = AudienceModel.Observe(rival.Audience, show, novelty, result, winners);
            rating = Math.Clamp(rating * (1 - crowd.SamenessPenalty) * (1 - crowd.UnderrunPenalty), 5, 95);

            foreach (var t in result.TitleUpdates.Where(t => t.Event is not (TitleEvent.Retained or TitleEvent.NonTitleLoss)))
                career.AddNews($"{t.Champion?.RingName ?? "Nobody"} won the {t.Title.Name}.");

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
            foreach (var w in show.Card.SelectMany(i => i.Wrestlers).Distinct()) w.LastAppearance = today;

            rival.Nights.Add(new RivalNight
            {
                Date = today, Type = type, Rating = Math.Round(rating, 1),
                Attendance = f.Attendance, Capacity = f.Capacity, Viewers = viewers, Net = f.Net,
                Headline = f.Draws.FirstOrDefault()?.Label ?? "",
                Card = lines, Filled = crowd.Filled
            });
            if (rival.Nights.Count > 12) rival.Nights.RemoveAt(0);
        }

        private static string Describe(ICardItem item) => item switch
        {
            BookedMatch { Plan.TitleAtStake: { } title } m => $"{m.Name} — for the {title.Name}",
            BookedMatch m when m.Plan.Sides.Count > 2      => $"{m.Name} — three-way",
            BookedMatch m when m.Plan.IsTagMatch           => $"{m.Name} — tag",
            BookedMatch m                                  => m.Name,
            _                                              => item.Name
        };

        private static BookedMatch AbstractMatch(Title? title, params IEnumerable<Wrestler>[] sides) => new()
        {
            Plan = new Models.MatchPlan.MatchPlan
            {
                Sides = sides.Select(s => new Models.MatchPlan.MatchSide { Members = s.ToList() }).ToList(),
                TitleAtStake = title
            },
            StructureName = "Abstract"
        };

        /// <summary>
        /// Builds the card: titles first, then something that is not a singles match, then
        /// talking, then singles until the slot is filled — each pairing chosen by what it would
        /// draw *as this audience now sees those people and that match-up*, so a rival with
        /// options rotates away from a stale match and one without cannot.
        ///
        /// The make-up is still a fixed recipe. Choosing it is the planner's job (plan §8); what
        /// this gives it is the vocabulary.
        /// </summary>
        public static List<ICardItem> BookCard(
            RivalPromotion rival, IReadOnlyList<Wrestler> available, ShowType type, DateOnly today,
            Func<Wrestler, double> response)
        {
            double target = rival.Promotion.DefaultRuntimeFor(type) * AudienceModel.FullCard;
            int index = rival.Nights.Count;
            var used = new HashSet<Wrestler>();
            var talking = new List<ICardItem>();
            var matches = new List<BookedMatch>();
            int minutes = 0;

            double Pull(ICardItem i) => BoxOffice.DrawOf(i, rival.Feuds, today, response).Pull;
            IEnumerable<Wrestler> Free(Division d) => available
                .Where(w => w.Division == d && !used.Contains(w))
                .OrderByDescending(w => w.EffectiveOverness * response(w))
                .ThenBy(w => w.Id, StringComparer.Ordinal);
            void Book(BookedMatch m)
            {
                matches.Add(m);
                minutes += m.DurationMinutes;
                foreach (var w in m.Wrestlers) used.Add(w);
            }

            // 1. The belts. A vacant one is filled on any night; a champion defends on the big one,
            //    against whoever in the division is the freshest draw. A thin division runs out.
            foreach (var title in rival.Titles.Active.Where(t => t.SideSize == 1).OrderByDescending(t => t.Prestige))
            {
                var division = Free(title.Division).ToList();
                var champion = title.Champion;
                if (champion is not null && (type != ShowType.PremiumEvent || !division.Contains(champion))) continue;

                var holder = champion ?? division.FirstOrDefault();
                if (holder is null) continue;

                var best = division.Where(w => w != holder).Take(8)
                    .Select(c => AbstractMatch(title, [holder], [c]))
                    .Select(m => (Match: m, Pull: Pull(m)))
                    .OrderByDescending(x => x.Pull).ThenBy(x => x.Match.Name, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (best.Match is not null) Book(best.Match);
            }

            // 2. Not another singles match: a tag one week, a three-way now and then — from the
            //    middle of the card, leaving the top two of a division for their own matches.
            var deepest = available.Select(w => w.Division).Distinct()
                .OrderByDescending(d => Free(d).Count()).FirstOrDefault();
            var middle = Free(deepest).Skip(2).ToList();
            if (index % 2 == 0 && middle.Count >= 4)
                Book(AbstractMatch(null, [middle[0], middle[3]], [middle[1], middle[2]]));
            else if (index % 3 == 0 && middle.Count >= 3)
                Book(AbstractMatch(null, [middle[0]], [middle[1]], [middle[2]]));

            // 3. Talking, on television: somebody the audience came to see, not wrestling tonight.
            if (type == ShowType.Television)
            {
                var names = available.Where(w => !used.Contains(w))
                    .OrderByDescending(w => w.EffectiveOverness * response(w))
                    .ThenBy(w => w.Id, StringComparer.Ordinal).Take(2).ToList();
                if (index % 2 == 1 && names.Count == 2)
                {
                    talking.Add(Factories.SegmentFactory.CreateConfrontation(names[0], names[1], "", ""));
                    used.Add(names[0]); used.Add(names[1]);
                }
                else if (names.Count >= 1)
                {
                    talking.Add(Factories.SegmentFactory.CreatePromo(names[0], ""));
                    used.Add(names[0]);
                }
                minutes += talking.Sum(t => t.DurationMinutes);
            }

            // 4. Singles until the slot is filled. Everybody once; then, if the roster has run out
            //    and the slot is still short, people work a second time — which is what a thin
            //    roster has to do, and what burns its match-ups.
            var worked = matches.SelectMany(m => m.Wrestlers).Concat(talking.SelectMany(t => t.Wrestlers))
                .GroupBy(w => w).ToDictionary(g => g.Key, g => g.Count());
            for (int pass = 1; pass <= 2 && minutes < target; pass++)
            {
                while (minutes < target)
                {
                    var best = available.GroupBy(w => w.Division)
                        .SelectMany(g =>
                        {
                            var pool = g.Where(w => worked.GetValueOrDefault(w) < pass)
                                .OrderByDescending(w => w.EffectiveOverness * response(w))
                                .ThenBy(w => w.Id, StringComparer.Ordinal).Take(10).ToList();
                            return pool.SelectMany((a, i) => pool.Skip(i + 1).Select(b => AbstractMatch(null, [a], [b])));
                        })
                        .Where(m => !matches.Any(x => x.Plan.Sides.Count == 2 && !x.Plan.IsTagMatch
                                                      && x.Wrestlers.All(m.Wrestlers.Contains)))
                        .Select(m => (Match: m, Pull: Pull(m)))
                        .OrderByDescending(x => x.Pull).ThenBy(x => x.Match.Name, StringComparer.Ordinal)
                        .FirstOrDefault();
                    if (best.Match is null) break;

                    Book(best.Match);
                    foreach (var w in best.Match.Wrestlers) worked[w] = worked.GetValueOrDefault(w) + 1;
                }
            }

            // Running order: talking opens, the biggest match closes.
            var card = new List<ICardItem>(talking);
            card.AddRange(matches.OrderBy(Pull).ThenBy(m => m.Name, StringComparer.Ordinal));
            return card;
        }

        /// <summary>
        /// What an item is worth on the same 0–100 scale as the player's, without working it:
        /// who is in it, how good they are, how over they are, whether the crowd has seen this
        /// match-up or these acts too often, what is on the line, and what the promotion is for.
        /// </summary>
        public static double ExpectedItemRating(
            RivalPromotion rival, ICardItem item, DateOnly on, Func<Wrestler, double>? response = null)
        {
            var people = item.Wrestlers;
            if (people.Count == 0) return 0;

            double acts = response is null ? 1.0 : people.Average(response);
            double over = people.Average(w => w.EffectiveOverness);
            double bias = Profile(rival.Motive).RatingBias;

            if (item is BookedMatch match)
            {
                double skill = people.Average(w => w.RingSkills.GetOverallSkill());
                double fresh = BoxOffice.StoryBetween(match.Plan, rival.Feuds)?.Familiarity(on) ?? 1.0;
                double rating = (22 + 6 * skill + 0.2 * over + bias) * acts * (0.75 + 0.25 * fresh);
                if (match.Plan.IsTitleMatch) rating += 3;
                return Math.Clamp(rating, 5, 95);
            }

            // Talking: charisma carries it.
            double charisma = people.Average(w => Math.Clamp(w.Charisma, 0, 5));
            return Math.Clamp((28 + 7 * charisma + 0.12 * over + bias / 2) * acts, 5, 95);
        }

        /// <summary>Takes anything they hold off them, because they are leaving this promotion.</summary>
        private static void Vacate(Career career, RivalPromotion rival, Wrestler w, string reason)
        {
            foreach (var title in rival.Titles.HeldBy(w))
                if (title.CurrentReign is { } reign)
                {
                    reign.Lost = career.CurrentDate;
                    reign.Vacated = true;
                    reign.LostAt = reason;
                }
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
                    Vacate(career, rival, w, $"Left for {career.Promotion.Name}");
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
                    Vacate(career, rival, w, "Contract ran out");
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
            foreach (var gone in rival.Contracts.Where(c => !c.IsGuaranteed
                         && career.FindWrestler(c.WrestlerId) is { } w
                         && (career.IsGuaranteed(w) || (career.SignedTo(w) is { } other && other != rival))).ToList())
            {
                rival.Contracts.Remove(gone);
                if (career.FindWrestler(gone.WrestlerId) is { } w) Vacate(career, rival, w, "Signed elsewhere");
            }

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
                Vacate(career, rival, worst.w, "Released");
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
