using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The planner: docs/rival-ai-plan.md §4–6, §11.
    ///
    /// These assert mechanisms and comparisons — the same promotion wanting different things,
    /// the same promotion with and without money — and never what becomes of a named promotion.
    /// What the default world does over five years is printed by
    /// <see cref="WhatTheWorldDoesWithIt_IsMeasuredNotForced"/> and asserted by nothing.
    /// </summary>
    public class PlannerTests(ITestOutputHelper output)
    {
        private static Career NewWorld(PromotionTier tier) => RivalTests.NewWorld(tier);

        private static RivalPromotion RivalAt(Career career, PromotionTier tier) =>
            career.Rivals.First(r => r.Promotion.Tier == tier);

        private static readonly Objective Nothing = new(0, 0, 0, 0, 0, 0, 0);
        private static readonly Disposition Sober = new(365, 1.0, 180, 3);

        private static Outlook AnOutlook() => new(
            CardPull: 0.6, Rating: 50, Fill: 0.7, Viewers: 800_000, RevenuePerShow: 1_000_000m, NetPerShow: 400_000m,
            DailyFixed: 50_000m, ShowsInHorizon: 52, CashAtHorizon: 10_000_000m, Equity: 0m,
            ReputationAtHorizon: 50, RenewalMultiple: 1.1, YearsToRenewal: 1, RunwayDays: 9999, FavourShare: 0, Filled: 0.5);

        // ── Scoring ──────────────────────────────────────────────────────────

        [Fact]
        public void DoingNothingScoresNothing()
        {
            var o = AnOutlook();
            foreach (var motive in Enum.GetValues<FounderMotive>())
                Assert.Equal(0, World.ScoreOf(o, o, World.ObjectiveFor(motive), World.DispositionFor(motive), PromotionTier.National).Total, 12);
        }

        /// <summary>Each weight counts its own thing and nothing else, in proportion.</summary>
        [Fact]
        public void EachThingAPromotionWantsCountsOnlyItsOwnThing()
        {
            var hold = AnOutlook();
            var cases = new (string Term, Objective Wants, Outlook After)[]
            {
                ("wealth",     Nothing with { Wealth = 1 },     hold with { CashAtHorizon = hold.CashAtHorizon + 5_200_000m }),
                ("wealth",     Nothing with { Wealth = 1 },     hold with { Equity = 5_200_000m }),
                ("franchise",  Nothing with { Franchise = 1 },  hold with { RenewalMultiple = 1.3 }),
                ("stars",      Nothing with { Stars = 1 },      hold with { CardPull = 0.7 }),
                ("quality",    Nothing with { Quality = 1 },    hold with { Rating = 60 }),
                ("reputation", Nothing with { Reputation = 1 }, hold with { ReputationAtHorizon = 55 }),
                ("favour",     Nothing with { Favour = 1 },     hold with { FavourShare = 0.5 }),
            };

            foreach (var (term, wants, after) in cases)
            {
                var once = World.ScoreOf(hold, after, wants, Sober, PromotionTier.National);
                Assert.True(once.Total > 0, term);
                Assert.Equal(once.Total, once.Terms[term], 12);
                Assert.Equal(term, once.Dominant);

                // Nothing wanted, nothing counted; twice as wanted, twice as much.
                Assert.Equal(0, World.ScoreOf(hold, after, Nothing, Sober, PromotionTier.National).Total, 12);
                var twice = wants with
                {
                    Wealth = wants.Wealth * 2, Franchise = wants.Franchise * 2, Stars = wants.Stars * 2, Quality = wants.Quality * 2,
                    Reputation = wants.Reputation * 2, Favour = wants.Favour * 2
                };
                Assert.Equal(once.Total * 2, World.ScoreOf(hold, after, twice, Sober, PromotionTier.National).Total, 12);
            }

            // Wealth is profit and what the contracts are worth — a tenth of a year's revenue here.
            Assert.Equal(0.1, World.ScoreOf(hold, cases[0].After, cases[0].Wants, Sober, PromotionTier.National).Total, 6);
        }

        /// <summary>The same audience gain is worth more the closer the renewal it feeds.</summary>
        [Fact]
        public void ARenewalNextYearCountsForMoreThanOneInFour()
        {
            var wants = Nothing with { Franchise = 1 };
            var soon = AnOutlook() with { YearsToRenewal = 1 };
            var far = AnOutlook() with { YearsToRenewal = 4 };

            double near = World.ScoreOf(soon, soon with { RenewalMultiple = 1.3 }, wants, Sober, PromotionTier.National).Total;
            double distant = World.ScoreOf(far, far with { RenewalMultiple = 1.3 }, wants, Sober, PromotionTier.National).Total;
            Assert.True(near > distant * 4);
        }

        /// <summary>
        /// Running out of money is feared whatever a promotion wants, in proportion to how much
        /// this one fears it, and not at all while it has more money-time than it asks for.
        /// </summary>
        [Fact]
        public void RunningOutOfMoneyIsFearedByTemperament_NotByWhatItWants()
        {
            var hold = AnOutlook();
            var comfortable = hold with { RunwayDays = 400 };
            var tight = hold with { RunwayDays = 90 };
            var desperate = hold with { RunwayDays = 30 };

            Assert.Equal(0, World.ScoreOf(hold, comfortable, Nothing, Sober, PromotionTier.National).Total, 12);

            double a = World.ScoreOf(hold, tight, Nothing, Sober, PromotionTier.National).Total;
            double b = World.ScoreOf(hold, desperate, Nothing, Sober, PromotionTier.National).Total;
            Assert.True(a < 0 && b < a);

            double nervous = World.ScoreOf(hold, tight, Nothing, Sober with { RiskAversion = 6 }, PromotionTier.National).Total;
            Assert.Equal(a * 2, nervous, 12);

            // And getting out of trouble is worth exactly what getting into it costs.
            Assert.Equal(-a, World.ScoreOf(tight, hold, Nothing, Sober, PromotionTier.National).Total, 12);
        }

        /// <summary>Continuity is charged for losing somebody, by how long they have been there.</summary>
        [Fact]
        public void LosingSomebodyTheAudienceIsUsedToCosts_ByHowMuchItValuesContinuity()
        {
            var o = AnOutlook();
            var wants = Nothing with { Stability = 1 };
            double newcomer = World.ScoreOf(o, o, wants, Sober, PromotionTier.National, continuityLost: 0.5).Total;
            double fixture = World.ScoreOf(o, o, wants, Sober, PromotionTier.National, continuityLost: 5.5).Total;

            Assert.True(fixture < newcomer && newcomer < 0);
            Assert.Equal(0, World.ScoreOf(o, o, Nothing, Sober, PromotionTier.National, continuityLost: 5.5).Total, 12);
        }

        [Fact]
        public void LettingSomebodyGoIsNeverCreditedWithBetterShows()
        {
            var with = AnOutlook();
            var without = with with { CardPull = 0.65, Rating = 55, ReputationAtHorizon = 52, RenewalMultiple = 1.2, CashAtHorizon = 11_000_000m };

            var read = World.NoBetterWithout(with, without);
            Assert.Equal(with.CardPull, read.CardPull);
            Assert.Equal(with.Rating, read.Rating);
            Assert.Equal(with.ReputationAtHorizon, read.ReputationAtHorizon);
            Assert.Equal(with.RenewalMultiple, read.RenewalMultiple);
            Assert.Equal(11_000_000m, read.CashAtHorizon);      // the money is real

            // Worse shows without them are still worse.
            Assert.Equal(0.5, World.NoBetterWithout(with, with with { CardPull = 0.5 }).CardPull);
        }

        // ── The forecast ─────────────────────────────────────────────────────

        /// <summary>Asking what would happen changes nothing.</summary>
        [Fact]
        public void TheForecastChangesNothing()
        {
            var career = NewWorld(PromotionTier.Local);
            for (int i = 0; i < 30; i++) career.AdvanceOneDay();
            string before = SaveSerializer.ToJson(career);

            foreach (var rival in career.Rivals)
            {
                var sees = World.DispositionOf(rival);
                var hold = World.Forecast(career, rival, rival.Contracts, sees);
                World.Forecast(career, rival, rival.Contracts.Skip(3).ToList(), sees, 50_000m);
                World.Reservation(career, rival, career.Roster[0], hold, World.ObjectiveOf(rival), sees);
            }

            Assert.Equal(before, SaveSerializer.ToJson(career));
        }

        /// <summary>
        /// The forecast books with the world's booker and prices with the world's box office,
        /// so what it expects of the next month should be what the next month does, less the
        /// night's luck. Measured on every rival with a show to run; the planner is switched
        /// off so the roster it forecast is the roster that runs.
        /// </summary>
        [Fact]
        public void TheForecastIsTheWorldMinusTheNoise()
        {
            var career = NewWorld(PromotionTier.Local);
            for (int i = 0; i < 45; i++) career.AdvanceOneDay();

            foreach (var rival in career.Rivals) rival.Frozen = true;
            var expected = career.Rivals.ToDictionary(r => r, r => World.Forecast(career, r, r.Contracts, Sober));
            var seen = career.Rivals.ToDictionary(r => r, r => r.Nights.LastOrDefault());

            var actual = career.Rivals.ToDictionary(r => r, _ => new List<RivalNight>());
            for (int day = 0; day < 120 && actual.Values.Any(n => n.Count < World.CycleShows); day++)
            {
                career.AdvanceOneDay();
                foreach (var r in career.Rivals)
                    if (r.Nights.LastOrDefault() is { } night && night != seen[r] && actual[r].Count < World.CycleShows)
                    {
                        actual[r].Add(night);
                        seen[r] = night;
                    }
            }

            foreach (var (rival, nights) in actual)
            {
                Assert.Equal(World.CycleShows, nights.Count);
                var o = expected[rival];
                double fill = nights.Average(n => n.Attendance / (double)Math.Max(1, n.Capacity));
                double rating = nights.Average(n => n.Rating);
                decimal net = nights.Average(n => n.Net);
                output.WriteLine($"{rival.Name,-30} fill {o.Fill:P0} expected, {fill:P0} happened; rating {o.Rating:F1} / {rating:F1}; net a show {o.NetPerShow:N0} / {net:N0}");

                Assert.InRange(fill - o.Fill, -0.08, 0.08);
                Assert.InRange(rating - o.Rating, -6, 6);       // a night's luck is ±6, over four nights
            }
        }

        [Fact]
        public void AnOptimistExpectsABiggerGate_AndTheSameRightsFee()
        {
            var career = NewWorld(PromotionTier.Local);
            var rival = RivalAt(career, PromotionTier.National);

            var sober = World.Forecast(career, rival, rival.Contracts, Sober);
            var hopeful = World.Forecast(career, rival, rival.Contracts, Sober with { Optimism = 1.25 });

            Assert.True(hopeful.RevenuePerShow > sober.RevenuePerShow);
            Assert.True(hopeful.CashAtHorizon > sober.CashAtHorizon);
            // The card and the house are what they are; it is the money it misreads.
            Assert.Equal(sober.CardPull, hopeful.CardPull, 12);
            Assert.Equal(sober.Fill, hopeful.Fill, 12);
            // And less than a quarter more, because the rights fee is a contract.
            Assert.True(hopeful.RevenuePerShow < sober.RevenuePerShow * 1.25m);
        }

        /// <summary>
        /// The booker prices match-ups with a quick figure that builds no report line. It has
        /// to be the box office's figure, exactly — on a stale audience and a remembered
        /// match-up as much as a fresh one.
        /// </summary>
        [Fact]
        public void TheQuickPullIsThePull()
        {
            var career = NewWorld(PromotionTier.Local);
            for (int i = 0; i < 60; i++) career.AdvanceOneDay();
            var rival = RivalAt(career, PromotionTier.National);
            var today = career.CurrentDate;
            var people = rival.Contracts.Select(c => career.FindWrestler(c.WrestlerId)!).ToList();

            double Response(Wrestler w) => AudienceModel.Response(rival.Audience, w, rival.Promotion.Tier, ShowType.Television);
            double Freshness(BookedMatch m) => BoxOffice.StoryBetween(m.Plan, rival.Feuds)?.Familiarity(today) ?? 1.0;

            int compared = 0;
            foreach (var type in new[] { ShowType.Television, ShowType.PremiumEvent })
                foreach (var item in World.BookCard(rival, people, type, today, 4, Response, Freshness).OfType<BookedMatch>())
                {
                    Assert.Equal(BoxOffice.DrawOf(item, null, today, Response, Freshness).Pull,
                                 BoxOffice.PullOf(item, null, today, Response, Freshness));
                    Assert.Equal(BoxOffice.DrawOf(item, rival.Feuds, today).Pull, BoxOffice.PullOf(item, rival.Feuds, today));
                    compared++;
                }
            Assert.True(compared >= 6);
        }

        // ── What it wants decides what it does ───────────────────────────────

        /// <summary>
        /// The same promotion, the same day, the same wrestler: what he is worth to it depends
        /// on what it is for. This is the test of the whole design — nothing in the planner
        /// knows which of these it is running.
        /// </summary>
        [Fact]
        public void WhatSomebodyIsWorthDependsOnWhatThePromotionWants()
        {
            var career = NewWorld(PromotionTier.Local);
            var rival = RivalAt(career, PromotionTier.National);
            var major = RivalAt(career, PromotionTier.Global);
            var star = major.Contracts.Select(c => career.FindWrestler(c.WrestlerId)!).MaxBy(w => w.EffectiveOverness)!;
            major.Contracts.RemoveAll(c => c.WrestlerId == star.Id);        // a free agent, for the asking

            var hold = World.Forecast(career, rival, rival.Contracts, Sober);
            decimal ask = career.GuaranteeAsk(star, 3);

            decimal Worth(FounderMotive m) => World.Reservation(career, rival, star, hold, World.ObjectiveFor(m), Sober);
            foreach (var m in Enum.GetValues<FounderMotive>())
                output.WriteLine($"{m,-10} would pay {Worth(m),12:N0} a year for {star.RingName} (asks {ask:N0})");

            // Run for names against run for a return: the first pays more for a name.
            Assert.True(Worth(FounderMotive.MarkMoney) > Worth(FounderMotive.Business));
            // And nobody pays more than three times the asking price, whatever they want.
            Assert.All(Enum.GetValues<FounderMotive>(), m => Assert.True(Worth(m) <= ask * 3));
        }

        /// <summary>
        /// The other half of the old bid rule, which bid for anybody: somebody who would not
        /// make a promotion's card is worth nothing to it, and it does not offer for them.
        /// </summary>
        [Fact]
        public void NobodyBidsForSomebodyWhoWouldNotMakeTheirCard()
        {
            var career = NewWorld(PromotionTier.Established);
            var major = RivalAt(career, PromotionTier.Global);
            var theirs = career.Contracts.Where(c => c.IsGuaranteed)
                .Select(c => career.FindWrestler(c.WrestlerId)!).MinBy(w => w.EffectiveOverness)!;
            career.ContractOf(theirs)!.End = career.CurrentDate.AddDays(30);

            var hold = World.Forecast(career, major, major.Contracts, World.DispositionOf(major));
            Assert.Equal(0m, World.Reservation(career, major, theirs, hold, World.ObjectiveOf(major), World.DispositionOf(major)));

            for (int i = 0; i < 14; i++) career.AdvanceOneDay();
            Assert.DoesNotContain(career.Bids, b => b.WrestlerId == theirs.Id && b.Bidder == major.Id);
        }

        /// <summary>
        /// The same promotion with and without money. Short of it, it lets people go and says
        /// that is why; flush, it does not. Nothing tells it to — the fear of closing is in how
        /// it scores, and the money a release saves is in what it forecasts.
        /// </summary>
        [Fact]
        public void APromotionRunningOutOfMoneyLetsPeopleGo_AndTheSameOneFlushDoesNot()
        {
            List<RivalDecision> Decide(bool broke)
            {
                var career = NewWorld(PromotionTier.Local);
                var rival = RivalAt(career, PromotionTier.National);
                // Careful with money, and paying people more than its shows bring in.
                rival.Objective = World.ObjectiveFor(FounderMotive.Business);
                rival.Disposition = World.DispositionFor(FounderMotive.Business);
                var dear = rival.Contracts.Select(c => new Contract
                {
                    WrestlerId = c.WrestlerId, Kind = c.Kind, AnnualGuarantee = c.AnnualGuarantee * 12, Start = c.Start, End = c.End
                }).ToList();
                rival.Contracts.Clear();
                rival.Contracts.AddRange(dear);
                if (broke) rival.Books.Cash = rival.Books.OpeningCash * 0.02m;
                else rival.Books.Cash = rival.Books.OpeningCash * 100m;

                World.Plan(career, rival);
                return rival.Decisions;
            }

            var broke = Decide(broke: true);
            var flush = Decide(broke: false);
            foreach (var d in broke) output.WriteLine($"broke: {d.Move} {d.WrestlerId} — {d.Reason} ({string.Join(", ", d.Terms.Select(t => $"{t.Key} {t.Value:+0.000;-0.000}"))})");
            foreach (var d in flush) output.WriteLine($"flush: {d.Move} {d.WrestlerId} — {d.Reason}");

            Assert.Contains(broke, d => d.Move == "released" && d.Reason.Contains("running out of money"));
            Assert.DoesNotContain(flush, d => d.Reason.Contains("running out of money"));
            Assert.True(broke.Count(d => d.Move == "released") >= flush.Count(d => d.Move == "released"));
        }

        // ── The weekly decision ──────────────────────────────────────────────

        [Fact]
        public void TheSameWorldPlansTheSameWay()
        {
            string Run()
            {
                var career = NewWorld(PromotionTier.Local);
                for (int i = 0; i < 90; i++) career.AdvanceOneDay();
                return string.Join("\n", career.Rivals.SelectMany(r => r.Decisions.Select(d =>
                    $"{r.Id} {d.Date:O} {d.Move} {d.WrestlerId} {d.Annual} {d.Score}")));
            }

            string first = Run();
            Assert.NotEmpty(first);
            Assert.Equal(first, Run());
        }

        [Fact]
        public void APromotionMakesAFewMovesAWeekAtMost_AndSaysWhyForEach()
        {
            var career = NewWorld(PromotionTier.Local);
            for (int i = 0; i < 120; i++) career.AdvanceOneDay();

            foreach (var rival in career.Rivals)
            {
                Assert.All(rival.Decisions.GroupBy(d => d.Date), week => Assert.True(week.Count() <= World.MaxMovesPerWeek));
                Assert.All(rival.Decisions, d =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(d.Reason));
                    Assert.True(d.Score > World.Epsilon);
                    Assert.NotEmpty(d.Terms);
                    Assert.False(string.IsNullOrWhiteSpace(d.Believed));
                });
            }
            Assert.Contains(career.Rivals, r => r.Decisions.Count > 0);
        }

        /// <summary>
        /// One more wrestler does nothing for a promotion that has none — it takes two to have
        /// a match and several to have a show — so a planner that only looked one move ahead
        /// would leave it shut for good.
        /// </summary>
        [Fact]
        public void APromotionWithNobodyCanStartAgain()
        {
            var career = NewWorld(PromotionTier.Local);
            var indie = RivalAt(career, PromotionTier.Independent);
            indie.Contracts.Clear();
            Assert.Equal(0, World.Forecast(career, indie, indie.Contracts, World.DispositionOf(indie)).ShowsInHorizon);

            World.Plan(career, indie);

            Assert.True(indie.Contracts.Count >= 2);
            Assert.True(World.Forecast(career, indie, indie.Contracts, World.DispositionOf(indie)).ShowsInHorizon > 0);
        }


        /// <summary>Plan §7: what planning costs is counted, not timed.</summary>
        [Fact]
        public void PlanningStaysInsideItsBudget()
        {
            var career = NewWorld(PromotionTier.Local);
            for (int i = 0; i < 60; i++) career.AdvanceOneDay();

            int worst = 0;
            foreach (var rival in career.Rivals.Where(r => r.IsActive))
            {
                // A week in which it has to look: no memory of having looked already.
                (rival.QuietAs, rival.QuietSince) = (0, null);
                World.ForecastsRun = 0;
                World.Plan(career, rival);
                output.WriteLine($"{rival.Name}: {World.ForecastsRun} forecasts in a week's planning");
                worst = Math.Max(worst, World.ForecastsRun);

                // And having looked and found nothing, the same week again costs nothing.
                if (rival.QuietSince is not null)
                {
                    World.ForecastsRun = 0;
                    World.Plan(career, rival);
                    Assert.Equal(0, World.ForecastsRun);
                }
            }
            Assert.InRange(worst, 1, 150);
        }

        /// <summary>Nobody is on a list of people a promotion can book while exclusive to somebody else.</summary>
        [Fact]
        public void AnExclusiveDealEndsTheirOtherBookings()
        {
            var career = NewWorld(PromotionTier.Local);
            for (int i = 0; i < 180; i++) career.AdvanceOneDay();

            Assert.Contains(career.Rivals.SelectMany(r => r.Decisions), d => d.Move == "signed");
            foreach (var w in career.Roster.Where(w => career.SignedTo(w) is not null))
            {
                Assert.DoesNotContain(career.Rivals, r => r.ContractOf(w) is { IsGuaranteed: false });
                Assert.Null(career.ContractOf(w));
            }
        }

        // ── Saves ────────────────────────────────────────────────────────────

        [Fact]
        public void WhatItWantsAndWhatItDecidedSurviveASave()
        {
            var career = NewWorld(PromotionTier.Local);
            var rival = RivalAt(career, PromotionTier.National);
            rival.Objective = new Objective(0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8);
            rival.Disposition = new Disposition(200, 1.1, 99, 2.5);
            rival.Favourites.Add(career.Roster[3].Id);
            for (int i = 0; i < 60; i++) career.AdvanceOneDay();
            Assert.NotEmpty(career.Rivals.SelectMany(r => r.Decisions));

            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career), DataLoaders.LoadEmbeddedWrestlers());
            var theirs = loaded.Rivals.First(r => r.Id == rival.Id);

            Assert.Equal(rival.Objective, theirs.Objective);
            Assert.Equal(rival.Disposition, theirs.Disposition);
            Assert.Equal(rival.Favourites, theirs.Favourites);
            foreach (var (was, now) in career.Rivals.Zip(loaded.Rivals))
            {
                Assert.Equal(was.Decisions.Count, now.Decisions.Count);
                foreach (var (a, b) in was.Decisions.Zip(now.Decisions))
                {
                    Assert.Equal((a.Date, a.Move, a.WrestlerId, a.Annual, a.Reason, a.Score, a.Believed),
                                 (b.Date, b.Move, b.WrestlerId, b.Annual, b.Reason, b.Score, b.Believed));
                    Assert.Equal(a.Terms, b.Terms);
                    Assert.Equal(a.Rejected, b.Rejected);
                }
            }
            // A default rival carries no override: it is still its founder motive's.
            Assert.All(loaded.Rivals.Where(r => r.Id != rival.Id), r => Assert.Null(r.Objective));

            // And the day after is the same day either way.
            career.AdvanceOneDay();
            loaded.AdvanceOneDay();
            Assert.Equal(career.Rivals.Sum(r => r.Books.Cash), loaded.Rivals.Sum(r => r.Books.Cash));
        }

        // ── The world ────────────────────────────────────────────────────────

        /// <summary>
        /// Five years of the default world, printed. **Nothing here is a target** — a rival
        /// that closes, hoards or thrives is something to read and ask about, not something to
        /// tune until it stops. The only assertion is that the world is still a world.
        /// </summary>
        [Fact]
        public void WhatTheWorldDoesWithIt_IsMeasuredNotForced()
        {
            var career = NewWorld(PromotionTier.Local);
            var moves = new SortedDictionary<string, int>();

            void Line(string when)
            {
                var ov = career.Roster.Select(w => w.Overness).OrderByDescending(x => x).ToList();
                output.WriteLine($"== {when}: overness top ten {ov.Take(10).Average():F0}, median {ov[ov.Count / 2]:F0}; " +
                                 $"nobody's: {career.Roster.Count(w => career.ContractOf(w) is null && career.Rivals.All(r => r.ContractOf(w) is null))} of {career.Roster.Count}");
                foreach (var r in career.Rivals)
                    output.WriteLine($"   {r.Name,-30} {(r.IsActive ? "" : $"closed {r.Folded:MMM yyyy} ")}cash {r.Books.Cash,14:N0}  roster {r.Contracts.Count,3}  " +
                                     $"guarantees {r.Contracts.Sum(c => c.AnnualGuarantee),12:N0}  reputation {r.Books.Reputation:F0}  " +
                                     $"last house {(r.Nights.Count == 0 ? 0 : r.Nights[^1].Attendance / (double)Math.Max(1, r.Nights[^1].Capacity)):P0}");
            }

            Line("opening");
            for (int year = 1; year <= 5; year++)
            {
                for (int i = 0; i < 365; i++)
                {
                    var last = career.Rivals.ToDictionary(r => r, r => r.Decisions.LastOrDefault());
                    career.AdvanceOneDay();
                    foreach (var r in career.Rivals)
                        foreach (var d in r.Decisions.Skip(last[r] is null ? 0 : r.Decisions.IndexOf(last[r]!) + 1))
                            moves[$"{r.Name}, year {year}: {d.Move}"] = moves.GetValueOrDefault($"{r.Name}, year {year}: {d.Move}") + 1;
                }
                Line($"year {year}");
            }
            foreach (var (what, count) in moves) output.WriteLine($"{what} ×{count}");

            Assert.Contains(career.Rivals, r => r.IsActive);
        }
    }
}
