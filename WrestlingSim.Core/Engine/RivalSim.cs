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
            _ticking = true;
            _holdings = null;
            try { TickEveryone(career); }
            finally { _ticking = false; _holdings = null; }
        }

        private static void TickEveryone(Career career)
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
                    Plan(career, rival);

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
            var type = NextShowType(rival.Promotion, today, rival.Nights.Select(n => (n.Date, n.Type)));

            var available = rival.Contracts
                .Select(c => career.FindWrestler(c.WrestlerId))
                .OfType<Wrestler>()
                .Where(w => !w.IsInjured(today)
                         && career.NonCompeteOf(w) is null
                         && !career.IsGuaranteed(w)
                         && (HolderOf(career, w) is not { } holder || holder == rival))
                .ToList();

            double Response(Wrestler w) => AudienceModel.Response(rival.Audience, w, rival.Promotion.Tier, type);

            var show = new ScheduledShow
            {
                Date = today, Type = type,
                Attendance = rival.Promotion.TypicalAttendanceFor(type),
                RuntimeMinutes = rival.Promotion.DefaultRuntimeFor(type),
                Name = rival.Name
            };
            // How fresh a match-up is with this audience: what its book remembers of it.
            double Freshness(BookedMatch m) =>
                BoxOffice.StoryBetween(m.Plan, rival.Feuds)?.Familiarity(today) ?? 1.0;

            show.Card.AddRange(BookCard(rival, available, type, today, rival.Nights.Count, Response, Freshness));
            if (show.Card.Count == 0) return;

            // Read before anything is worked, like the player's: the house is sold on the card as
            // advertised, and how new each act's part is depends on memory the night will change.
            var novelty = AudienceModel.Novelty(rival.Audience, show.Card, rival.Feuds, today);
            var f = BoxOffice.Night(show, rival.Promotion, rival.Books.Reputation, rival.Feuds, rival.Television,
                w => rival.ContractOf(w)?.IsGuaranteed == true ? 0m : career.PerDateAsk(w), career.Interest,
                Response, Freshness);

            var rng = new Random(StableSeed.From(rival.Id, today.DayNumber));
            var result = new ShowResult { BookedMinutes = show.BookedMinutes, BudgetMinutes = show.RuntimeMinutes };
            var winners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = new List<string>();
            double total = 0, weights = 0;

            for (int i = 0; i < show.Card.Count; i++)
            {
                var item = show.Card[i];
                double itemRating = ExpectedItemRating(rival, item, Response, Freshness);

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
        /// <remarks>
        /// Takes the audience's response and the match-ups' freshness as functions, not as the
        /// rival's own books, so that the forecast (<see cref="Forecast"/>) can book the shows
        /// that have not happened yet with this very code. One booker; if the forecast booked
        /// differently it would be planning for a world that does not exist.
        /// </remarks>
        /// <summary>
        /// How over somebody would have had to be for the booker to have so much as looked at
        /// them when it built a card — by division, and for the talking, which is anybody.
        ///
        /// Every choice <see cref="BookCard"/> makes is from the top few of some list, ranked
        /// by overness as this audience now feels it. If a list was full and its last name
        /// ranked above somebody, adding that somebody would not have changed the list, and so
        /// not the card. This records the lowest such last name across every list the card was
        /// built from, and marks a division <see cref="Open"/> where some list was short — where
        /// anybody at all would have got in.
        ///
        /// It is what lets a promotion planning ahead know that a free agent could not make its
        /// cards without booking a month of shows to find out. It decides nothing; it reports.
        /// </summary>
        public sealed class CutLine
        {
            public ShowType Type { get; init; }
            public HashSet<Division> Present { get; } = new();
            public HashSet<Division> Open { get; } = new();
            public Dictionary<Division, double> Lowest { get; } = new();
            public bool AnybodyOpen { get; private set; }
            public double AnybodyLowest { get; private set; } = double.PositiveInfinity;

            internal void See(Division division, int have, int full, double last)
            {
                if (have < full) { Open.Add(division); return; }
                Lowest[division] = Math.Min(Lowest.GetValueOrDefault(division, double.PositiveInfinity), last);
            }

            internal void SeeAnybody(int have, int full, double last)
            {
                if (have < full) AnybodyOpen = true;
                else AnybodyLowest = Math.Min(AnybodyLowest, last);
            }

            /// <summary>Whether somebody this over, in this division, could have been on the card.</summary>
            public bool CouldHaveBooked(Division division, double felt) =>
                !Present.Contains(division) || Open.Contains(division) || AnybodyOpen
                || felt >= AnybodyLowest
                || (Lowest.TryGetValue(division, out double lowest) && felt >= lowest);
        }

        public static List<ICardItem> BookCard(
            RivalPromotion rival, IReadOnlyList<Wrestler> available, ShowType type, DateOnly today, int index,
            Func<Wrestler, double> response, Func<BookedMatch, double> freshness, CutLine? cut = null)
        {
            double target = rival.Promotion.DefaultRuntimeFor(type) * AudienceModel.FullCard;
            var used = new HashSet<Wrestler>();
            var talking = new List<ICardItem>();
            var matches = new List<BookedMatch>();
            int minutes = 0;

            // Asked many times for the same people on one night; the answers do not change within it.
            var responses = new Dictionary<Wrestler, double>();
            var asked = response;
            response = w => responses.TryGetValue(w, out double r) ? r : responses[w] = asked(w);
            double Pull(ICardItem i) => i is BookedMatch m
                ? BoxOffice.PullOf(m, null, today, response, freshness)
                : BoxOffice.DrawOf(i, null, today, response, freshness).Pull;
            IEnumerable<Wrestler> Free(Division d) => available
                .Where(w => w.Division == d && !used.Contains(w))
                .OrderByDescending(w => w.EffectiveOverness * response(w))
                .ThenBy(w => w.Id, StringComparer.Ordinal);
            double Key(Wrestler w) => w.EffectiveOverness * response(w);
            if (cut is not null) foreach (var w in available) cut.Present.Add(w.Division);

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
                if (holder is null) { cut?.Open.Add(title.Division); continue; }

                var challengers = division.Where(w => w != holder).Take(8).ToList();
                cut?.See(title.Division, challengers.Count, 8, challengers.Count == 0 ? 0 : Key(challengers[^1]));

                var best = challengers
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
            var depth = Free(deepest).ToList();
            var middle = depth.Skip(2).ToList();
            if (cut is not null)
            {
                // One more person could make another division the deepest, or get into the six
                // this match is drawn from.
                foreach (var d in cut.Present)
                    if (d != deepest && Free(d).Count() + 1 >= depth.Count) cut.Open.Add(d);
                cut.See(deepest, depth.Count, 6, depth.Count < 6 ? 0 : Key(depth[5]));
            }
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
                cut?.SeeAnybody(names.Count, 2, names.Count == 0 ? 0 : Key(names[^1]));
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
            var divisions = available.GroupBy(w => w.Division).Select(g => g.ToList()).ToList();
            var pairs = new Dictionary<(Wrestler, Wrestler), (BookedMatch Match, double Pull)>();
            var booked = matches.Where(x => x.Plan.Sides.Count == 2 && !x.Plan.IsTagMatch && x.Wrestlers.Count == 2)
                .Select(x => (x.Wrestlers[0], x.Wrestlers[1])).ToHashSet();
            for (int pass = 1; pass <= 2 && minutes < target; pass++)
            {
                while (minutes < target)
                {
                    (BookedMatch? Match, double Pull) best = (null, 0);
                    foreach (var g in divisions)
                    {
                        var pool = g.Where(w => worked.GetValueOrDefault(w) < pass)
                            .OrderByDescending(w => w.EffectiveOverness * response(w))
                            .ThenBy(w => w.Id, StringComparer.Ordinal).Take(10).ToList();
                        cut?.See(g[0].Division, pool.Count, 10, pool.Count == 0 ? 0 : Key(pool[^1]));
                        for (int a = 0; a < pool.Count; a++)
                            for (int b = a + 1; b < pool.Count; b++)
                            {
                                if (!pairs.TryGetValue((pool[a], pool[b]), out var pair))
                                {
                                    var made = AbstractMatch(null, [pool[a]], [pool[b]]);
                                    pair = pairs[(pool[a], pool[b])] = (made, Pull(made));
                                }
                                if (booked.Contains((pool[a], pool[b])) || booked.Contains((pool[b], pool[a]))) continue;
                                if (best.Match is null || pair.Pull > best.Pull
                                    || (pair.Pull == best.Pull && string.CompareOrdinal(pair.Match.Name, best.Match.Name) < 0))
                                    best = pair;
                            }
                    }
                    if (best.Match is null) break;

                    Book(best.Match);
                    booked.Add((best.Match.Wrestlers[0], best.Match.Wrestlers[1]));
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
            RivalPromotion rival, ICardItem item,
            Func<Wrestler, double>? response = null, Func<BookedMatch, double>? freshness = null)
        {
            var people = item.Wrestlers;
            if (people.Count == 0) return 0;

            double acts = response is null ? 1.0 : people.Average(response);
            double over = people.Average(w => w.EffectiveOverness);
            double bias = Profile(rival.Motive).RatingBias;

            if (item is BookedMatch match)
            {
                double skill = people.Average(w => w.RingSkills.GetOverallSkill());
                double fresh = freshness?.Invoke(match) ?? 1.0;
                double rating = (22 + 6 * skill + 0.2 * over + bias) * acts * (0.75 + 0.25 * fresh);
                if (match.Plan.IsTitleMatch) rating += 3;
                return Math.Clamp(rating, 5, 95);
            }

            // Talking: charisma carries it.
            double charisma = people.Average(w => Math.Clamp(w.Charisma, 0, 5));
            return Math.Clamp((28 + 7 * charisma + 0.12 * over + bias / 2) * acts, 5, 95);
        }

        /// <summary>Takes anything they hold off them, because they are leaving this promotion.</summary>
        internal static void Vacate(Career career, RivalPromotion rival, Wrestler w, string reason)
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
        // Who a rival signs, keeps, releases and bids for is decided in RivalPlanner.cs.

        // ── Who holds whom ───────────────────────────────────────────────────

        /// <summary>
        /// Who holds each wrestler exclusively, and who is free — worked out once and reused
        /// for as long as nobody's contracts change.
        ///
        /// Both questions were answered by walking every contract in the world for every
        /// wrestler asked about, every time. With four rivals nobody noticed. With ninety it
        /// was a third of a week's cost before any forecast ran.
        ///
        /// Kept only while the world's own clock is running (<see cref="Tick"/>), where every
        /// place that changes a contract says so (<see cref="ContractsChanged"/>). Asked from
        /// anywhere else — a screen, a test, the player signing somebody — it is worked out
        /// fresh each time.
        /// </summary>
        private sealed class Holdings
        {
            public required Dictionary<string, RivalPromotion> Holder { get; init; }
            public IReadOnlyList<Wrestler>? Free { get; set; }
        }

        [ThreadStatic] private static Holdings? _holdings;
        [ThreadStatic] private static bool _ticking;

        internal static void ContractsChanged() => _holdings = null;

        private static Holdings HoldingsOf(Career career)
        {
            if (_ticking && _holdings is { } kept) return kept;

            var holder = new Dictionary<string, RivalPromotion>();
            foreach (var rival in career.Rivals)
                if (rival.IsActive)
                    foreach (var c in rival.Contracts)
                        if (c.IsGuaranteed) holder.TryAdd(c.WrestlerId, rival);

            var made = new Holdings { Holder = holder };
            if (_ticking) _holdings = made;
            return made;
        }

        /// <summary>The rival that holds them exclusively, or null. <see cref="Career.SignedTo"/>, indexed.</summary>
        internal static RivalPromotion? HolderOf(Career career, Wrestler w) =>
            HoldingsOf(career).Holder.GetValueOrDefault(w.Id);

        /// <summary>Anybody nobody holds exclusively, and who is free to work. Most over first.</summary>
        internal static IReadOnlyList<Wrestler> FreeAgents(Career career)
        {
            var holdings = HoldingsOf(career);
            if (holdings.Free is { } known) return known;

            var mine = career.Contracts.Where(c => c.IsGuaranteed).Select(c => c.WrestlerId).ToHashSet();
            var barred = career.NonCompetes.Where(n => n.Until > career.CurrentDate).Select(n => n.WrestlerId).ToHashSet();
            var free = career.Roster
                .Where(w => !mine.Contains(w.Id)
                         && !holdings.Holder.ContainsKey(w.Id)
                         && !barred.Contains(w.Id)
                         && career.PlayerBid(w) is null)
                .OrderByDescending(w => w.EffectiveOverness)
                .ThenBy(w => w.Id, StringComparer.Ordinal)
                .ToList();
            holdings.Free = free;
            return free;
        }

        /// <summary>
        /// Out of money. Everybody it held is a free agent at once — a closed promotion has no
        /// non-compete to enforce — and the world has one employer fewer.
        /// </summary>
        private static void Fold(Career career, RivalPromotion rival)
        {
            int freed = rival.Contracts.Count(c => c.IsGuaranteed);
            rival.Folded = career.CurrentDate;
            rival.Contracts.Clear();
            ContractsChanged();
            rival.Television = null;
            career.Bids.RemoveAll(b => b.Bidder == rival.Id);
            career.AddNews($"{rival.Name} has closed. {freed} wrestler{(freed == 1 ? " is" : "s are")} free agents.", aboutYou: true);
        }
    }
}
