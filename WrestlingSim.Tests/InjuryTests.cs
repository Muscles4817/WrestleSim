using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.Person;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **The cost of having had the match.**
    ///
    /// Doc 15 §3.1 gives a table of risk multipliers and calls them "directly usable", so
    /// <see cref="InjuryRisk"/> is that table rather than a curve somebody liked the shape
    /// of. What it deliberately contains nothing about is *rating*: an injury is not a
    /// punishment for booking a bad match, it is what a body does after enough of them.
    ///
    /// This closes the loop the ring-condition meters opened. Until now a booker could work
    /// their main eventer three times a night, every week, for a year, and the only cost was
    /// a number. Doc 15 puts match volume first in its table — "roughly linear; 200 dates is
    /// ~2× the risk of 100" — and being cooked is now dangerous rather than merely
    /// expensive.
    ///
    /// The load-bearing test is <see cref="ASeasonProducesTheRateTheReferenceReports"/>:
    /// every multiplier below could be individually sensible and still add up to a roster
    /// that is 85% crippled by March, which is exactly what the first calibration did.
    /// </summary>
    public class InjuryTests(ITestOutputHelper output)
    {
        private static readonly int Seed = StableSeed.From("injuries");
        private static readonly DateOnly Day0 = new(2026, 1, 5);

        private static Wrestler W(string n, WrestlingStyle style = WrestlingStyle.Brawler,
                                  int size = 3, int psych = 78) =>
            TestRoster.Make(n, overness: 75, charisma: 3.2, psychology: psych, skill: 3.5)
                is var w && Set(w, style, size, psych) ? w : w;

        private static bool Set(Wrestler w, WrestlingStyle style, int size, int psych)
        {
            w.Style = style;
            w.Physical!.Size = size;
            w.Mental!.RingIQ = psych;
            return true;
        }

        private static Injury Hurt(BodyPart part, int weeks = 8) => new()
        {
            Part = part, Sustained = Day0, ClearedOn = Day0.AddDays(weeks * 7), WeeksOut = weeks
        };

        // ── The rate the reference reports ───────────────────────────────────

        /// <summary>
        /// **A season has to produce the numbers doc 15 §3 reports.**
        ///
        /// 35–55% of full-time performers miss time in a given year; 10–18% lose three
        /// months or more; six to twelve weeks lost on average. Against doc 06's 100–150
        /// dates for a full-timer.
        ///
        /// This is the test that matters, because the multipliers compound harder than they
        /// look — a mid-card wrestler on an ordinary night is already carrying four at once.
        /// The first calibration had every individual multiplier looking reasonable and put
        /// **85%** of a roster on the shelf inside a year, over half of them for three months
        /// or more. Nothing but running the season would have found that.
        /// </summary>
        [Fact]
        public void ASeasonProducesTheRateTheReferenceReports()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers().Take(40).ToList();
            var rng = new Random(Seed);

            int hurt = 0, significant = 0;
            double weeksLost = 0;

            foreach (var w in roster)
            {
                var foe = roster[(roster.IndexOf(w) + 1) % roster.Count];
                w.Fatigue = 25; w.Injury = null; w.InjuryHistory.Clear();

                bool any = false, big = false;
                for (int date = 0; date < 120; date++)
                {
                    var plan = Singles(w, foe, "Face-in-Peril");
                    foreach (var roll in new MatchEngine(rng.Next()).Execute(plan).Injuries)
                    {
                        if (roll.Wrestler != w) continue;
                        any = true;
                        weeksLost += roll.WeeksOut;
                        if (roll.WeeksOut >= 13) big = true;
                        date += roll.WeeksOut * 2;      // roughly: they are not working
                    }
                }
                if (any) hurt++;
                if (big) significant++;
            }

            double missed = 100.0 * hurt / roster.Count;
            double serious = 100.0 * significant / roster.Count;
            double avg = weeksLost / roster.Count;

            output.WriteLine($"  120 dates, {roster.Count} wrestlers");
            output.WriteLine($"  missed time   {missed:F0}%   doc 15 §3: 35–55%");
            output.WriteLine($"  3+ months     {serious:F0}%   doc 15 §3: 10–18%");
            output.WriteLine($"  weeks lost    {avg:F1}    doc 15 §3: 6–12");

            Assert.InRange(missed, 30, 62);
            Assert.InRange(serious, 8, 24);
            Assert.InRange(avg, 5, 14);
        }

        // ── The multipliers ──────────────────────────────────────────────────

        /// <summary>
        /// **Being cooked is dangerous, and it is dangerous late.**
        ///
        /// Doc 15 §3.1's most specific claim: "poor conditioning sharply increases
        /// **late-match** injury risk." So the roll is per beat and both terms are read —
        /// how tired they arrived and how deep into the match it is — and they compound. A
        /// fresh wrestler in the tenth minute is fine; a cooked one is not.
        ///
        /// This is the link the ring-condition meters existed to make, and without it
        /// fatigue is a rating penalty with no teeth.
        /// </summary>
        [Fact]
        public void BeingCookedIsDangerousAndItIsDangerousLate()
        {
            foreach (int f in new[] { 0, 30, 60, 90 })
                output.WriteLine($"  fatigue {f,3}: early ×{InjuryRisk.FatigueRisk(f, 2, 1.0):F2}   " +
                                 $"late ×{InjuryRisk.FatigueRisk(f, 12, 1.0):F2}");

            double freshEarly = InjuryRisk.FatigueRisk(0, 2, 1.0);
            double freshLate  = InjuryRisk.FatigueRisk(0, 12, 1.0);
            double cookedLate = InjuryRisk.FatigueRisk(90, 12, 1.0);

            Assert.True(cookedLate > freshLate, "fatigue carried in has to matter");
            Assert.True(freshLate > freshEarly, "and the late part of a match has to matter");

            // The two compound rather than merely adding — the cooked wrestler in the
            // twelfth minute is the case doc 15 is describing.
            Assert.True(cookedLate > freshEarly * 2.5,
                $"cooked and late should be much worse than fresh and early: {cookedLate:F2} vs {freshEarly:F2}");

            // A worse gas tank makes the late term bite harder.
            Assert.True(InjuryRisk.FatigueRisk(50, 12, 0.60) > InjuryRisk.FatigueRisk(50, 12, 1.30));
        }

        /// <summary>
        /// **A prior back or neck injury roughly doubles it, and history never goes away.**
        ///
        /// Doc 15 calls injury history "the strongest single predictor" and its sim
        /// implications call it "a permanent, compounding attribute — the most important
        /// detail". It is specific about which: back and neck double, and the rest are
        /// milder, because a wrestler who has turned an ankle once is not carrying what a
        /// wrestler with a fused neck is.
        /// </summary>
        [Fact]
        public void APriorBackOrNeckRoughlyDoublesIt()
        {
            var none = new List<Injury>();
            var oneNeck  = new List<Injury> { Hurt(BodyPart.Neck) };
            var oneAnkle = new List<Injury> { Hurt(BodyPart.Ankle) };

            double neck  = InjuryRisk.HistoryRisk(oneNeck,  BodyPart.Neck);
            double back  = InjuryRisk.HistoryRisk([Hurt(BodyPart.Back)], BodyPart.Back);
            double ankle = InjuryRisk.HistoryRisk(oneAnkle, BodyPart.Ankle);

            output.WriteLine($"  one prior neck  ×{neck:F2}");
            output.WriteLine($"  one prior back  ×{back:F2}");
            output.WriteLine($"  one prior ankle ×{ankle:F2}");

            Assert.Equal(1.0, InjuryRisk.HistoryRisk(none, BodyPart.Neck), 3);
            Assert.InRange(neck, 1.85, 2.15);
            Assert.InRange(back, 1.85, 2.15);
            Assert.True(ankle < neck, "the reference names back and neck specifically");
            Assert.True(ankle > 1.0, "but any history is still history");

            // Compounding, and saturating — a wrestler with nine prior knee injuries is
            // somebody whose knee is gone, and the model says that once.
            var many = Enumerable.Repeat(Hurt(BodyPart.Knee), 9).ToList();
            double nine = InjuryRisk.HistoryRisk(many, BodyPart.Knee);
            double two  = InjuryRisk.HistoryRisk(many.Take(2).ToList(), BodyPart.Knee);
            output.WriteLine($"  two prior knees ×{two:F2}   nine ×{nine:F2}");
            Assert.True(nine > two);
            Assert.True(nine < two * 1.6, "and it saturates rather than running away");

            // Injuries elsewhere count for something too — a body that has been broken
            // before breaks again.
            Assert.True(InjuryRisk.HistoryRisk([Hurt(BodyPart.Shoulder)], BodyPart.Knee) > 1.0);
        }

        /// <summary>
        /// **What gets you hurt does not rank like what tires you out.**
        ///
        /// Three different questions with three different answers, which is the reason none
        /// of them is one "how hard is this style" number: a powerhouse is punished by the
        /// clock, a striker by the pace, and a high-flyer by the landing.
        /// </summary>
        [Fact]
        public void RiskDoesNotRankTheStylesLikeFatigueDoes()
        {
            var styles = Enum.GetValues<WrestlingStyle>().Where(s => s != WrestlingStyle.Null).ToList();

            var byRisk   = styles.OrderByDescending(InjuryRisk.StyleRisk).ToList();
            var byPace   = styles.OrderByDescending(RingCondition.PaceLoad).ToList();
            var byVolume = styles.OrderByDescending(RingCondition.VolumeLoad).ToList();

            output.WriteLine($"  by injury: {string.Join(" > ", byRisk)}");
            output.WriteLine($"  by pace:   {string.Join(" > ", byPace)}");
            output.WriteLine($"  by length: {string.Join(" > ", byVolume)}");

            Assert.NotEqual(byRisk, byPace);
            Assert.NotEqual(byRisk, byVolume);

            // The high-flyer is the dangerous one — doc 15 §3.1 puts high-flying at the top
            // of its 1.5–2.5× band.
            Assert.Equal(WrestlingStyle.HighFlyer, byRisk.First());
            Assert.InRange(InjuryRisk.StyleRisk(WrestlingStyle.HighFlyer), 1.5, 2.5);

            // But a powerhouse outranks him on *length*, which is the whole point.
            Assert.True(RingCondition.VolumeLoad(WrestlingStyle.Powerhouse)
                        > RingCondition.VolumeLoad(WrestlingStyle.HighFlyer));
        }

        /// <summary>
        /// The rest of doc 15 §3.1's table: intensity, size, opponent safety, and the ring
        /// itself. Each is a claim the reference makes in one line.
        /// </summary>
        [Fact]
        public void TheRestOfTheTableIsThere()
        {
            // Intensity — the hardest beats are where people get hurt.
            Assert.True(InjuryRisk.IntensityRisk(BeatIntensity.Extreme)
                        > InjuryRisk.IntensityRisk(BeatIntensity.Low) * 4);

            // Size — "more force, more joint load, worse landing mechanics".
            Assert.True(InjuryRisk.SizeRisk(5) > InjuryRisk.SizeRisk(3));
            Assert.Equal(InjuryRisk.SizeRisk(1), InjuryRisk.SizeRisk(3), 3);

            // The opponent — a wrestler who does not know where he is putting people.
            Assert.True(InjuryRisk.OpponentRisk(40, 40) > InjuryRisk.OpponentRisk(95, 95) * 1.3);

            // The ring — "indie rings are frequently much worse".
            Assert.True(InjuryRisk.RingRisk(PromotionTier.Local)
                        > InjuryRisk.RingRisk(PromotionTier.Global) * 1.35);

            output.WriteLine($"  extreme/low intensity ×{InjuryRisk.IntensityRisk(BeatIntensity.Extreme) / InjuryRisk.IntensityRisk(BeatIntensity.Low):F1}");
            output.WriteLine($"  size 5 ×{InjuryRisk.SizeRisk(5):F2}   sloppy opponent ×{InjuryRisk.OpponentRisk(40, 40):F2}");
            output.WriteLine($"  local ring ×{InjuryRisk.RingRisk(PromotionTier.Local):F2}");
        }

        /// <summary>
        /// **A big man tears a pectoral and a high-flyer wrecks a knee.**
        ///
        /// Doc 15 §2.1 names the pectoral tear as "common in powerlifting-built performers"
        /// specifically, and the frequency column is what decides the shape of a season —
        /// most of what happens has to be short, or 35–55% missing time and only 10–18%
        /// losing three months cannot both be true.
        /// </summary>
        [Fact]
        public void WhatGetsHurtDependsOnWhoIsWrestling()
        {
            double flyerKnee = InjuryRisk.Likelihood(BodyPart.Knee, WrestlingStyle.HighFlyer, 3);
            double techKnee  = InjuryRisk.Likelihood(BodyPart.Knee, WrestlingStyle.Technical, 3);

            double bigPec    = InjuryRisk.Likelihood(BodyPart.Pectoral, WrestlingStyle.Powerhouse, 5);
            double smallPec  = InjuryRisk.Likelihood(BodyPart.Pectoral, WrestlingStyle.HighFlyer, 2);

            output.WriteLine($"  knee: high-flyer {flyerKnee:F1} vs technical {techKnee:F1}");
            output.WriteLine($"  pec:  big powerhouse {bigPec:F1} vs small flyer {smallPec:F1}");

            Assert.True(flyerKnee > techKnee * 1.5);
            Assert.True(bigPec > smallPec * 2);

            // And the short injuries dominate, which is what makes the season's shape right.
            var all = Enum.GetValues<BodyPart>()
                .ToDictionary(p => p, p => InjuryRisk.Likelihood(p, WrestlingStyle.Brawler, 3));
            double shortOnes = all.Where(kv => InjuryRisk.WeeksOut(kv.Key).High <= 14).Sum(kv => kv.Value);
            double share = shortOnes / all.Values.Sum();

            output.WriteLine($"  share of injuries under 14 weeks: {share * 100:F0}%");
            Assert.True(share > 0.6, "most of what happens to a wrestler has to be short");
        }

        /// <summary>
        /// **How long it keeps them out is decided by what it is.**
        ///
        /// Doc 15 §2.1's time-out column, which the frequency weights are calibrated
        /// *against* and therefore cannot themselves check: a season's headline rate barely
        /// moves if a torn ACL heals in a fortnight, because knees are rare. A mutation that
        /// shortened <c>WeeksOut(Knee)</c> from 24–52 to 2–6 survived every other test here.
        ///
        /// The two groups have to stay apart. The reference's short injuries — the
        /// concussion, the tweaked back, the rolled ankle, the broken bone — are the ones a
        /// booker works around; its ligament and tendon injuries are the ones that take a
        /// wrestler off television for the rest of the year and change what the promotion
        /// can book. Anything that lets those overlap has stopped modelling either.
        /// </summary>
        [Fact]
        public void TheTimeOutIsDecidedByWhatTheInjuryIs()
        {
            // Doc 15 §2.1: "days to months" — the ones a card is rebuilt around.
            var short_ = new[] { BodyPart.Ankle, BodyPart.Concussion, BodyPart.Back, BodyPart.Bone };

            // Doc 15 §2.1: an ACL is 9–12 months, an Achilles 9–12, a serious neck 12–18
            // and often career-ending. Truncated at a year because a career cannot end here.
            var long_ = new[] { BodyPart.Knee, BodyPart.Achilles, BodyPart.Neck };

            foreach (var part in Enum.GetValues<BodyPart>())
            {
                var (low, high) = InjuryRisk.WeeksOut(part);
                output.WriteLine($"  {Injury.Label(part),-11} {low,2}–{high,2} weeks");
                Assert.True(low >= 1 && low <= high && high <= 52);
            }

            foreach (var part in short_)
                Assert.True(InjuryRisk.WeeksOut(part).High <= 14,
                            $"{Injury.Label(part)} is one a booker works around, not a season");

            foreach (var part in long_)
            {
                var (low, high) = InjuryRisk.WeeksOut(part);
                Assert.True(low >= 24, $"{Injury.Label(part)} takes most of a year at least");
                Assert.True(high >= 52, $"{Injury.Label(part)} runs to the end of the model");
            }

            // The shoulder sits between: doc 15 gives four to nine months, so it is neither
            // a fortnight nor a write-off.
            var shoulder = InjuryRisk.WeeksOut(BodyPart.Shoulder);
            Assert.InRange(shoulder.Low, 13, 20);
            Assert.InRange(shoulder.High, 30, 45);

            // And no short injury reaches into the long band, in either direction.
            int worstShort = short_.Max(p => InjuryRisk.WeeksOut(p).High);
            int bestLong   = long_.Min(p => InjuryRisk.WeeksOut(p).Low);
            output.WriteLine($"  worst short {worstShort}w · shortest long {bestLong}w");
            Assert.True(worstShort < bestLong, "the two kinds of injury have to stay apart");
        }

        // ── What an injury does ──────────────────────────────────────────────

        /// <summary>
        /// **The engine reports and the show writes it down.**
        ///
        /// The same split every other consequence uses, and the reason a match can be
        /// simulated twice without maiming somebody twice.
        /// </summary>
        [Fact]
        public void TheEngineReportsAndDoesNotApply()
        {
            var a = W("Alpha", WrestlingStyle.HighFlyer);
            var b = W("Bravo");
            a.Fatigue = 95;

            // Enough runs that somebody is going to get hurt.
            var seen = 0;
            for (int i = 0; i < 400 && seen == 0; i++)
            {
                var r = new MatchEngine(Seed + i).Execute(Singles(a, b, "Epic"),
                                                          tier: PromotionTier.Local);
                seen = r.Injuries.Count;
            }

            output.WriteLine($"  injuries reported across the run: {seen}");
            Assert.True(seen > 0, "a cooked high-flyer working epics on an indie ring should get hurt");

            // And nobody was actually injured by it — the engine only said so.
            Assert.Null(a.Injury);
            Assert.Empty(a.InjuryHistory);
        }

        /// <summary>
        /// **One match, one injury per wrestler.**
        ///
        /// The first one is the story. Without the guard a wrestler can be hurt twice in the
        /// same match, and the show layer writes both down — so their sheet ends up showing
        /// whichever came *last*, which can be the shorter of the two, while both sit in the
        /// history and the report names the same person twice.
        ///
        /// At the shipped risk that is a one-in-tens-of-thousands event, which is exactly why
        /// it needs the worst wrestler in the world to test: a cooked, oversized high-flyer
        /// with a file of prior injuries, working extreme beats on an indie ring against
        /// somebody who does not know where he is putting people. Everything the table has,
        /// all at once, is about a hundredfold — and a double still only happens in a couple
        /// of matches per hundred, so the run has to be long. The count printed below is the
        /// evidence the scenario is hot enough for the assertion underneath it to mean
        /// something.
        /// </summary>
        [Fact]
        public void AMatchHurtsAWrestlerAtMostOnce()
        {
            var wrecked = W("Wrecked", WrestlingStyle.HighFlyer, size: 5);
            wrecked.Fatigue = 100;
            wrecked.InjuryHistory.AddRange([
                Hurt(BodyPart.Knee), Hurt(BodyPart.Knee), Hurt(BodyPart.Knee),
                Hurt(BodyPart.Back), Hurt(BodyPart.Ankle), Hurt(BodyPart.Shoulder)]);

            // Somebody with no idea what he is doing out there.
            var careless = W("Careless", psych: 5);

            int matches = 0, injuries = 0;
            for (int i = 0; i < 2500; i++)
            {
                var plan = Singles(wrecked, careless, "Epic");
                foreach (var beat in plan.Beats) beat.Intensity = BeatIntensity.Extreme;

                var result = new MatchEngine(Seed + i).Execute(plan, tier: PromotionTier.Local);
                if (result.Injuries.Count == 0) continue;

                matches++;
                injuries += result.Injuries.Count;

                Assert.Equal(result.Injuries.Select(x => x.Wrestler).Distinct().Count(),
                             result.Injuries.Count);
            }

            output.WriteLine($"  2500 matches, {matches} of them produced an injury, {injuries} injuries");
            Assert.True(injuries > 100,
                        "the scenario has to be dangerous enough that a double would have shown");
        }

        /// <summary>
        /// On a card, an injury is written onto the wrestler, into their permanent history,
        /// and onto the show report — where it belongs above the rating, because a rating is
        /// a number and somebody being out for six months is next month's booking.
        /// </summary>
        [Fact]
        public void AShowWritesTheInjuryDown()
        {
            var roster = new List<Wrestler> { W("Alpha", WrestlingStyle.HighFlyer), W("Bravo") };
            roster[0].Fatigue = 95;

            ShowResult? withInjury = null;
            for (int i = 0; i < 400 && withInjury is null; i++)
            {
                var career = NewCareer(roster);
                roster[0].Injury = null; roster[0].InjuryHistory.Clear();

                var show = career.Schedule("Night", career.CurrentDate, ShowType.Television);
                show.Card.Add(new BookedMatch { Plan = Singles(roster[0], roster[1], "Epic") });

                var r = new ShowSimulator(career.FeudBook, seed: Seed + i,
                                          tier: PromotionTier.Local).Simulate(show.ToShow());
                if (r.Injuries.Count > 0) withInjury = r;
            }

            Assert.NotNull(withInjury);
            var report = withInjury!.Injuries[0];

            output.WriteLine($"  {report.Wrestler.RingName}: {report.Injury.Describe(Day0)}");
            foreach (var item in withInjury.Items)
                foreach (var note in item.Notes.Where(n => n.Contains("hurt")))
                    output.WriteLine($"  note: {note}");

            Assert.Same(report.Wrestler.Injury, report.Injury);
            Assert.Contains(report.Injury, report.Wrestler.InjuryHistory);
            Assert.True(report.Injury.KeepsOut(Day0));

            // Dated from the night it happened, and the weeks the engine reported are
            // weeks. A mutation that clears them after that many *days* leaves every
            // assertion above true and puts a torn ACL back in the ring inside two months.
            Assert.Equal(Day0, report.Injury.Sustained);
            Assert.Equal(Day0.AddDays(report.Injury.WeeksOut * 7), report.Injury.ClearedOn);
            Assert.InRange(report.Injury.WeeksOut, 1, 52);
            Assert.Contains(withInjury.Items.SelectMany(i => i.Notes), n => n.Contains("hurt"));
        }

        /// <summary>
        /// **Healing is read off the calendar, not counted down.**
        ///
        /// The clock in this game does not always tick a day at a time — running a show
        /// three weeks out moves the date straight there. Anything decremented once per
        /// <c>AdvanceOneDay</c> would quietly skip those weeks and hold somebody out
        /// forever, or clear them early.
        /// </summary>
        [Fact]
        public void HealingIsReadOffTheCalendarAndNotCountedDown()
        {
            var w = W("Hurt");
            w.Injury = Hurt(BodyPart.Knee, weeks: 30);

            Assert.True(w.IsInjured(Day0));
            Assert.True(w.IsInjured(Day0.AddDays(29 * 7)));
            Assert.False(w.IsInjured(Day0.AddDays(30 * 7)));

            output.WriteLine($"  day 0: {w.Injury.Describe(Day0)}");
            output.WriteLine($"  +29w:  {w.Injury.Describe(Day0.AddDays(29 * 7))}");
            output.WriteLine($"  +30w:  {w.Injury.Describe(Day0.AddDays(30 * 7))}");

            // Part of a week left still reads as a week. Truncating instead of rounding up
            // puts "out 0 weeks" on the roster sheet beside somebody the builder refuses to
            // book, which is the game contradicting itself in two lines of the same screen.
            for (int days = 1; days <= 6; days++)
            {
                string reading = w.Injury.Describe(Day0.AddDays(30 * 7 - days));
                Assert.DoesNotContain("0 weeks", reading);
                Assert.Contains("back next week", reading);
            }
            output.WriteLine($"  3 days left: {w.Injury.Describe(Day0.AddDays(30 * 7 - 3))}");

            // And a career that jumps the clock straight past it still clears them.
            var career = NewCareer([w]);
            career.CurrentDate = Day0.AddDays(40 * 7);
            Assert.False(w.IsInjured(career.CurrentDate));
        }

        /// <summary>
        /// An injured wrestler cannot be booked. Everything else in the builder warns and
        /// lets you do it anyway; this one refuses, because a game that lets you book a
        /// wrestler in a sling is lying about its own injury model.
        /// </summary>
        [Fact]
        public void AnInjuredWrestlerIsUnavailable()
        {
            var w = W("Hurt");
            w.Injury = Hurt(BodyPart.Shoulder, weeks: 20);

            var reading = BookingSuggestions.Rank(
                [w], against: [], feuds: new FeudBook(), bookedAs: _ => null,
                standingPartnerOf: _ => null, recentlyBooked: new HashSet<Wrestler>(),
                today: Day0).Single();

            output.WriteLine($"  picker says: {reading.Condition}");
            Assert.Contains("unavailable", reading.Condition!);
            Assert.Contains("shoulder", reading.Condition!.ToLowerInvariant());

            // And once cleared, it goes back to whatever the meters say.
            var later = BookingSuggestions.Rank(
                [w], against: [], feuds: new FeudBook(), bookedAs: _ => null,
                standingPartnerOf: _ => null, recentlyBooked: new HashSet<Wrestler>(),
                today: Day0.AddDays(21 * 7)).Single();
            Assert.DoesNotContain("unavailable", later.Condition ?? "");
        }

        /// <summary>
        /// **Coming back is rusty, and the two systems already did that.**
        ///
        /// An injury is months off, and months off is exactly what the sharpness meter
        /// charges for. Nothing was added to make this true — it is what happens when a
        /// wrestler stops working, and the injury model only had to take them out.
        /// </summary>
        [Fact]
        public void ComingBackFromAnInjuryIsComingBackRusty()
        {
            var roster = new List<Wrestler> { W("Hurt"), W("Other") };
            var career = NewCareer(roster);

            roster[0].Injury = Hurt(BodyPart.Knee, weeks: 30);
            double before = roster[0].Sharpness;

            for (int i = 0; i < 30 * 7; i++) career.AdvanceOneDay();

            output.WriteLine($"  sharpness {before:F0} → {roster[0].Sharpness:F0} after thirty weeks out");
            output.WriteLine($"  fatigue   → {roster[0].Fatigue:F0}");

            Assert.True(roster[0].Sharpness < before - 20, "months out should cost real sharpness");
            Assert.False(roster[0].IsInjured(career.CurrentDate));
        }

        /// <summary>
        /// Injuries and the permanent history survive a save, or a reload is a miracle cure
        /// and doc 15's most important detail is a session variable.
        /// </summary>
        [Fact]
        public void InjuriesAndHistorySurviveASaveAndReload()
        {
            var roster = new List<Wrestler> { W("Alpha"), W("Bravo") };
            var career = NewCareer(roster);

            roster[0].Injury = Hurt(BodyPart.Neck, weeks: 34);
            roster[0].InjuryHistory.Add(Hurt(BodyPart.Back, weeks: 6));
            roster[0].InjuryHistory.Add(roster[0].Injury!);

            var back = SaveSerializer.FromJson(SaveSerializer.ToJson(career), roster)
                .Roster.Single(w => w.Id == roster[0].Id);

            output.WriteLine($"  {back.Injury?.Describe(Day0)}, history {back.InjuryHistory.Count}");

            Assert.NotNull(back.Injury);
            Assert.Equal(BodyPart.Neck, back.Injury!.Part);
            Assert.Equal(34, back.Injury.WeeksOut);
            Assert.True(back.Injury.KeepsOut(Day0));
            Assert.Equal(2, back.InjuryHistory.Count);
            Assert.Contains(back.InjuryHistory, i => i.Part == BodyPart.Back);

            // And the history still weighs on what happens next, which is the whole point
            // of keeping it.
            Assert.True(InjuryRisk.HistoryRisk(back.InjuryHistory, BodyPart.Neck) > 1.8);
        }

        /// <summary>A save written before injuries existed reads back as a fit roster.</summary>
        [Fact]
        public void AnOlderSaveLoadsAsFit()
        {
            var roster = new List<Wrestler> { W("Alpha") };
            var career = NewCareer(roster);
            roster[0].Injury = Hurt(BodyPart.Knee, 40);

            string json = SaveSerializer.ToJson(career);
            json = System.Text.RegularExpressions.Regex.Replace(
                json, @"\s*""Injury"":\s*\{[^}]*\},", "");
            json = System.Text.RegularExpressions.Regex.Replace(
                json, @"\s*,?\s*""InjuryHistory"":\s*\[[^\]]*\]", "");

            var back = SaveSerializer.FromJson(json, roster).Roster.Single();
            output.WriteLine($"  legacy save: injury {(back.Injury is null ? "none" : "set")}, " +
                             $"history {back.InjuryHistory.Count}");

            Assert.Null(back.Injury);
            Assert.Empty(back.InjuryHistory);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static MatchPlan Singles(Wrestler a, Wrestler b, string structure) => new()
        {
            Sides = [new MatchSide { Members = [a] }, new MatchSide { Members = [b] }],
            Beats = MatchStructureLibrary.Find(structure)!.Beats.Select(x => x.Clone()).ToList()
        };

        private static Career NewCareer(List<Wrestler> roster) => new()
        {
            Promotion   = new Promotion { Name = "Hard Way Wrestling" },
            StartDate   = Day0,
            CurrentDate = Day0,
            Roster      = roster
        };
    }
}
