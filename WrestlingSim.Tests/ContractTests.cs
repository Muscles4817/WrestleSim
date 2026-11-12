using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.Person;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Contracts and the market. docs/wrestling-reference/10, 11 and 13.
    /// </summary>
    public class ContractTests
    {
        private readonly ITestOutputHelper output;
        public ContractTests(ITestOutputHelper output) => this.output = output;

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

        // ── The price ────────────────────────────────────────────────────────

        /// <summary>
        /// The anchors the curve is built on, and doc 29 §4.1's bands for a year of it. Pay in
        /// wrestling is exponential; the top of the card earns three hundred times the bottom.
        /// </summary>
        [Fact]
        public void TheAskingPriceFollowsHowOverTheyAre()
        {
            decimal At(double o) => TalentMarket.PerDateAsk(TestRoster.Make($"o{o}", overness: o));

            Assert.InRange(At(25), 90m, 110m);
            Assert.InRange(At(95), 28_000m, 32_000m);
            Assert.True(At(50) < At(70) && At(70) < At(85));

            // A year's guarantee lands in the reported bands.
            decimal Year(double o) => TalentMarket.GuaranteeAsk(TestRoster.Make($"y{o}", overness: o), 3);
            Assert.InRange(Year(95), 3_000_000m, 5_000_000m);   // top star
            Assert.InRange(Year(70), 300_000m, 700_000m);       // mid-card
        }

        /// <summary>Doc 11 §7.1: a hot run is when the price goes up. Momentum moves it.</summary>
        [Fact]
        public void AHotRunRaisesThePrice()
        {
            var cold = TestRoster.Make("Cold", overness: 70);
            var hot = TestRoster.Make("Hot", overness: 70);
            hot.Momentum = 80;

            Assert.True(TalentMarket.PerDateAsk(hot) > TalentMarket.PerDateAsk(cold));
        }

        [Fact]
        public void ALongerTermBuysALowerAnnualFigure()
        {
            var w = TestRoster.Make("W", overness: 75);
            Assert.True(TalentMarket.GuaranteeAsk(w, 5) < TalentMarket.GuaranteeAsk(w, 1));
        }

        [Fact]
        public void ThePriceIsTheSameWhoeverIsBuying()
        {
            var w = TestRoster.Make("W", overness: 80);
            var local = NewCareer(PromotionTier.Local, w);
            var global = NewCareer(PromotionTier.Global, w);

            var l = local.Projection(Show(local, ShowType.PremiumEvent, w, TestRoster.Make("X", overness: 80)));
            var g = global.Projection(Show(global, ShowType.PremiumEvent, w, TestRoster.Make("Y", overness: 80)));

            Assert.Equal(l.Talent, g.Talent);
        }

        private static ScheduledShow Show(Career career, ShowType type, Wrestler a, Wrestler b)
        {
            var show = career.Schedule("Show", career.CurrentDate, type);
            show.Card.Add(Match(a, b));
            return show;
        }

        // ── Signing ──────────────────────────────────────────────────────────

        [Fact]
        public void NobodySignsForLessThanTheyAreWorth()
        {
            var w = TestRoster.Make("W", overness: 75);
            var career = NewCareer(PromotionTier.National, w);
            var ask = TalentMarket.GuaranteeAsk(w, 3);

            var low = career.Sign(w, ask - 1_000m, 3);
            Assert.False(low.Accepted);
            Assert.Contains("$" + ask.ToString("N0"), low.Reason);
            Assert.Null(career.ContractOf(w));

            var fair = career.Sign(w, ask, 3);
            Assert.True(fair.Accepted);
            Assert.Equal(Day.AddYears(3), career.ContractOf(w)!.End);
        }

        /// <summary>
        /// A deal just signed is worth exactly what it cost. Compared on a different term, the
        /// term discount read as a change in the wrestler and every fresh deal looked dear.
        /// </summary>
        [Fact]
        public void AFreshDealIsWorthWhatItCost_AndAHotRunMakesItABargain()
        {
            var w = TestRoster.Make("W", overness: 70);
            var career = NewCareer(PromotionTier.National, w);
            career.Sign(w, TalentMarket.GuaranteeAsk(w, 2), 2);
            var c = career.ContractOf(w)!;

            Assert.Equal(c.AnnualGuarantee, TalentMarket.WorthToday(w, c));

            w.Momentum = 80;
            Assert.True(TalentMarket.WorthToday(w, c) > c.AnnualGuarantee);
        }

        /// <summary>Doc 11 §2.4: below television nobody is signed. Everybody works by the night.</summary>
        [Fact]
        public void BelowTelevisionNobodySigns()
        {
            var w = TestRoster.Make("W");
            var career = NewCareer(PromotionTier.SuperIndie, w);

            var result = career.Sign(w, 10_000_000m, 3);

            Assert.False(result.Accepted);
            Assert.False(career.CanSign);
        }

        /// <summary>The trade a guarantee is: their appearances cost nothing extra; everybody else's cost the asking price.</summary>
        [Fact]
        public void AGuaranteedAppearanceCostsNothingExtra()
        {
            var signed = TestRoster.Make("Signed", overness: 80);
            var free = TestRoster.Make("Free", overness: 80);
            var career = NewCareer(PromotionTier.National, signed, free);
            career.Sign(signed, TalentMarket.GuaranteeAsk(signed, 2), 2);

            var f = career.Projection(Show(career, ShowType.Television, signed, free));

            Assert.Equal(TalentMarket.PerDateAsk(free), f.Talent);
        }

        [Fact]
        public void GuaranteesArePaidByTheClock_WorkOrNot()
        {
            var w = TestRoster.Make("W", overness: 40);
            var career = NewCareer(PromotionTier.National, w);
            Assert.True(career.Sign(w, 365_000m, 2).Accepted);
            var cash = career.Books.Cash;

            for (int i = 0; i < 10; i++) career.AdvanceOneDay();

            Assert.Equal(10_000m, career.Books.Payroll);
            Assert.Equal(cash - 10_000m - 10 * career.DailyOverhead, career.Books.Cash);
        }

        [Fact]
        public void RunningAShowEarlyPaysTheGuaranteesForTheDaysItSkips()
        {
            var a = TestRoster.Make("A", overness: 40);
            var b = TestRoster.Make("B");
            var career = NewCareer(PromotionTier.National, a, b);
            Assert.True(career.Sign(a, 365_000m, 2).Accepted);
            var show = career.Schedule("Later", Day.AddDays(10), ShowType.Television);
            show.Card.Add(Match(a, b));

            career.RunShow(show);

            Assert.Equal(10_000m, career.Books.Payroll);
        }

        // ── The term ─────────────────────────────────────────────────────────

        /// <summary>Doc 11 §2.1: the contract freezes while they are hurt — nobody runs out the clock on the sidelines.</summary>
        [Fact]
        public void AnInjuryTollsTheContract()
        {
            var w = TestRoster.Make("W");
            var career = NewCareer(PromotionTier.National, w);
            career.Sign(w, TalentMarket.GuaranteeAsk(w, 1), 1);
            var end = career.ContractOf(w)!.End!.Value;
            w.Injury = new Injury { Part = BodyPart.Knee, Sustained = Day, ClearedOn = Day.AddDays(30), WeeksOut = 4 };

            for (int i = 0; i < 10; i++) career.AdvanceOneDay();

            Assert.Equal(end.AddDays(10), career.ContractOf(w)!.End);
            Assert.Equal(10, career.ContractOf(w)!.DaysTolled);
        }

        /// <summary>A deal that runs out sends them to the open market — still bookable, by the night.</summary>
        [Fact]
        public void AnExpiredContractGoesToTheMarket()
        {
            var w = TestRoster.Make("W");
            var career = NewCareer(PromotionTier.Established, w);
            career.Sign(w, TalentMarket.GuaranteeAsk(w, 1), 1);
            var end = career.ContractOf(w)!.End!.Value;

            Assert.Contains(career.ContractOf(w)!, career.ExpiringWithin(400));
            while (career.CurrentDate < end) career.AdvanceOneDay();

            Assert.Null(career.ContractOf(w));
            Assert.Contains("ran out", career.ContractLog.Last().What);
        }

        /// <summary>Doc 13 §3.2: a release pays out the non-compete — ninety days, not the whole deal.</summary>
        [Fact]
        public void AReleasePaysOutNinetyDays()
        {
            var w = TestRoster.Make("W", overness: 40);
            var career = NewCareer(PromotionTier.National, w);
            Assert.True(career.Sign(w, 365_000m, 3).Accepted);
            var cash = career.Books.Cash;

            var paid = career.Release(w);

            Assert.Equal(90_000m, paid);
            Assert.Equal(cash - 90_000m, career.Books.Cash);
            Assert.Null(career.ContractOf(w));
        }

        [Fact]
        public void LettingARegularGoCostsNothing()
        {
            var w = TestRoster.Make("W");
            var career = NewCareer(PromotionTier.Local, w);
            career.MakeRegular(w);

            Assert.Equal(0m, career.Release(w));
            Assert.Null(career.ContractOf(w));
        }

        // ── The opening roster ───────────────────────────────────────────────

        [Theory]
        [InlineData(PromotionTier.Established)]
        [InlineData(PromotionTier.National)]
        [InlineData(PromotionTier.Global)]
        public void ATelevisedPromotionOpensWithGuaranteesInsideItsBudget(PromotionTier tier)
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var contracts = new TierOpeningRoster().Open(new Promotion { Tier = tier }, roster, Day);
            var (size, budget) = TierOpeningRoster.For(tier);
            var signed = roster.Where(w => contracts.Any(c => c.WrestlerId == w.Id)).ToList();

            output.WriteLine($"{tier}: {contracts.Count} signed, {contracts.Sum(c => c.AnnualGuarantee):N0} a year");

            Assert.All(contracts, c => Assert.True(c.IsGuaranteed));
            Assert.True(contracts.Sum(c => c.AnnualGuarantee) <= budget);
            Assert.True(contracts.Count >= Math.Min(size, roster.Count) * 0.9, "The budget should fill the roster, not a handful of stars.");
            Assert.Contains(signed, w => w.Division == Division.Mens);
            Assert.Contains(signed, w => w.Division == Division.Womens);

            // Staggered, so the renewals arrive a few at a time.
            Assert.True(contracts.Select(c => c.End).Distinct().Count() >= 3);
        }

        /// <summary>
        /// Quality is not chosen; it falls out of the budget. The smallest promotion opens with
        /// the least over end of the roster and the largest with its stars.
        /// </summary>
        [Fact]
        public void QualityFollowsTheBudget()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            double Average(PromotionTier tier)
            {
                var ids = new TierOpeningRoster().Open(new Promotion { Tier = tier }, roster, Day).Select(c => c.WrestlerId).ToHashSet();
                return roster.Where(w => ids.Contains(w.Id)).Average(w => w.Overness);
            }

            double local = Average(PromotionTier.Local);
            double super = Average(PromotionTier.SuperIndie);
            double global = Average(PromotionTier.Global);
            output.WriteLine($"average overness: Local {local:F1}, SuperIndie {super:F1}, Global {global:F1}");

            Assert.True(local < super && super < global);
        }

        [Theory]
        [InlineData(PromotionTier.Local)]
        [InlineData(PromotionTier.Independent)]
        [InlineData(PromotionTier.SuperIndie)]
        public void BelowTelevisionThePromotionOpensWithRegulars_AtPricesItCanPay(PromotionTier tier)
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var contracts = new TierOpeningRoster().Open(new Promotion { Tier = tier }, roster, Day);
            var (_, ceiling) = TierOpeningRoster.For(tier);

            Assert.NotEmpty(contracts);
            Assert.All(contracts, c => Assert.Equal(ContractKind.Regular, c.Kind));
            Assert.All(contracts, c => Assert.True(TalentMarket.PerDateAsk(roster.Single(w => w.Id == c.WrestlerId)) <= ceiling));
        }

        [Fact]
        public void TheOpeningRosterIsTheSameEveryTime()
        {
            var a = new TierOpeningRoster().Open(new Promotion { Tier = PromotionTier.National }, DataLoaders.LoadEmbeddedWrestlers(), Day);
            var b = new TierOpeningRoster().Open(new Promotion { Tier = PromotionTier.National }, DataLoaders.LoadEmbeddedWrestlers(), Day);
            Assert.Equal(a.Select(c => c.WrestlerId), b.Select(c => c.WrestlerId));
        }

        /// <summary>The seam a custom database will use: any source can open the roster.</summary>
        [Fact]
        public void AnySourceCanOpenTheRoster()
        {
            var w = TestRoster.Make("Chosen");
            var career = NewCareer(PromotionTier.Global, w, TestRoster.Make("Other"));

            career.OpenRoster(new Fixed(new Contract { WrestlerId = w.Id, AnnualGuarantee = 1m, Start = Day, End = Day.AddYears(1) }));

            Assert.Equal(w.Id, career.Contracts.Single().WrestlerId);
        }

        private sealed class Fixed(params Contract[] contracts) : IOpeningRoster
        {
            public IReadOnlyList<Contract> Open(Promotion promotion, IReadOnlyList<Wrestler> world, DateOnly on) => contracts;
        }

        // ── Saves ────────────────────────────────────────────────────────────

        [Fact]
        public void ContractsSurviveASave()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var career = NewCareer(PromotionTier.National, roster.ToArray());
            career.OpenRoster();
            var hurt = career.Contracts[0];
            hurt.DaysTolled = 12;
            career.Release(career.FindWrestler(career.Contracts[1].WrestlerId)!);

            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career), DataLoaders.LoadEmbeddedWrestlers());

            Assert.Equal(career.Contracts.Count, loaded.Contracts.Count);
            Assert.Equal(career.DailyPayroll, loaded.DailyPayroll);
            Assert.Equal(12, loaded.Contracts.Single(c => c.WrestlerId == hurt.WrestlerId).DaysTolled);
            Assert.Equal(career.Books.Payroll, loaded.Books.Payroll);
            Assert.Contains("released", loaded.ContractLog.Last().What);
        }

        /// <summary>
        /// A career saved before contracts opens its roster from the tier's source on load.
        /// Nobody vanishes from the world — everybody else is on the open market — but at a
        /// television tier the bill is no longer everybody.
        /// </summary>
        [Fact]
        public void ASaveFromBeforeContracts_OpensItsRosterOnLoad()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var career = NewCareer(PromotionTier.Established, roster.ToArray());

            var dto = SaveSerializer.ToDto(career);
            dto.Contracts = null;
            var loaded = SaveSerializer.FromJson(System.Text.Json.JsonSerializer.Serialize(dto), DataLoaders.LoadEmbeddedWrestlers());

            Assert.Equal(TierOpeningRoster.For(PromotionTier.Established).Size, loaded.Contracts.Count);
            Assert.Equal(roster.Count, loaded.Roster.Count);
        }
    }
}
