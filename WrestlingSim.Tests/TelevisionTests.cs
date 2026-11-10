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
    /// The television deal: viewers per episode, a term, and a renewal that pays next term for
    /// what this one drew. docs/wrestling-reference/08-television-and-media.md.
    /// </summary>
    public class TelevisionTests
    {
        private readonly ITestOutputHelper output;
        public TelevisionTests(ITestOutputHelper output) => this.output = output;

        private static readonly DateOnly Day = new(2025, 3, 3);

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

        /// <summary>A deal with one episode a week across its whole term, at the given audience.</summary>
        private static TvDeal Term(PromotionTier tier, Func<DateOnly, int> viewers)
        {
            var deal = Television.Open(tier, Day);
            for (var d = Day; d < deal.End; d = d.AddDays(7))
                deal.Episodes.Add(new EpisodeRating { Date = d, Viewers = viewers(d) });
            return deal;
        }

        // ── The deal exists where television does ────────────────────────────

        [Theory]
        [InlineData(PromotionTier.Established, 3)]
        [InlineData(PromotionTier.National, 4)]
        [InlineData(PromotionTier.Global, 5)]
        public void ATelevisedPromotionOpensWithADealAndATerm(PromotionTier tier, int years)
        {
            var career = NewCareer(tier);

            Assert.NotNull(career.Television);
            Assert.Equal(Day.AddYears(years), career.Television!.End);
            Assert.Equal(BoxOffice.For(tier).RightsPerEpisode, career.Television.FeePerEpisode);
        }

        [Fact]
        public void BelowEstablishedThereIsNoDealAndNobodyWatching()
        {
            var a = TestRoster.Make("A");
            var b = TestRoster.Make("B");
            var career = NewCareer(PromotionTier.SuperIndie, a, b);

            var f = career.Projection(career.Schedule("Show", Day, ShowType.PremiumEvent));

            Assert.Null(career.Television);
            Assert.Equal(0, f.TuneIn);
        }

        // ── Who watched ──────────────────────────────────────────────────────

        /// <summary>
        /// Tune-in is sold on the card, the same as the gate; the show's quality then keeps or
        /// loses the audience, and that kept audience is what the network counts.
        /// </summary>
        [Fact]
        public void ViewersAreTheTuneInKeptByTheShow_AndTheDealCountsThem()
        {
            var a = TestRoster.Make("A", overness: 80);
            var b = TestRoster.Make("B", overness: 78);
            var career = NewCareer(PromotionTier.National, a, b);
            var show = career.Schedule("Weekly", Day, ShowType.Television);
            show.Card.Add(Match(a, b));

            int projected = career.Projection(show).TuneIn;
            var result = career.RunShow(show)!;
            var f = result.Finances!;

            Assert.Equal(projected, f.TuneIn);
            Assert.Equal((int)Math.Round(f.TuneIn * Television.Retention(result.OverallRating)), f.Viewers);
            Assert.Equal(f.Viewers, career.Television!.Episodes.Single().Viewers);
        }

        [Fact]
        public void AnOnlyPremiumEventIsPaidForButNotRated()
        {
            var a = TestRoster.Make("A");
            var b = TestRoster.Make("B");
            var career = NewCareer(PromotionTier.Established, a, b);
            var show = career.Schedule("Big One", Day, ShowType.PremiumEvent);
            show.Card.Add(Match(a, b));

            var f = career.RunShow(show)!.Finances!;

            Assert.Equal(2 * career.Television!.FeePerEpisode, f.MediaRights);
            Assert.Empty(career.Television.Episodes);
        }

        /// <summary>
        /// The baseline is what a typical card gets on average, so it is a claim about tune-in
        /// and retention together: the typical card's tune-in, kept at typical retention.
        /// </summary>
        [Theory]
        [InlineData(PromotionTier.Established)]
        [InlineData(PromotionTier.National)]
        [InlineData(PromotionTier.Global)]
        public void ATypicalCardAveragesTheTiersBaseline(PromotionTier tier)
        {
            int tuneIn = Television.TuneIn(tier, BoxOffice.TypicalCardPull, CareerBooks.OpeningReputation);
            Assert.InRange(tuneIn * Television.TypicalRetention,
                           Television.BaselineViewers(tier) - 1, Television.BaselineViewers(tier) + 1);
        }

        [Fact]
        public void AGreatShowKeepsItsAudience_ABadOneLosesIt()
        {
            Assert.True(Television.Retention(80) > 1.0, "A great show picks people up.");
            Assert.Equal(Television.TypicalRetention, Television.Retention(50), 10);
            Assert.True(Television.Retention(20) < 0.85);
        }

        [Fact]
        public void AStrongCardDrawsMoreViewers_ButLessElasticallyThanTickets()
        {
            const PromotionTier tier = PromotionTier.National;
            double strong = BoxOffice.TypicalCardPull * 1.5;

            double tvRatio = Television.TuneIn(tier, strong, 50) / (double)Television.TuneIn(tier, BoxOffice.TypicalCardPull, 50);
            double gateRatio = BoxOffice.Demand(100_000, strong, 50) / (double)BoxOffice.Demand(100_000, BoxOffice.TypicalCardPull, 50);

            Assert.True(tvRatio > 1.2);
            Assert.True(tvRatio < gateRatio, "A television audience is a habit; it moves less than a ticket buyer.");
        }

        // ── The renewal ──────────────────────────────────────────────────────

        [Fact]
        public void HoldingTheBaselineRenewsAtTheMarketsDrift()
        {
            const PromotionTier tier = PromotionTier.National;
            var deal = Term(tier, _ => Television.BaselineViewers(tier));

            Assert.Equal(Television.RightsInflation, Television.RenewalMultiple(deal, tier), 6);
        }

        [Fact]
        public void ABiggerAudienceRenewsHigher_ASmallerOneLower()
        {
            const PromotionTier tier = PromotionTier.National;
            int baseline = Television.BaselineViewers(tier);

            double up   = Television.RenewalMultiple(Term(tier, _ => (int)(baseline * 1.4)), tier);
            double flat = Television.RenewalMultiple(Term(tier, _ => baseline), tier);
            double down = Television.RenewalMultiple(Term(tier, _ => (int)(baseline * 0.7)), tier);

            output.WriteLine($"1.4× → {up:F3}   1.0× → {flat:F3}   0.7× → {down:F3}");
            Assert.True(up > flat && flat > down);
            Assert.True(down < 1.0, "Losing a third of the audience should cost money, not just growth.");
        }

        /// <summary>Doc 08 §3: the twelve months before expiry are the ones that count.</summary>
        [Fact]
        public void TheFinalYearCountsDouble()
        {
            const PromotionTier tier = PromotionTier.Established;
            int baseline = Television.BaselineViewers(tier);
            var finalYear = Day.AddYears(Television.TermYears(tier) - 1);

            var lateSurge  = Term(tier, d => d >= finalYear ? (int)(baseline * 1.3) : baseline);
            var earlySurge = Term(tier, d => d <  Day.AddYears(1) ? (int)(baseline * 1.3) : baseline);

            Assert.True(Television.RenewalMultiple(lateSurge, tier) > Television.RenewalMultiple(earlySurge, tier));
        }

        /// <summary>Doc 08 §2.2: the network buys a number it can sell against.</summary>
        [Fact]
        public void ASwingingAudienceRenewsBelowASteadyOne()
        {
            const PromotionTier tier = PromotionTier.National;
            int baseline = Television.BaselineViewers(tier);
            int week = 0;

            var steady   = Term(tier, _ => baseline);
            var swinging = Term(tier, _ => (week++ % 2 == 0) ? (int)(baseline * 1.5) : (int)(baseline * 0.5));

            Assert.Equal(1.0, Television.Consistency(steady), 6);
            Assert.True(Television.Consistency(swinging) < 0.9);
            Assert.True(Television.RenewalMultiple(swinging, tier) < Television.RenewalMultiple(steady, tier));
        }

        [Fact]
        public void ATermWithNoTelevisionRenewsAtTheFloor()
        {
            var deal = Television.Open(PromotionTier.Global, Day);
            Assert.Equal(Television.RenewalFloor, Television.RenewalMultiple(deal, PromotionTier.Global));
        }

        /// <summary>The clock renews the deal when the term runs out, and the next term starts clean.</summary>
        [Fact]
        public void TheClockRenewsTheDealAtTheEndOfItsTerm()
        {
            var career = NewCareer(PromotionTier.Established);
            var deal = career.Television!;
            int baseline = Television.BaselineViewers(PromotionTier.Established);
            for (var d = Day; d < deal.End; d = d.AddDays(7))
                deal.Episodes.Add(new EpisodeRating { Date = d, Viewers = (int)(baseline * 1.25) });

            decimal before = deal.FeePerEpisode;
            double expected = Television.RenewalMultiple(deal, PromotionTier.Established);
            var end = deal.End;

            while (career.CurrentDate < end) Assert.True(career.AdvanceOneDay());

            var term = Assert.Single(deal.History);
            Assert.Equal(expected, term.RenewedAt, 6);
            Assert.Equal(Television.RenewedFee(before, expected), deal.FeePerEpisode);
            Assert.Equal(0m, deal.FeePerEpisode % 1_000m);
            Assert.Equal(end, deal.Start);
            Assert.Equal(end.AddYears(3), deal.End);
            Assert.Empty(deal.Episodes);
            Assert.Same(term, career.LastRenewal);
        }

        /// <summary>
        /// The new fee reaches the books: the episode after a renewal is paid at the renewed
        /// rate. Without this the renewal would be a number on a dashboard and nothing else.
        /// </summary>
        [Fact]
        public void TheRenewedFeeIsWhatTheNextEpisodePays()
        {
            var a = TestRoster.Make("A");
            var b = TestRoster.Make("B");
            var career = NewCareer(PromotionTier.Established, a, b);
            career.Television!.FeePerEpisode = 123_000m;

            var show = career.Schedule("Weekly", Day, ShowType.Television);
            show.Card.Add(Match(a, b));

            Assert.Equal(123_000m, career.Projection(show).MediaRights);
        }

        // ── Saves ────────────────────────────────────────────────────────────

        [Fact]
        public void TheDealSurvivesASave()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var career = NewCareer(PromotionTier.Global, roster.ToArray());
            var deal = career.Television!;
            deal.Episodes.Add(new EpisodeRating { Date = Day, Viewers = 1_234_567 });
            deal.History.Add(new TvTerm { Start = Day.AddYears(-5), End = Day, FeePerEpisode = 3_000_000m, Episodes = 260, AverageViewers = 1_900_000, RenewedAt = 1.21 });

            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career), DataLoaders.LoadEmbeddedWrestlers()).Television!;

            Assert.Equal(deal.FeePerEpisode, loaded.FeePerEpisode);
            Assert.Equal(deal.End, loaded.End);
            Assert.Equal(1_234_567, loaded.Episodes.Single().Viewers);
            Assert.Equal(1.21, loaded.History.Single().RenewedAt, 6);
        }

        [Fact]
        public void ATelevisedSaveFromBeforeDeals_OpensATermOnTheDayItIsLoaded()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var career = NewCareer(PromotionTier.National, roster.ToArray());
            career.CurrentDate = Day.AddDays(100);

            var dto = SaveSerializer.ToDto(career);
            dto.Television = null;
            var loaded = SaveSerializer.FromJson(System.Text.Json.JsonSerializer.Serialize(dto), DataLoaders.LoadEmbeddedWrestlers());

            Assert.NotNull(loaded.Television);
            Assert.Equal(Day.AddDays(100), loaded.Television!.Start);
        }
    }
}
