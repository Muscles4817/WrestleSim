using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Person;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;
using Xunit.Abstractions;
using SegmentModel = WrestlingSim.Models.Segment.Segment;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Characters, step 3 of docs/gimmick-plan.md: promos as performances. The owner's ruling of
    /// 17 Nov 2026 is the spine of this file — neither form wins by rule; both can be superb and
    /// both can be awful, and the people decide which.
    /// </summary>
    public class PromoTests
    {
        private readonly ITestOutputHelper output;
        public PromoTests(ITestOutputHelper output) => this.output = output;

        private static Wrestler Talker(string name, int delivery, int improv, int all = 70,
                                       params RegisterSkill[] registers)
        {
            var w = TestRoster.Make(name, charisma: 3.5);
            w.Promo = new PromoSkills
            {
                Delivery = delivery, Improvisation = improv,
                Stillness = all, RingPresence = all, CrowdWork = all, SetPiece = all, Physicality = all,
                Registers = registers.Length > 0 ? [.. registers] : [new RegisterSkill { Register = PromoRegister.Cold, Rating = all }],
                Range = all
            };
            return w;
        }

        private static Promos.Reading Read(Wrestler w, PromoForm form, int writing = 70,
                                           PromoStaging staging = PromoStaging.Ring, PromoRegister register = PromoRegister.Cold) =>
            Promos.Read(w, form, staging, register, writing);

        // ── Neither form wins by rule ────────────────────────────────────────

        [Fact]
        public void AnEquallyGoodReaderAndImproviser_WithWritersAsGood_GetsTheSameFromEitherForm()
        {
            var w = Talker("Even", delivery: 70, improv: 70);
            Assert.Equal(Read(w, PromoForm.Scripted).Expected, Read(w, PromoForm.Free).Expected, 6);
            Assert.Equal(Read(w, PromoForm.Scripted).Spread, Read(w, PromoForm.Free).Spread, 6);
        }

        [Fact]
        public void BadWritersSinkAGoodReader_AndGreatWritersCannotCarryAPoorOne()
        {
            var reader = Talker("Reader", delivery: 90, improv: 40);
            var mumbler = Talker("Mumbler", delivery: 30, improv: 40);

            Assert.True(Read(reader, PromoForm.Scripted, writing: 20).Expected < Read(reader, PromoForm.Scripted, writing: 90).Expected - 10);
            Assert.True(Read(mumbler, PromoForm.Scripted, writing: 100).Expected < Read(reader, PromoForm.Scripted, writing: 100).Expected - 10);
        }

        [Fact]
        public void TheOwnersExample_FunnyOffTheCuff_ButNeedsAScriptWhenItIsSerious()
        {
            var w = Talker("Comic", delivery: 70, improv: 70, all: 70,
                new RegisterSkill { Register = PromoRegister.Funny,     Rating = 85, Lean = PromoForm.Free },
                new RegisterSkill { Register = PromoRegister.Emotional, Rating = 75, Lean = PromoForm.Scripted });

            Assert.True(Read(w, PromoForm.Free, register: PromoRegister.Funny).Expected
                      > Read(w, PromoForm.Scripted, register: PromoRegister.Funny).Expected);
            Assert.True(Read(w, PromoForm.Scripted, register: PromoRegister.Emotional).Expected
                      > Read(w, PromoForm.Free, register: PromoRegister.Emotional).Expected);
        }

        [Fact]
        public void HowFarAPromoSwings_ComesFromThePerson_NotTheForm()
        {
            var great = Talker("Great", delivery: 95, improv: 95);
            var poor  = Talker("Poor",  delivery: 35, improv: 35);
            foreach (var form in new[] { PromoForm.Scripted, PromoForm.Free })
                Assert.True(Read(poor, form, writing: 95).Spread > Read(great, form, writing: 95).Spread, form.ToString());
        }

        // ── Staging and register ─────────────────────────────────────────────

        [Fact]
        public void AGreatTalkerAskedToBrawl_IsNotAGreatBrawler()
        {
            var w = Talker("Talker", delivery: 90, improv: 90, all: 90);
            w.Promo!.Physicality = 35;
            var talk  = Read(w, PromoForm.Free, staging: PromoStaging.Ring);
            var brawl = Read(w, PromoForm.Free, staging: PromoStaging.Physical);
            Assert.True(brawl.Expected < talk.Expected - 10);
            Assert.Equal("staging", brawl.Deciding);
        }

        [Fact]
        public void AMonsterAskedToBeAfraid_IsWorkingAgainstTheCharacter()
        {
            var w = Talker("Beast", 70, 70, 70);
            w.Gimmick.Type = GimmickType.Loner;
            double asLoner = Read(w, PromoForm.Free, register: PromoRegister.Fearful).Expected;
            w.Gimmick.Type = GimmickType.Monster;
            Assert.True(Read(w, PromoForm.Free, register: PromoRegister.Fearful).Expected < asLoner);
        }

        // ── In the show ──────────────────────────────────────────────────────

        private static SegmentModel Promo(Wrestler w, PromoForm form, PromoRegister? register = null)
        {
            var s = new SegmentModel("Test Promo", SegmentType.Promo, SegmentLocation.Ring, isScripted: true) { Form = form, Register = register };
            s.AddParticipant(w);
            s.AddAction(SegmentActionLibrary.Find("Cut a Promo")!.ToAction(w));
            return s;
        }

        [Fact]
        public void NoFormGetsABonusForBeingThatForm()
        {
            var w = Talker("Even", delivery: 70, improv: 70);
            double scripted = Enumerable.Range(0, 300).Average(s => new SegmentSimulator(s, creative: 7).Simulate(Promo(w, PromoForm.Scripted)).AudienceImpact);
            double free     = Enumerable.Range(0, 300).Average(s => new SegmentSimulator(s, creative: 7).Simulate(Promo(w, PromoForm.Free)).AudienceImpact);
            output.WriteLine($"scripted {scripted:F3}, free {free:F3}");
            Assert.InRange(free / scripted, 0.97, 1.03);
        }

        [Fact]
        public void WhatDecidedABadPromo_IsInTheReport()
        {
            var w = Talker("Wooden", delivery: 15, improv: 60, all: 60);
            var lines = Enumerable.Range(0, 200)
                .SelectMany(s => new SegmentSimulator(s, creative: 1).Simulate(Promo(w, PromoForm.Scripted)).PromoLines)
                .ToList();
            Assert.Contains(lines, l => l.Contains("reading every line") || l.Contains("got through the script"));
        }

        [Fact]
        public void TheCreativeTeamIsWhatTheShowWritesWith()
        {
            var w = Talker("Reader", delivery: 85, improv: 40);
            double poor  = Enumerable.Range(0, 200).Average(s => new SegmentSimulator(s, creative: 1).Simulate(Promo(w, PromoForm.Scripted)).Promos[w]);
            double great = Enumerable.Range(0, 200).Average(s => new SegmentSimulator(s, creative: 10).Simulate(Promo(w, PromoForm.Scripted)).Promos[w]);
            double free1 = Enumerable.Range(0, 200).Average(s => new SegmentSimulator(s, creative: 1).Simulate(Promo(w, PromoForm.Free)).Promos[w]);
            double free10 = Enumerable.Range(0, 200).Average(s => new SegmentSimulator(s, creative: 10).Simulate(Promo(w, PromoForm.Free)).Promos[w]);
            Assert.True(great > poor + 15);
            Assert.Equal(free1, free10, 6);   // nothing written, nothing for writers to do
        }

        // ── Calibration ──────────────────────────────────────────────────────

        /// <summary>
        /// A promo by a typical shipped wrestler, at a typical promotion's writing and on bullet
        /// points, should land about where the old charisma-only formula put it, so segment values
        /// on a card stay in range. Measured over the shipped roster against their charisma, on
        /// the 0–5 scale the segment reads.
        /// </summary>
        [Fact]
        public void ATypicalPromoLandsWhereATypicalPromoDid()
        {
            var shipped = DataLoaders.LoadEmbeddedWrestlers();
            int writing = Promos.Writing(Promos.StartingCreative(PromotionTier.Established));
            double now = shipped.Average(w => Promos.Read(w, PromoForm.BulletPoints, PromoStaging.Ring,
                                                          Promos.NaturalRegister(w.Gimmick.Tone), writing).Expected / 20.0);
            double was = shipped.Average(w => w.Charisma);
            output.WriteLine($"mean promo {now:F4} on the 0–5 scale; mean charisma {was:F4}; centring {was - now:F4} (constant {Promos.Centring})");
            Assert.Equal(Promos.Centring, was - now, 3);
        }

        // ── The creative team ────────────────────────────────────────────────

        private static Career NewCareer(PromotionTier tier = PromotionTier.Independent, decimal cash = 1_000_000m)
        {
            var day = new DateOnly(2026, 1, 5);
            var career = new Career { Promotion = new Promotion { Name = "Test", Tier = tier }, StartDate = day, CurrentDate = day, Roster = [] };
            career.OpenBooks(day);
            career.Books.Cash = cash;
            return career;
        }

        [Fact]
        public void TheOpeningTeamCostsNothingExtra_AndEveryLevelBoughtCosts()
        {
            var career = NewCareer();
            decimal before = career.DailyOverhead;
            Assert.Equal(0m, Promos.UpkeepPerDay(career.Promotion.Tier, Promos.CreativeOf(career.Promotion)));

            Assert.Null(career.UpgradeCreative());
            Assert.Equal(Promos.StartingCreative(PromotionTier.Independent) + 1, Promos.CreativeOf(career.Promotion));
            Assert.True(career.DailyOverhead > before);
            Assert.True(career.Books.Cash < 1_000_000m);
        }

        [Fact]
        public void AnIndieCanBuyItsWayToTen_IfItCanPay()
        {
            var career = NewCareer(PromotionTier.Local, cash: 10_000_000m);
            while (career.UpgradeCreative() is null) { }
            Assert.Equal(Promos.MaxCreative, Promos.CreativeOf(career.Promotion));

            var broke = NewCareer(PromotionTier.Local, cash: 10m);
            Assert.NotNull(broke.UpgradeCreative());
            Assert.Equal(Promos.StartingCreative(PromotionTier.Local), Promos.CreativeOf(broke.Promotion));
        }

        // ── Notes and saves ──────────────────────────────────────────────────

        [Fact]
        public void AStandingNoteCanScriptOnlyTheSeriousMaterial()
        {
            var career = NewCareer();
            var w = TestRoster.Make("Comic");
            career.PromoNotes[w.Id] = new PromoNote { Usual = PromoForm.Free, Serious = PromoForm.Scripted };
            Assert.Equal(PromoForm.Free, career.NotedForm(w, PromoRegister.Funny));
            Assert.Equal(PromoForm.Scripted, career.NotedForm(w, PromoRegister.Emotional));
            Assert.Null(career.NotedForm(TestRoster.Make("Nobody"), PromoRegister.Funny));
        }

        [Fact]
        public void TheSaveCarriesTheCreativeTeamTheNotesAndASegmentsAxes()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var career = NewCareer(PromotionTier.Established);
            career.Roster = roster;
            Assert.Null(career.UpgradeCreative());
            var w = roster[0];
            career.PromoNotes[w.Id] = new PromoNote { Usual = PromoForm.BulletPoints, Serious = PromoForm.Scripted };

            var show = career.Schedule("Weekly", career.CurrentDate.AddDays(3), ShowType.Television);
            var seg = Promo(w, PromoForm.Free, PromoRegister.Menacing);
            seg.Staging = PromoStaging.Roaming;
            show.Card.Add(seg);

            var back = SaveSerializer.FromJson(SaveSerializer.ToJson(career), DataLoaders.LoadEmbeddedWrestlers());
            Assert.Equal(Promos.StartingCreative(PromotionTier.Established) + 1, Promos.CreativeOf(back.Promotion));
            Assert.Equal(PromoForm.Scripted, back.PromoNotes[w.Id].Serious);
            var saved = Assert.IsType<SegmentModel>(back.Upcoming.Single().Card.Single());
            Assert.Equal(PromoForm.Free, saved.Form);
            Assert.Equal(PromoStaging.Roaming, saved.Staging);
            Assert.Equal(PromoRegister.Menacing, saved.Register);
        }

        // ── The roster ───────────────────────────────────────────────────────

        [Fact]
        public void EveryRealWrestlerIsJudged_AndTheResearchBehindItIsKept()
        {
            var shipped = DataLoaders.LoadEmbeddedWrestlers();
            Assert.All(shipped, w => Assert.NotNull(w.Promo));
            Assert.All(shipped, w => Assert.InRange(w.Promo!.Registers.Count, 1, 3));

            var record = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(
                RepoRoot(), "docs", "research", "promo-skills-2026-11.json")));
            var researched = record.RootElement.EnumerateArray().Select(e => e.GetProperty("RealName").GetString()).ToHashSet();
            Assert.All(shipped, w => Assert.Contains(w.RealName, researched));
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs"))) dir = dir.Parent;
            return dir!.FullName;
        }

        [Fact]
        public void WhatYouKnowAboutSomebodysTalkingSharpensWithTime()
        {
            var w = DataLoaders.LoadEmbeddedWrestlers()[0];
            var stranger = Promos.Describe(NewCareer(), w);
            Assert.Equal("Known for", stranger.Lines[0].Label);
            Assert.Equal(2, stranger.Lines.Count);

            var exhibition = Promos.Describe(null, w);
            Assert.True(exhibition.Lines.Count > 7);
        }
    }
}
