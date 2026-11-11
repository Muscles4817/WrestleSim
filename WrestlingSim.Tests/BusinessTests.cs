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
    /// C5 — rate well vs draw well (docs/wrestling-reference/31-sim-mapping.md). The premise,
    /// from doc 18 §7, is that the two outputs can diverge sharply, so the tests that matter
    /// most here are the ones that pin a divergence rather than a figure.
    /// </summary>
    public class BusinessTests
    {
        private readonly ITestOutputHelper output;
        public BusinessTests(ITestOutputHelper output) => this.output = output;

        private static readonly DateOnly Day = new(2025, 3, 1);

        private static BookedMatch Match(Wrestler a, Wrestler b, Feud? feud = null) => new()
        {
            Plan = new MatchPlan
            {
                WrestlerA = a,
                WrestlerB = b,
                Beats     = MatchStructureLibrary.Find("TV Formula")!.Beats.Select(x => x.Clone()).ToList(),
                Feud      = feud
            },
            StructureName = "TV Formula"
        };

        private static Career NewCareer(PromotionTier tier, params Wrestler[] roster)
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

        private static ScheduledShow Show(Career career, ShowType type, params ICardItem[] card)
        {
            var show = career.Schedule("Tonight", Day, type);
            show.Card.AddRange(card);
            return show;
        }

        private static Feud HotFeud(FeudBook book, Wrestler a, Wrestler b)
        {
            var update = book.Record(a, b, Feud.NuclearThreshold, date: Day);
            Assert.True(update.Feud.Intensity >= FeudIntensity.Hot);
            return update.Feud;
        }

        // ── The premise ──────────────────────────────────────────────────────

        /// <summary>
        /// Doc 18 §7: "a five-star match between two people nobody cares about draws nothing;
        /// a three-star match at the end of a hot feud sells out." Unknowns at the top of the
        /// skill scale against stars at the bottom of it, with a hot feud behind the stars.
        ///
        /// **What this pins is narrower than the doc, and the reason is the engine, not the box
        /// office.** The star rating is not a pure quality score: its crowd component reads
        /// investment, which reads overness, and measured over forty seeds overness moves the
        /// rating about as far as ring skill does (≈1.2★ from unknown to star at fixed skill,
        /// ≈1.0★ from worst to best skill at fixed overness). So the unknowns cannot out-rate
        /// the stars by much — on this pairing they do not out-rate them at all — and the
        /// doc's extreme case is unreachable. What does hold is that the two outputs answer
        /// the card very differently: the stars draw several times what the unknowns do, for
        /// a rating that differs by a fraction. Recorded in docs/business-design-record.md.
        /// </summary>
        [Fact]
        public void DrawAndRatingAreDifferentOutputs_TheDrawGapDwarfsTheRatingGap()
        {
            var workerA = TestRoster.Make("Worker A", overness: 25, skill: 4.8, psychology: 95);
            var workerB = TestRoster.Make("Worker B", overness: 23, skill: 4.8, psychology: 95);
            var starA   = TestRoster.Make("Star A", overness: 92, skill: 1.8, psychology: 55);
            var starB   = TestRoster.Make("Star B", overness: 90, skill: 1.8, psychology: 55);

            var book = new FeudBook();
            var feud = HotFeud(book, starA, starB);

            var workers = Match(workerA, workerB);
            var stars   = Match(starA, starB, feud);

            double Rating(BookedMatch m, string who) => Enumerable.Range(0, 40)
                .Average(i => new MatchEngine(StableSeed.From("c5", who, i)).Execute(m.Plan).StarRating);

            double workersRating = Rating(workers, "workers");
            double starsRating   = Rating(stars, "stars");
            double workersDraw = BoxOffice.DrawOf(workers, book, Day).Pull;
            double starsDraw   = BoxOffice.DrawOf(stars, book, Day).Pull;

            output.WriteLine($"workers {workersRating:F2}★ pull {workersDraw:F3}");
            output.WriteLine($"stars   {starsRating:F2}★ pull {starsDraw:F3}");

            Assert.True(starsDraw > 4 * workersDraw, "The stars should outdraw the unknowns several times over.");
            Assert.True(starsRating / workersRating < 1.3,
                "…for a rating within a third of theirs. If this fails the engine has started " +
                "rating stars well above workers, and draw and rating are converging.");
        }

        /// <summary>
        /// The house is sold before the bell. What the booking screen projects for the card is
        /// what the night takes, even though running it moves the heat, overness and
        /// familiarity the projection was read from.
        /// </summary>
        [Fact]
        public void TheHouseIsSoldOnTheCardAsAdvertised_NotOnHowItWent()
        {
            var a = TestRoster.Make("A", overness: 80);
            var b = TestRoster.Make("B", overness: 78);
            var career = NewCareer(PromotionTier.SuperIndie, a, b);
            var feud = HotFeud(career.FeudBook, a, b);
            var show = Show(career, ShowType.PremiumEvent, Match(a, b, feud));

            var projected = career.Projection(show);
            var result = career.RunShow(show)!;

            Assert.NotNull(result.Finances);
            Assert.Equal(projected.Demand, result.Finances!.Demand);
            Assert.Equal(projected.Gate, result.Finances.Gate);
            Assert.Equal(projected.Net, result.Finances.Net);
            Assert.Equal(BoxOffice.For(PromotionTier.SuperIndie).OpeningCash + projected.Net, career.Books.Cash);
        }

        /// <summary>Quality sells the next ticket: the same card draws more after good shows.</summary>
        [Fact]
        public void QualitySellsTheNextTicket()
        {
            Assert.True(BoxOffice.Demand(1_000, 0.5, reputation: 80, PromotionTier.SuperIndie)
                      > BoxOffice.Demand(1_000, 0.5, reputation: 30, PromotionTier.SuperIndie));

            // And reputation is where a show's quality goes: a step toward its rating, bigger
            // for the night that is remembered longest.
            double tv = BoxOffice.ReputationAfter(50, 90, ShowType.Television);
            double pe = BoxOffice.ReputationAfter(50, 90, ShowType.PremiumEvent);
            Assert.True(tv > 50 && pe > tv);
            Assert.True(BoxOffice.ReputationAfter(50, 20, ShowType.Television) < 50);
        }

        [Fact]
        public void RunningAShow_MovesReputationTowardItsRating()
        {
            var a = TestRoster.Make("A");
            var b = TestRoster.Make("B");
            var career = NewCareer(PromotionTier.Independent, a, b);
            var result = career.RunShow(Show(career, ShowType.PremiumEvent, Match(a, b)))!;

            double expected = BoxOffice.ReputationAfter(CareerBooks.OpeningReputation, result.OverallRating, ShowType.PremiumEvent);
            Assert.Equal(expected, career.Books.Reputation, 6);
            Assert.Equal(expected, result.Finances!.ReputationAfter, 6);
            Assert.Equal(career.Books.Cash, result.Finances.CashAfter);
        }

        /// <summary>Running a date early jumps the clock, and the days it jumps still cost money.</summary>
        [Fact]
        public void RunningAShowEarly_StillPaysForTheDaysItSkips()
        {
            var a = TestRoster.Make("A");
            var b = TestRoster.Make("B");
            var career = NewCareer(PromotionTier.SuperIndie, a, b);
            var show = career.Schedule("Later", Day.AddDays(10), ShowType.PremiumEvent);
            show.Card.Add(Match(a, b));

            var net = career.Projection(show).Net;
            career.RunShow(show);

            Assert.Equal(Day.AddDays(10), career.CurrentDate);
            Assert.Equal(10 * career.DailyOverhead, career.Books.Overheads);
            Assert.Equal(career.Books.OpeningCash + net - 10 * career.DailyOverhead, career.Books.Cash);
        }

        /// <summary>
        /// Two partners feuding with each other are not what their tag match is selling. Only
        /// a story across the ring builds the draw.
        /// </summary>
        [Fact]
        public void AFeudBetweenTagPartnersDoesNotSellTheirMatch()
        {
            var a = TestRoster.Make("A"); var b = TestRoster.Make("B");
            var c = TestRoster.Make("C"); var d = TestRoster.Make("D");
            var book = new FeudBook();
            HotFeud(book, a, b);

            var tag = new BookedMatch
            {
                Plan = new MatchPlan
                {
                    Sides = [new MatchSide { Members = [a, b] }, new MatchSide { Members = [c, d] }],
                    Beats = MatchStructureLibrary.Find("TV Formula")!.Beats.Select(x => x.Clone()).ToList()
                }
            };
            Assert.Equal(1.0, BoxOffice.DrawOf(tag, book, Day).Build, 10);

            HotFeud(book, a, c);
            Assert.True(BoxOffice.DrawOf(tag, book, Day).Build > 1.3);
        }

        [Fact]
        public void TicketPricesAreWholeDollars_SoTheGateLineAddsUp()
        {
            foreach (var tier in Enum.GetValues<PromotionTier>())
            foreach (var type in Enum.GetValues<ShowType>())
            {
                var price = BoxOffice.PriceFor(BoxOffice.For(tier), type);
                Assert.Equal(Math.Round(price), price);
            }
        }

        // ── What sells a ticket ──────────────────────────────────────────────

        [Fact]
        public void AStaleRematchDrawsLessThanTheFirstMeeting()
        {
            var a = TestRoster.Make("A");
            var b = TestRoster.Make("B");
            var book = new FeudBook();
            var feud = book.GetOrCreate(a, b);

            double first = BoxOffice.DrawOf(Match(a, b, feud), book, Day).Pull;
            for (int i = 0; i < 5; i++) feud.RecordMatch(Day);
            double sixth = BoxOffice.DrawOf(Match(a, b, feud), book, Day).Pull;

            Assert.True(sixth < first * 0.7, $"sixth {sixth:F3} vs first {first:F3}");
        }

        [Fact]
        public void ABeltOnTheLineDrawsMore_AndAPrestigiousOneMoreStill()
        {
            var a = TestRoster.Make("A");
            var b = TestRoster.Make("B");
            var titles = new TitleRegistry();
            titles.SeedDefaults("Test", Day);
            var belt = titles.Active.First(t => t.SideSize <= 1);

            var plain = Match(a, b);
            var title = Match(a, b);
            title.Plan.TitleAtStake = belt;

            Assert.True(belt.Prestige > 0);
            Assert.True(BoxOffice.DrawOf(title, null, Day).Pull > BoxOffice.DrawOf(plain, null, Day).Pull);
        }

        [Fact]
        public void TheMainEventSellsTheCard_TheFifthMatchSellsNobody()
        {
            var star = TestRoster.Make("Star", overness: 95);
            var rival = TestRoster.Make("Rival", overness: 92);
            var filler = Enumerable.Range(0, 8).Select(i => TestRoster.Make($"F{i}", overness: 40)).ToList();

            var top = BoxOffice.Draw([Match(star, rival), Match(filler[0], filler[1]), Match(filler[2], filler[3])], null, Day);
            var deeper = BoxOffice.Draw([Match(star, rival), Match(filler[0], filler[1]), Match(filler[2], filler[3]),
                                         Match(filler[4], filler[5]), Match(filler[6], filler[7])], null, Day);

            Assert.Equal(BoxOffice.CardPull(top), BoxOffice.CardPull(deeper), 10);
            Assert.Equal("Star", top[0].Headliners[0]);
        }

        [Fact]
        public void ASoldOutBuildingCountsWhoItTurnedAway()
        {
            var a = TestRoster.Make("A", overness: 98);
            var b = TestRoster.Make("B", overness: 97);
            var career = NewCareer(PromotionTier.Local, a, b);
            var c = TestRoster.Make("C", overness: 80);
            var d = TestRoster.Make("D", overness: 80);
            var e = TestRoster.Make("E", overness: 75);
            var g = TestRoster.Make("G", overness: 75);
            var show = Show(career, ShowType.PremiumEvent,
                Match(a, b, HotFeud(career.FeudBook, a, b)), Match(c, d), Match(e, g));

            var f = career.Projection(show);

            Assert.True(f.SoldOut);
            Assert.Equal(f.Capacity, f.Attendance);
            Assert.True(f.TurnedAway > 0);
        }

        // ── What it costs ────────────────────────────────────────────────────

        [Fact]
        public void OnlyThePeopleWhoWorkArePaid_AtTheirAskingPrice()
        {
            var star = TestRoster.Make("Star", overness: 95);
            var nobody = TestRoster.Make("Nobody", overness: 10);
            var benched = TestRoster.Make("Benched", overness: 95);
            var career = NewCareer(PromotionTier.Independent, star, nobody, benched);

            var f = career.Projection(Show(career, ShowType.PremiumEvent, Match(star, nobody)));

            Assert.True(TalentMarket.PerDateAsk(star) > 100 * TalentMarket.PerDateAsk(nobody));
            Assert.Equal(TalentMarket.PerDateAsk(star) + TalentMarket.PerDateAsk(nobody), f.Talent);
        }

        /// <summary>
        /// The tension the roster creates: every career has the same stars, and a small
        /// room cannot pay for a card of them. A main event of stars over a cheap undercard is
        /// the affordable way to sell a Local show.
        /// </summary>
        [Fact]
        public void ALocalRoomCannotAffordACardOfStars()
        {
            var stars = Enumerable.Range(0, 10).Select(i => TestRoster.Make($"S{i}", overness: 92)).ToArray();
            var career = NewCareer(PromotionTier.Local, stars);
            var card = Enumerable.Range(0, 5).Select(i => (ICardItem)Match(stars[2 * i], stars[2 * i + 1])).ToArray();

            var f = career.Projection(Show(career, ShowType.PremiumEvent, card));

            output.WriteLine($"fill {f.Fill:P0}, revenue {f.Revenue:N0}, talent {f.Talent:N0}, net {f.Net:N0}");
            Assert.True(f.Fill > 0.85);
            Assert.True(f.Net < 0, "Filling a room of 240 does not pay ten main eventers.");
        }

        /// <summary>
        /// At a television tier the rights fee carries the night, so booking moves a small
        /// share of the money — which is true to doc 09 §3.1, and is why renewal has to come
        /// next for booking to matter there.
        /// </summary>
        [Fact]
        public void TelevisionIsCarriedByTheRightsFee()
        {
            var a = TestRoster.Make("A");
            var b = TestRoster.Make("B");
            var career = NewCareer(PromotionTier.Global, a, b);

            var f = career.Projection(Show(career, ShowType.Television, Match(a, b)));

            Assert.True(f.MediaRights > f.Gate + f.Merchandise);
            Assert.True(f.Production > 0);
            Assert.True(f.Net > 0);
        }

        [Fact]
        public void TheRoadIsSoldTownByTown_AndPaysOnlyForNightsWorked()
        {
            var cast = Enumerable.Range(0, 6).Select(i => TestRoster.Make($"R{i}", overness: 60)).ToArray();
            var career = NewCareer(PromotionTier.SuperIndie, cast);
            var show = career.Schedule("Loop", Day, ShowType.HouseShow);
            show.Loop = new HouseShowLoop { Cast = cast.ToList(), Towns = 4 };

            var projected = career.Projection(show).Road!;
            var result = career.RunShow(show)!;
            var road = result.Finances!.Road!;

            Assert.Equal(4, road.Towns);
            Assert.Equal(projected.Gate, road.Gate);
            Assert.Equal(4 * road.AttendancePerNight * BoxOffice.PriceFor(BoxOffice.For(PromotionTier.SuperIndie), ShowType.HouseShow), road.Gate);
            Assert.Equal(result.Loop!.Workers.Sum(w => w.NightsWorked * TalentMarket.PerDateAsk(w.Wrestler)), road.Talent);

            // A night with no card sells no tickets of its own and moves no reputation.
            Assert.False(result.Finances.HadCard);
            Assert.Equal(CareerBooks.OpeningReputation, career.Books.Reputation);
        }

        /// <summary>
        /// A house show pairs its cast against each other. Priced as single names wrestling
        /// nobody, a run headlined by three upper-card stars drew 41% and lost money.
        ///
        /// Since prices are the same for every buyer, the run that pays is one the tier can
        /// afford: the same towns worked by upper-card stars sell more tickets and lose money,
        /// because a Super Indie cannot pay six of them $10k a night.
        /// </summary>
        [Fact]
        public void ARoadTheTierCanAffordFillsItsTownsAndPays_ARoadOfStarsDoesNot()
        {
            (RoadTakings Road, decimal Ask) Run(double top)
            {
                var cast = Enumerable.Range(0, 6).Select(i => TestRoster.Make($"R{i}", overness: top - i)).ToArray();
                var career = NewCareer(PromotionTier.SuperIndie, cast);
                var show = career.Schedule("Loop", Day, ShowType.HouseShow);
                show.Loop = new HouseShowLoop { Cast = cast.ToList(), Towns = 3 };
                return (career.Projection(show).Road!, TalentMarket.PerDateAsk(cast[0]));
            }

            var (affordable, cheap) = Run(66);
            var (stars, dear) = Run(86);

            output.WriteLine($"affordable: {affordable.AttendancePerNight}/{affordable.CapacityPerNight}, net {affordable.Net:N0} (top asks {cheap:N0})");
            output.WriteLine($"stars:      {stars.AttendancePerNight}/{stars.CapacityPerNight}, net {stars.Net:N0} (top asks {dear:N0})");

            Assert.True(affordable.AttendancePerNight > 0.7 * affordable.CapacityPerNight);
            Assert.True(affordable.Net > 0);
            Assert.Equal("R0 vs R1", affordable.Headliners[0]);

            Assert.True(stars.AttendancePerNight > affordable.AttendancePerNight, "Stars sell more tickets…");
            Assert.True(stars.Net < 0, "…and cost more than the towns can pay.");
        }

        // ── Calibration ──────────────────────────────────────────────────────

        /// <summary>
        /// Five singles matches between same-division people picked at random from the tier's
        /// opening roster — the card a promotion of that size can afford — nothing built and
        /// nothing on the line.
        /// </summary>
        private static List<List<ICardItem>> TypicalCards(IReadOnlyList<Wrestler> pool, int count = 2_000)
        {
            var cards = new List<List<ICardItem>>();
            var divisions = pool.GroupBy(w => w.Division).Where(g => g.Count() >= 2).Select(g => g.ToList()).ToList();

            for (int i = 0; i < count; i++)
            {
                var rng = new Random(StableSeed.From("typical-card", i));
                var card = new List<ICardItem>();
                for (int m = 0; m < 5; m++)
                {
                    var division = divisions[rng.Next(divisions.Count)];
                    var a = division[rng.Next(division.Count)];
                    Wrestler b;
                    do b = division[rng.Next(division.Count)]; while (ReferenceEquals(a, b));
                    card.Add(Match(a, b));
                }
                cards.Add(card);
            }
            return cards;
        }

        /// <summary>The tier's opening roster, from the shipped roster, and the contracts it opens with.</summary>
        private static (List<Wrestler> Pool, List<Contract> Contracts) OpeningRosterFor(PromotionTier tier)
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var contracts = new TierOpeningRoster().Open(new Promotion { Tier = tier }, roster, Day).ToList();
            var ids = contracts.Select(c => c.WrestlerId).ToHashSet();
            return (roster.Where(w => ids.Contains(w.Id)).ToList(), contracts);
        }

        /// <summary>
        /// <see cref="BoxOffice.TypicalCardPullFor"/> is the median of each tier's corpus. A
        /// roster or price change moves it; when this fails, re-measure and update the table.
        /// </summary>
        [Theory]
        [InlineData(PromotionTier.Local)]
        [InlineData(PromotionTier.Independent)]
        [InlineData(PromotionTier.SuperIndie)]
        [InlineData(PromotionTier.Established)]
        [InlineData(PromotionTier.National)]
        [InlineData(PromotionTier.Global)]
        public void ATypicalCardFillsTypically_AndThatIsMeasuredNotAsserted(PromotionTier tier)
        {
            var (pool, _) = OpeningRosterFor(tier);
            var pulls = TypicalCards(pool)
                .Select(c => BoxOffice.CardPull(BoxOffice.Draw(c, null, Day)))
                .OrderBy(p => p)
                .ToList();
            double median = pulls[pulls.Count / 2];

            output.WriteLine($"{tier}: roster {pool.Count}, median card pull {median:F4} (n = {pulls.Count}); constant {BoxOffice.TypicalCardPullFor(tier):F4}");

            Assert.Equal(BoxOffice.TypicalCardPullFor(tier), median, 3);

            // And the claim the table exists for: that card, at an opening reputation, fills
            // the building to TypicalFill.
            Assert.Equal((int)Math.Round(10_000 * BoxOffice.TypicalFill),
                         BoxOffice.Demand(10_000, BoxOffice.TypicalCardPullFor(tier), CareerBooks.OpeningReputation, tier));
        }

        /// <summary>
        /// A promotion booking whoever it has should land where doc 09 §4 puts its tier —
        /// measured on the typical card from its opening roster, at the tier's usual cadence,
        /// with the days between shows charged their running costs and their guarantees.
        ///
        ///   Global       Tier 0   30–40% margins in strong years
        ///   National     Tier 1   costs that can exceed revenue — about break-even
        ///   Established  Tier 2   near break-even or modest profit
        ///   SuperIndie   Tier 3   real but small profit
        ///   Independent  Tier 4   thin
        ///   Local        Tier 5   thin, often subsidised
        ///
        /// A typical card is the floor of competent booking, so each band is where a booker
        /// who builds nothing ends up; building stories is how a promotion gets above it.
        /// </summary>
        [Theory]
        [InlineData(PromotionTier.Local,       -0.10, 0.25)]
        [InlineData(PromotionTier.Independent, -0.10, 0.25)]
        [InlineData(PromotionTier.SuperIndie,   0.00, 0.30)]
        [InlineData(PromotionTier.Established, -0.05, 0.25)]
        [InlineData(PromotionTier.National,    -0.15, 0.15)]
        [InlineData(PromotionTier.Global,       0.25, 0.45)]
        public void ATypicalCardLandsWhereTheReferencePutsTheTier(PromotionTier tier, double low, double high)
        {
            var (pool, contracts) = OpeningRosterFor(tier);
            var guaranteed = contracts.Where(c => c.IsGuaranteed).Select(c => c.WrestlerId).ToHashSet();
            var promotion = new Promotion { Tier = tier };
            var type = promotion.HasTelevision ? ShowType.Television : ShowType.PremiumEvent;
            int interval = promotion.TypicalShowIntervalDays;
            decimal standing = (BoxOffice.For(tier).DailyOverhead + contracts.Sum(c => c.DailyCost)) * interval;

            var margins = new List<double>();
            foreach (var card in TypicalCards(pool, 400))
            {
                var show = new ScheduledShow { Date = Day, Type = type, Attendance = promotion.TypicalAttendanceFor(type) };
                show.Card.AddRange(card);
                var f = BoxOffice.Night(show, promotion, CareerBooks.OpeningReputation, null,
                                        guaranteed: w => guaranteed.Contains(w.Id));
                margins.Add((double)((f.Net - standing) / f.Revenue));
            }
            margins.Sort();
            double median = margins[margins.Count / 2];

            output.WriteLine($"{tier}: roster {pool.Count}, payroll {contracts.Sum(c => c.AnnualGuarantee):N0}/yr, " +
                             $"median margin {median:P1}  (10th {margins[margins.Count / 10]:P1}, 90th {margins[margins.Count * 9 / 10]:P1})");

            Assert.InRange(median, low, high);
        }

        // ── Saves ────────────────────────────────────────────────────────────

        [Fact]
        public void BooksAndTakingsSurviveASave()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var a = roster[0];
            var b = roster.First(w => w.Division == a.Division && w != a);
            var career = NewCareer(PromotionTier.SuperIndie, roster.ToArray());
            var show = Show(career, ShowType.PremiumEvent, Match(a, b));
            career.RunShow(show);

            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career), DataLoaders.LoadEmbeddedWrestlers());
            var before = show.Result!.Finances!;
            var after = loaded.Shows.Single(s => s.Id == show.Id).Result!.Finances!;

            Assert.Equal(career.Books.Cash, loaded.Books.Cash);
            Assert.Equal(career.Books.Reputation, loaded.Books.Reputation, 3);
            Assert.Equal(career.Books.OpenedOn, loaded.Books.OpenedOn);
            Assert.Equal(before.Net, after.Net);
            Assert.Equal(before.Attendance, after.Attendance);
            Assert.Equal(before.Draws[0].Label, after.Draws[0].Label);
        }

        [Fact]
        public void ASaveFromBeforeMoney_OpensItsBooksOnTheDayItIsLoaded()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var career = NewCareer(PromotionTier.Established, roster.ToArray());
            career.CurrentDate = Day.AddDays(40);

            var dto = SaveSerializer.ToDto(career);
            dto.Books = null;
            var json = System.Text.Json.JsonSerializer.Serialize(dto);

            var loaded = SaveSerializer.FromJson(json, DataLoaders.LoadEmbeddedWrestlers());

            Assert.Equal(BoxOffice.For(PromotionTier.Established).OpeningCash, loaded.Books.Cash);
            Assert.Equal(Day.AddDays(40), loaded.Books.OpenedOn);
        }
    }
}
