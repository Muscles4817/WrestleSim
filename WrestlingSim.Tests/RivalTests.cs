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
    /// Competing promotions. docs/wrestling-reference/01, 10 §2.6, 11 §7–8, 13 §3.2, 26 §3.2.
    /// </summary>
    public class RivalTests
    {
        private readonly ITestOutputHelper output;
        public RivalTests(ITestOutputHelper output) => this.output = output;

        private static readonly DateOnly Day = new(2025, 1, 6);

        private static Career World(PromotionTier tier)
        {
            var career = new Career
            {
                Promotion   = new Promotion { Name = "Player Wrestling", Tier = tier },
                StartDate   = Day,
                CurrentDate = Day,
                Roster      = DataLoaders.LoadEmbeddedWrestlers()
            };
            career.OpenBooks(Day);
            career.Titles.SeedDefaults(career.Promotion.Name, Day);
            Engine.World.Open(career, Day);
            return career;
        }

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

        private static RivalPromotion RivalAt(Career career, PromotionTier tier) =>
            career.Rivals.Single(r => r.Promotion.Tier == tier);

        // ── The world ────────────────────────────────────────────────────────

        [Theory]
        [InlineData(PromotionTier.Local, 4)]
        [InlineData(PromotionTier.Established, 4)]
        [InlineData(PromotionTier.National, 3)]
        [InlineData(PromotionTier.Global, 3)]
        public void TheDefaultWorldIsTheIndustrysShape_WithThePlayerInTheirTiersPlace(PromotionTier tier, int rivals)
        {
            var career = World(tier);

            Assert.Equal(rivals, career.Rivals.Count);
            Assert.DoesNotContain(career.Rivals, r => r.Promotion.Tier == tier);
            Assert.All(career.Rivals.Where(r => r.Promotion.HasTelevision), r => Assert.NotNull(r.Television));
        }

        /// <summary>Exclusive means exclusive: nobody opens on two guarantees.</summary>
        [Fact]
        public void NobodyOpensOnTwoGuarantees()
        {
            var career = World(PromotionTier.Established);
            var held = career.Contracts.Where(c => c.IsGuaranteed).Select(c => c.WrestlerId)
                .Concat(career.Rivals.SelectMany(r => r.Contracts).Where(c => c.IsGuaranteed).Select(c => c.WrestlerId))
                .ToList();

            Assert.Equal(held.Count, held.Distinct().Count());
        }

        /// <summary>The biggest promotion signs first, so the global major opens with the stars.</summary>
        [Fact]
        public void TheBiggestPromotionSignsFirst()
        {
            var career = World(PromotionTier.Local);
            double Average(IEnumerable<Contract> cs) =>
                cs.Select(c => career.FindWrestler(c.WrestlerId)!.Overness).Average();

            double global = Average(RivalAt(career, PromotionTier.Global).Contracts);
            double national = Average(RivalAt(career, PromotionTier.National).Contracts);
            double player = Average(career.Contracts);
            output.WriteLine($"average overness: global {global:F1}, national {national:F1}, player (Local) {player:F1}");

            Assert.True(global > national && national > player);
        }

        // ── Exclusivity ──────────────────────────────────────────────────────

        [Fact]
        public void ARivalsSignedWrestlerCannotWorkForThePlayer()
        {
            var career = World(PromotionTier.Established);
            var theirs = career.FindWrestler(RivalAt(career, PromotionTier.Global).Contracts[0].WrestlerId)!;
            var mine = career.FindWrestler(career.Contracts[0].WrestlerId)!;
            var show = career.Schedule("Weekly", Day, ShowType.Television);
            show.Card.Add(Match(mine, theirs));

            Assert.Contains("Vantage", career.Unavailable(theirs));
            Assert.Single(career.UnavailableOnCard(show));
            Assert.Null(career.RunShow(show));
            Assert.False(show.HasRun);
        }

        /// <summary>Doc 13 §3.2: released means ninety days out of the business, for everybody.</summary>
        [Fact]
        public void AReleaseStartsANinetyDayNonCompete()
        {
            var career = World(PromotionTier.Established);
            var w = career.FindWrestler(career.Contracts.First(c => c.IsGuaranteed).WrestlerId)!;

            career.Release(w);

            Assert.NotNull(career.NonCompeteOf(w));
            Assert.False(career.Sign(w, 10_000_000m, 3).Accepted);
            Assert.Contains("non-compete", career.Unavailable(w));

            for (int i = 0; i < TalentMarket.ReleasePayoutDays; i++) career.AdvanceOneDay();
            Assert.Null(career.NonCompeteOf(w));
        }

        // ── Competing offers ─────────────────────────────────────────────────

        /// <summary>
        /// Doc 10 §6.2: a competing offer is the only real leverage. A rival bids for the
        /// player's star in the last sixty days; re-signing means matching it; not matching it
        /// means losing them — with the belt vacated on the way out.
        /// </summary>
        [Fact]
        public void ARivalBidsForAnExpiringStar_AndTakesThemIfThePlayerDoesNotMatch()
        {
            var career = World(PromotionTier.Established);
            var star = career.Contracts.Select(c => career.FindWrestler(c.WrestlerId)!).MaxBy(w => w.EffectiveOverness)!;
            var contract = career.ContractOf(star)!;
            contract.End = Day.AddDays(30);

            var title = career.Titles.Active.First(t => t.SideSize <= 1);
            title.Lineage.Add(new TitleReign { Champions = [star], ReignNumber = 1, Won = Day });

            for (int i = 0; i < 8; i++) career.AdvanceOneDay();
            var bid = career.BestRivalBid(star);
            Assert.NotNull(bid);
            Assert.Contains(career.News, n => n.AboutYou && n.Text.Contains(star.RingName));

            var refused = career.Sign(star, bid!.Annual - 1_000m, 3);
            Assert.False(refused.Accepted);
            Assert.Contains("Match it", refused.Reason);

            while (career.CurrentDate < Day.AddDays(31)) career.AdvanceOneDay();

            Assert.Null(career.ContractOf(star));
            Assert.Equal(bid.Bidder, career.SignedTo(star)!.Id);
            Assert.True(title.IsVacant);
        }

        [Fact]
        public void MatchingTheBidKeepsThem()
        {
            var career = World(PromotionTier.Established);
            var star = career.Contracts.Select(c => career.FindWrestler(c.WrestlerId)!).MaxBy(w => w.EffectiveOverness)!;
            career.ContractOf(star)!.End = Day.AddDays(30);

            for (int i = 0; i < 8; i++) career.AdvanceOneDay();
            var bid = career.BestRivalBid(star)!;

            Assert.True(career.Sign(star, bid.Annual, 3).Accepted);
            Assert.Null(career.BestRivalBid(star));
            Assert.Equal(career.CurrentDate.AddYears(3), career.ContractOf(star)!.End);
        }

        /// <summary>
        /// The other direction: an offer to a rival's wrestler in their last sixty days is lodged,
        /// and if it beats what the rival will pay to keep them, they arrive when the deal ends.
        /// </summary>
        [Fact]
        public void APlayerOfferBeatsARivalsAndTheWrestlerArrivesAtExpiry()
        {
            var career = World(PromotionTier.National);
            var rival = RivalAt(career, PromotionTier.Global);
            var theirs = rival.Contracts.Select(c => career.FindWrestler(c.WrestlerId)!).MaxBy(w => w.EffectiveOverness)!;
            rival.ContractOf(theirs)!.End = Day.AddDays(20);

            var generous = career.GuaranteeAsk(theirs, 3) * 2;
            var lodged = career.Sign(theirs, generous, 3);

            Assert.True(lodged.Accepted && lodged.Pending);
            Assert.Null(career.ContractOf(theirs));

            while (career.CurrentDate < Day.AddDays(21)) career.AdvanceOneDay();

            Assert.Equal(generous, career.ContractOf(theirs)!.AnnualGuarantee);
            Assert.Null(career.SignedTo(theirs));
        }

        [Fact]
        public void NobodyCanBeOfferedForUntilTheLastSixtyDays()
        {
            var career = World(PromotionTier.National);
            var rival = RivalAt(career, PromotionTier.Global);
            var theirs = career.FindWrestler(rival.Contracts.First(c => c.DaysLeft(Day) > 365).WrestlerId)!;

            var result = career.Sign(theirs, 100_000_000m, 3);

            Assert.False(result.Accepted);
            Assert.Contains("Offers open", result.Reason);
        }

        // ── The rivals' business ─────────────────────────────────────────────

        [Fact]
        public void RivalsRunTheirShowsOnTheClock()
        {
            var career = World(PromotionTier.Local);
            var global = RivalAt(career, PromotionTier.Global);
            var cash = global.Books.Cash;

            for (int i = 0; i < 30; i++) career.AdvanceOneDay();

            Assert.InRange(global.Nights.Count, 3, 5);     // weekly: one brand, one show a week
            Assert.NotEqual(cash, global.Books.Cash);
            Assert.NotEmpty(global.Television!.Episodes);
            Assert.All(global.Nights, n => Assert.InRange(n.Rating, 5, 95));

            // On screen for somebody, so not forgotten.
            var worked = career.FindWrestler(global.Contracts[0].WrestlerId)!;
            Assert.NotNull(worked.LastAppearance);
        }

        /// <summary>
        /// What becomes of the wealthy fan's promotion, **measured and not asserted**.
        ///
        /// Doc 01 §7 says such promotions "typically" burn out in 18–36 months. This test used to
        /// assert that band, and twice a constant was re-solved to keep it passing. The owner
        /// ruled that out: an outcome the world is tuned to produce is scripted, however
        /// indirectly. A wealthy fan's promotion should be able to survive in a world that lets
        /// it. So this prints what happened for the design record, and when the figure is far
        /// from the reference the thing to do is look at why — not to turn a dial.
        ///
        /// What it does assert is only that the world keeps running and the news says so if
        /// somebody closes.
        /// </summary>
        [Fact]
        public void WhatBecomesOfTheWealthyFansPromotion_IsMeasuredNotForced()
        {
            var career = World(PromotionTier.Local);
            var ironclad = RivalAt(career, PromotionTier.National);
            var vantage = RivalAt(career, PromotionTier.Global);
            Assert.Equal(FounderMotive.MarkMoney, ironclad.Motive);

            for (int year = 1; year <= 5; year++)
            {
                for (int i = 0; i < 365 && ironclad.IsActive; i++) career.AdvanceOneDay();
                output.WriteLine($"year {year}: {ironclad.Name} cash {ironclad.Books.Cash:N0}, roster {ironclad.Contracts.Count}, " +
                                 $"reputation {ironclad.Books.Reputation:F1}; {vantage.Name} cash {vantage.Books.Cash:N0}");
                if (!ironclad.IsActive) break;
            }

            if (ironclad.Folded is { } closed)
            {
                output.WriteLine($"{ironclad.Name} closed after {(closed.DayNumber - Day.DayNumber) / 30.4:F0} months (doc 01 §7: typically 18–36)");
                Assert.Contains(career.News, n => n.Text.Contains("has closed"));
            }
            else output.WriteLine($"{ironclad.Name} is still running after five years (doc 01 §7: typically 18–36 months)");

            Assert.True(career.Rivals.Any(r => r.IsActive));
        }

        /// <summary>
        /// Doc 11 §8 and doc 26 §3.2: wages and audiences follow the number of credible
        /// employers. Two is the baseline; a fold is a monopoly, and winning the war quietly
        /// costs the winner.
        /// </summary>
        [Fact]
        public void CompetitionMovesWagesAndAudiences_AndAFoldCostsTheWinner()
        {
            var career = World(PromotionTier.Local);
            var w = career.Roster[0];
            var before = career.PerDateAsk(w);

            Assert.Equal(1.0, career.WageFactor, 10);
            Assert.Equal(1.0, career.Interest, 10);

            RivalAt(career, PromotionTier.National).Folded = Day;

            Assert.True(career.WageFactor < 1.0);
            Assert.True(career.Interest < 1.0);
            Assert.True(career.PerDateAsk(w) < before);
        }

        [Fact]
        public void ACareerWithNoWorldIsNotAMonopoly()
        {
            var career = new Career { Promotion = new Promotion { Tier = PromotionTier.Established }, StartDate = Day, CurrentDate = Day };
            Assert.Equal(1.0, career.WageFactor);
            Assert.Equal(1.0, career.Interest);
        }

        // ── Saves ────────────────────────────────────────────────────────────

        [Fact]
        public void TheWorldSurvivesASave()
        {
            var career = World(PromotionTier.Established);
            for (int i = 0; i < 20; i++) career.AdvanceOneDay();
            var w = career.FindWrestler(career.Contracts[0].WrestlerId)!;
            career.Release(w);

            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career), DataLoaders.LoadEmbeddedWrestlers());

            Assert.Equal(career.Rivals.Count, loaded.Rivals.Count);
            for (int i = 0; i < career.Rivals.Count; i++)
            {
                Assert.Equal(career.Rivals[i].Name, loaded.Rivals[i].Name);
                Assert.Equal(career.Rivals[i].Books.Cash, loaded.Rivals[i].Books.Cash);
                Assert.Equal(career.Rivals[i].Contracts.Count, loaded.Rivals[i].Contracts.Count);
                Assert.Equal(career.Rivals[i].NextShow, loaded.Rivals[i].NextShow);
                Assert.Equal(career.Rivals[i].Nights.Count, loaded.Rivals[i].Nights.Count);
            }
            Assert.Equal(career.NonCompetes.Count, loaded.NonCompetes.Count);
            Assert.Equal(career.News.Count, loaded.News.Count);
        }

        [Fact]
        public void ASaveFromBeforeRivals_GetsTheDefaultWorldAndKeepsItsRoster()
        {
            var career = World(PromotionTier.Established);
            var mine = career.Contracts.Select(c => c.WrestlerId).ToHashSet();

            var dto = SaveSerializer.ToDto(career);
            dto.Rivals = null;
            var loaded = SaveSerializer.FromJson(System.Text.Json.JsonSerializer.Serialize(dto), DataLoaders.LoadEmbeddedWrestlers());

            Assert.Equal(mine, loaded.Contracts.Select(c => c.WrestlerId).ToHashSet());
            Assert.Equal(4, loaded.Rivals.Count);
            Assert.DoesNotContain(loaded.Rivals.SelectMany(r => r.Contracts).Where(c => c.IsGuaranteed), c => mine.Contains(c.WrestlerId));
        }
    }
}
