using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// How an audience tires. docs/wrestling-reference/17 §3–4, 23 §2; docs/rival-ai-plan.md §2A.
    ///
    /// The owner's test case is the first one here: a national promotion cannot run on six
    /// wrestlers, a local one can, and no line of code mentions roster size.
    /// </summary>
    public class AudienceTests
    {
        private readonly ITestOutputHelper output;
        public AudienceTests(ITestOutputHelper output) => this.output = output;

        private static readonly DateOnly Day = new(2025, 1, 6);

        private static BookedMatch Match(Wrestler a, Wrestler b) => new()
        {
            Plan = new MatchPlan
            {
                WrestlerA = a,
                WrestlerB = b,
                Beats     = MatchStructureLibrary.Find("TV Formula")!.Beats.Select(x => x.Clone()).ToList()
            },
            StructureName = "TV Formula"
        };

        private static Career NewCareer(PromotionTier tier, IEnumerable<Wrestler> roster)
        {
            var career = new Career
            {
                Promotion   = new Promotion { Name = "Test Wrestling", Tier = tier },
                StartDate   = Day,
                CurrentDate = Day,
                Roster      = roster.ToList()
            };
            career.OpenBooks(Day);
            return career;
        }

        /// <summary>
        /// The best card this roster can make tonight: pairings within a division, taken greedily
        /// by what they would draw *as this audience now sees them* — so a booker with options
        /// rotates away from a stale match, and one without cannot.
        /// </summary>
        private static List<ICardItem> BestCard(Career career, ShowType type)
        {
            var fit = career.Roster.Where(w => !w.IsInjured(career.CurrentDate)).ToList();
            var pairs = fit.GroupBy(w => w.Division)
                .SelectMany(g => g.SelectMany((a, i) => g.Skip(i + 1).Select(b => Match(a, b))))
                .Select(m => (Match: m, Pull: BoxOffice.DrawOf(m, career.FeudBook, career.CurrentDate,
                                                               w => career.Response(w, type)).Pull))
                .OrderByDescending(x => x.Pull)
                .ThenBy(x => x.Match.Name, StringComparer.Ordinal)
                .ToList();

            // Fill the slot. Everybody once first; then, if the roster has run out and the slot
            // is still short, people work a second time — which is what a thin roster has to do,
            // and what burns its match-ups.
            int runtime = career.Promotion.DefaultRuntimeFor(type);
            var times = new Dictionary<Wrestler, int>();
            var card = new List<ICardItem>();
            int minutes = 0;
            for (int pass = 1; pass <= 2 && minutes < runtime * AudienceModel.FullCard; pass++)
                foreach (var (match, _) in pairs)
                {
                    if (minutes >= runtime * AudienceModel.FullCard) break;
                    if (card.Contains(match) || match.Wrestlers.Any(w => times.GetValueOrDefault(w) >= pass)) continue;
                    foreach (var w in match.Wrestlers) times[w] = times.GetValueOrDefault(w) + 1;
                    card.Add(match);
                    minutes += match.DurationMinutes;
                }
            card.Reverse();   // main event last
            return card;
        }

        private sealed record Night(int Week, double Fill, int Viewers, double Rating, double Reputation, double ShowSameness, double Filled);

        /// <summary>Runs a show every <paramref name="everyDays"/> days with the best card available.</summary>
        private List<Night> Run(Career career, int shows, int everyDays, ShowType type)
        {
            var nights = new List<Night>();
            for (int i = 0; i < shows; i++)
            {
                var show = career.Schedule($"Show {i + 1}", career.CurrentDate, type);
                show.RuntimeMinutes = career.Promotion.DefaultRuntimeFor(type);
                show.Card.AddRange(BestCard(career, type));
                var result = career.RunShow(show)!;
                var f = result.Finances!;
                nights.Add(new Night(i + 1, f.Fill, f.Viewers, result.OverallRating, career.Books.Reputation,
                                     career.Audience.ShowSameness, result.Audience!.Filled));
                for (int d = 0; d < everyDays; d++) career.AdvanceOneDay();
            }
            return nights;
        }

        private void Print(string label, IEnumerable<Night> nights)
        {
            output.WriteLine(label);
            foreach (var n in nights)
                output.WriteLine($"  wk {n.Week,2}: fill {n.Fill,5:P0}  viewers {n.Viewers,9:N0}  rating {n.Rating,5:F1}  rep {n.Reputation,5:F1}  show-sameness {n.ShowSameness,5:F1}  slot {n.Filled:P0}");
        }

        private static List<Wrestler> TopOfTheRoster(int perDivision) =>
            DataLoaders.LoadEmbeddedWrestlers()
                .GroupBy(w => w.Division)
                .SelectMany(g => g.OrderByDescending(w => w.Overness).Take(perDivision))
                .ToList();

        /// <summary>Runs one show with exactly these matches, then lets a week pass.</summary>
        private static ShowResult Week(Career career, params BookedMatch[] matches)
        {
            var show = career.Schedule("Weekly", career.CurrentDate, ShowType.Television);
            show.RuntimeMinutes = 30;     // these tests are about the acts, not the slot
            show.Card.AddRange(matches);
            var result = career.RunShow(show)!;
            for (int d = 0; d < 7; d++) career.AdvanceOneDay();
            return result;
        }

        // ── The owner's claim ────────────────────────────────────────────────

        /// <summary>
        /// "A roster of six… wouldn't even last… if you're like a national level promotion."
        /// The six most over wrestlers in the game, three a division, on weekly national
        /// television, booked as well as six people can be: within two months the house and the
        /// audience have gone. A tier-sized roster booked the same way holds both.
        ///
        /// Nothing in the engine mentions roster size. Six fails because it has six match-ups a
        /// division-pair can make (doc 17 §4.1: a match-up is fresh for 2–4 encounters), because
        /// filling two hours means everybody works twice, and because the same faces every week
        /// is the same show.
        /// </summary>
        [Fact]
        public void SixWrestlersCannotCarryANationalPromotion_ADeepRosterCan()
        {
            var six  = Run(NewCareer(PromotionTier.National, TopOfTheRoster(3)), 12, 7, ShowType.Television);
            var deep = Run(NewCareer(PromotionTier.National, TopOfTheRoster(15)), 12, 7, ShowType.Television);
            Print("National, six wrestlers, weekly TV", six);
            Print("National, thirty wrestlers, weekly TV", deep);

            int gone = six.FindIndex(n => n.Fill < 0.80) + 1;
            output.WriteLine($"six: house below 80% in week {gone}");

            Assert.InRange(gone, 2, 8);
            Assert.True(six[7].Viewers < 0.75 * six[0].Viewers, "The television audience should have left with the house.");
            Assert.True(six[11].Reputation < six[2].Reputation, "…and the promotion's name with it.");

            Assert.All(deep, n => Assert.True(n.Fill >= 0.95));
            Assert.True(deep[11].Viewers >= 0.90 * deep[0].Viewers);
            Assert.True(deep[11].Reputation >= deep[0].Reputation);
        }

        /// <summary>
        /// "Only the most local promotion could get away with that." The same six, at a local
        /// promotion running every six weeks, hold a full house for half a year — the cadence does
        /// it, not a special case: acts and match-ups rest between shows.
        ///
        /// **Measured, and worth knowing:** they do not hold it for ever. With everybody working
        /// everybody every six weeks the match-ups never get the sixty days they need to be
        /// forgotten, and the house starts to slide in the second half of the year.
        /// </summary>
        [Fact]
        public void TheSameSixLastFarLongerAtALocalPromotion()
        {
            var local = Run(NewCareer(PromotionTier.Local, TopOfTheRoster(3)), 9, 45, ShowType.PremiumEvent);
            Print("Local, six wrestlers, every 45 days", local);

            int full = local.TakeWhile(n => n.Fill >= 0.90).Count();
            output.WriteLine($"local six: {full} shows ({full * 45} days) at 90% or better");

            Assert.True(full * 45 >= 180, "Six should carry a local promotion for at least half a year.");
            Assert.Equal(0, local[^1].ShowSameness, 1);   // six weeks apart is never "the same show"
        }

        // ── Sameness, not screen time ────────────────────────────────────────

        /// <summary>
        /// The test that the model is sameness and not exposure. One wrestler is on television
        /// every week for six months against a rotation of opponents; another is on every week
        /// for the same six months in the same match. Same screen time. Only the second wears.
        /// </summary>
        [Fact]
        public void BeingOnEveryWeekIsNotTheProblem_DoingTheSameThingIs()
        {
            var moving = TestRoster.Make("Moving", overness: 80);
            var stuck = TestRoster.Make("Stuck", overness: 80);
            var foil = TestRoster.Make("Foil", overness: 70);
            var others = Enumerable.Range(0, 12).Select(i => TestRoster.Make($"Opp{i}", overness: 70)).ToList();
            var career = NewCareer(PromotionTier.National, [moving, stuck, foil, .. others]);

            // Both win one week and lose the next, so neither is on a streak: a run of wins
            // tires the hardcore fans by itself (doc 17 §3.11), and this test is not about that.
            for (int week = 0; week < 26; week++)
            {
                var opponent = others[week % others.Count];
                if (week % 2 == 0) Week(career, Match(moving, opponent), Match(stuck, foil));
                else               Week(career, Match(opponent, moving), Match(foil, stuck));
            }

            double movingNow = career.Response(moving, ShowType.Television);
            double stuckNow = career.Response(stuck, ShowType.Television);
            output.WriteLine($"26 weeks on screen: moving {career.Audience.SamenessOf(moving):F1} (×{movingNow:F2}), stuck {career.Audience.SamenessOf(stuck):F1} (×{stuckNow:F2})");

            Assert.True(movingNow >= 0.97, "On every week with something new each time is not staleness.");
            Assert.True(stuckNow <= 0.85, "On every week in the same match is.");
        }

        /// <summary>
        /// "It's probably affected by the charisma and gimmick of the wrestler." Two acts of the
        /// same standing, in the same rut: the one with charisma and a long-lived gimmick holds
        /// far longer than the one built on a short-term gimmick and athletic novelty
        /// (doc 17 §3.4).
        /// </summary>
        [Fact]
        public void ACharismaticActOutlastsASpotAct()
        {
            var star = TestRoster.Make("Star", overness: 80, charisma: 4.8);
            star.Gimmick.Durability = GimmickDurability.LongTerm;
            var flyer = TestRoster.Make("Flyer", overness: 80, charisma: 1.8);
            flyer.Gimmick.Durability = GimmickDurability.ShortTerm;
            flyer.Style = WrestlingStyle.HighFlyer;
            var a = TestRoster.Make("FoilA", overness: 70);
            var b = TestRoster.Make("FoilB", overness: 70);
            var career = NewCareer(PromotionTier.National, [star, flyer, a, b]);

            Assert.True(AudienceModel.Rate(flyer) > 2 * AudienceModel.Rate(star));

            int starWeeks = 0, flyerWeeks = 0;
            for (int week = 1; week <= 40; week++)
            {
                Week(career, Match(star, a), Match(flyer, b));
                if (starWeeks == 0 && career.Response(star, ShowType.Television) < 0.95) starWeeks = week;
                if (flyerWeeks == 0 && career.Response(flyer, ShowType.Television) < 0.95) flyerWeeks = week;
            }
            output.WriteLine($"weeks in the same match before the reaction drops 5%: flyer {flyerWeeks}, star {(starWeeks == 0 ? "never in 40" : starWeeks.ToString())}");

            Assert.InRange(flyerWeeks, 3, 16);
            Assert.True(starWeeks == 0 || starWeeks > 2 * flyerWeeks);
        }

        [Fact]
        public void SomethingNewTakesItBackOff()
        {
            var stale = TestRoster.Make("Stale", overness: 80, charisma: 2.0);
            var foil = TestRoster.Make("Foil", overness: 70);
            var fresh = TestRoster.Make("Fresh", overness: 70);
            var career = NewCareer(PromotionTier.National, [stale, foil, fresh]);

            for (int week = 0; week < 14; week++) Week(career, Match(stale, foil));
            double before = career.Audience.SamenessOf(stale);

            Week(career, Match(stale, fresh));

            Assert.True(before > 30);
            Assert.True(career.Audience.SamenessOf(stale) < before - 4, "A new opponent is something new.");
        }

        /// <summary>
        /// Rhea Ripley and John Cena: one wrestler, two audiences. Months of the same strong
        /// booking leave the casual fan untouched and the hardcore fan tired — and a winning
        /// streak tires only the people who follow closest (doc 17 §3.11, doc 23 §2–3).
        /// </summary>
        [Fact]
        public void OneWrestlerTwoAudiences()
        {
            const double sameness = 50;

            double casual   = AudienceModel.GroupResponse(FanGroup.Casual, sameness, streak: 0);
            double regular  = AudienceModel.GroupResponse(FanGroup.Regular, sameness, streak: 0);
            double hardcore = AudienceModel.GroupResponse(FanGroup.Hardcore, sameness, streak: 0);

            Assert.Equal(1.0, casual, 10);
            Assert.True(regular < 0.95);
            Assert.True(hardcore < regular);

            // Dominance: only the people who follow closest mind.
            Assert.Equal(casual, AudienceModel.GroupResponse(FanGroup.Casual, sameness, streak: 12));
            Assert.True(AudienceModel.GroupResponse(FanGroup.Hardcore, sameness, streak: 12) < hardcore);
        }

        /// <summary>The people who like an act most are the ones whose patience decides its pull.</summary>
        [Fact]
        public void ACasualFavouriteIsCarriedByTheCasualFans()
        {
            Wrestler With(string name, double casual, double hardcore)
            {
                var w = TestRoster.Make(name);
                w.Gimmick.AppealRatings =
                [
                    new FanGroupAppeal { Group = "Casual", AppealScore = casual },
                    new FanGroupAppeal { Group = "Hardcore", AppealScore = hardcore }
                ];
                return w;
            }
            var mainstream = With("Mainstream", casual: 0.95, hardcore: 0.2);
            var cult = With("Cult", casual: 0.2, hardcore: 0.95);

            var audience = new Audience();
            audience.Acts[mainstream.Id] = new Act { Sameness = 60, Streak = 10, LastSeen = Day };
            audience.Acts[cult.Id] = new Act { Sameness = 60, Streak = 10, LastSeen = Day };

            Assert.True(AudienceModel.Response(audience, mainstream, PromotionTier.National, ShowType.PremiumEvent)
                      > AudienceModel.Response(audience, cult, PromotionTier.National, ShowType.PremiumEvent));

            // Regular has no rating in the roster; it reads Casual's.
            Assert.Equal(0.95, AudienceModel.Appeal(mainstream, FanGroup.Regular), 10);
        }

        // ── The show itself ──────────────────────────────────────────────────

        [Fact]
        public void TheSameShowEveryWeekIsItsOwnProblem()
        {
            var roster = Enumerable.Range(0, 8).Select(i => TestRoster.Make($"W{i}", overness: 70, charisma: 4.9)).ToList();
            var career = NewCareer(PromotionTier.National, roster);

            ShowResult last = null!;
            for (int week = 0; week < 6; week++)
                last = Week(career, Match(roster[0], roster[1]), Match(roster[2], roster[3]),
                                    Match(roster[4], roster[5]), Match(roster[6], roster[7]));

            output.WriteLine($"show sameness {last.Audience!.ShowSamenessAfter:F1}, penalty {last.Audience.SamenessPenalty:P1}");
            Assert.True(last.Audience.ShowSamenessAfter > 60);
            Assert.True(last.Audience.SamenessPenalty > 0.08);
        }

        [Fact]
        public void AThinCardSellsFewerTickets_AndIsAWorseShow()
        {
            var a = TestRoster.Make("A", overness: 85);
            var b = TestRoster.Make("B", overness: 85);
            var roster = new List<Wrestler> { a, b };
            roster.AddRange(Enumerable.Range(0, 12).Select(i => TestRoster.Make($"U{i}", overness: 40)));
            var career = NewCareer(PromotionTier.National, roster);

            var thin = career.Schedule("Thin", Day, ShowType.Television);
            thin.Card.Add(Match(a, b));
            var full = career.Schedule("Full", Day, ShowType.Television);
            for (int i = 0; i < 12; i += 2) full.Card.Add(Match(roster[2 + i], roster[3 + i]));
            full.Card.Add(Match(a, b));

            var thinHouse = career.Projection(thin);
            var fullHouse = career.Projection(full);

            // Same main event, so the same pull at the top of the card…
            Assert.Equal(thinHouse.Draws[0].Pull, fullHouse.Draws[0].Pull, 10);
            // …and fewer people come to one match than to a show.
            Assert.True(thinHouse.CardDepth < 0.7);
            Assert.Equal(1.0, fullHouse.CardDepth, 10);
            Assert.True(thinHouse.Demand < 0.75 * fullHouse.Demand);
            Assert.True(thinHouse.TuneIn < 0.75 * fullHouse.TuneIn);

            Assert.True(AudienceModel.UnderrunPenalty(0.10) > 0.2);
            Assert.Equal(0, AudienceModel.UnderrunPenalty(0.55));
        }

        // ── The box office ───────────────────────────────────────────────────

        /// <summary>
        /// A rematch between two people with no heat between them was sold as a first meeting
        /// every time: the box office looked for the story among *live* feuds only, and a pairing
        /// with no heat is not live. The match count is what goes stale, and it is on the book.
        /// </summary>
        [Fact]
        public void ARematchWithNoHeatIsNotSoldAsAFirstMeeting()
        {
            var a = TestRoster.Make("A");
            var b = TestRoster.Make("B");
            var career = NewCareer(PromotionTier.National, [a, b]);

            for (int week = 0; week < 5; week++) Week(career, Match(a, b));

            var again = career.Schedule("Again", career.CurrentDate, ShowType.Television);
            again.Card.Add(Match(a, b));
            var draw = career.Projection(again).Draws[0];

            output.WriteLine($"sixth meeting: freshness ×{draw.Freshness:F2}, acts ×{draw.Acts:F2}");
            Assert.True(draw.Freshness < 0.7);
        }

        [Fact]
        public void ThePlayerPaysForSamenessAtTheBoxOffice()
        {
            var a = TestRoster.Make("A", overness: 80, charisma: 1.8);
            var b = TestRoster.Make("B", overness: 80, charisma: 1.8);
            var c = TestRoster.Make("C", overness: 80, charisma: 1.8);
            var career = NewCareer(PromotionTier.National, [a, b, c]);

            for (int week = 0; week < 14; week++) Week(career, Match(a, b));

            var next = career.Schedule("Next", career.CurrentDate, ShowType.Television);
            next.Card.Add(Match(a, c));     // a new opponent, so the match-up is fresh…
            var draw = career.Projection(next).Draws[0];

            Assert.Equal(1.0, draw.Freshness, 10);
            Assert.True(draw.Acts < 0.97, "…but the audience has still seen fourteen weeks of A.");

            // A show booked a year out is watched a year out.
            var later = career.Schedule("Later", career.CurrentDate.AddDays(365), ShowType.Television);
            later.Card.Add(Match(a, c));
            Assert.True(career.Projection(later).Draws[0].Acts > draw.Acts);
        }

        [Fact]
        public void ExhibitionHasNoAudienceMemory()
        {
            var a = TestRoster.Make("A");
            var b = TestRoster.Make("B");
            var show = new Show { Card = [Match(a, b)], TotalDurationMinutes = 120 };

            var result = new ShowSimulator(new FeudBook(), seed: 7).Simulate(show);

            Assert.Null(result.Audience);
            Assert.DoesNotContain(result.Items[0].Notes, n => n.Contains("seen this too often"));
        }

        // ── Saves ────────────────────────────────────────────────────────────

        [Fact]
        public void TheAudiencesMemorySurvivesASave()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var career = NewCareer(PromotionTier.National, roster);
            var men = roster.Where(w => w.Division == Division.Mens).Take(4).ToList();
            ShowResult last = null!;
            for (int week = 0; week < 4; week++) last = Week(career, Match(men[0], men[1]), Match(men[2], men[3]));

            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career), DataLoaders.LoadEmbeddedWrestlers());

            Assert.Equal(career.Audience.SamenessOf(men[0]), loaded.Audience.SamenessOf(loaded.FindWrestler(men[0].Id)!), 2);
            Assert.Equal(career.Audience.ActOf(men[0])!.Streak, loaded.Audience.ActOf(loaded.FindWrestler(men[0].Id)!)!.Streak);
            Assert.Equal(career.Audience.ShowSameness, loaded.Audience.ShowSameness, 2);
            Assert.Equal(career.Audience.Recent.Count, loaded.Audience.Recent.Count);
            Assert.Equal(last.Audience!.SamenessPenalty, loaded.Shows.Last(s => s.HasRun).Result!.Audience!.SamenessPenalty, 3);
        }

        [Fact]
        public void ASaveFromBeforeTheAudienceHadAMemory_LoadsWithEverybodyFresh()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var career = NewCareer(PromotionTier.National, roster);
            var dto = SaveSerializer.ToDto(career);
            dto.Audience = null;

            var loaded = SaveSerializer.FromJson(System.Text.Json.JsonSerializer.Serialize(dto), DataLoaders.LoadEmbeddedWrestlers());

            Assert.Empty(loaded.Audience.Acts);
            Assert.Equal(1.0, loaded.Response(loaded.Roster[0], ShowType.Television));
        }
    }
}
