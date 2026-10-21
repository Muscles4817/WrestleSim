using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **What the match promised, which nobody declares.**
    ///
    /// The builder used to have a step where the booker picked a match type, and the engine
    /// then graded the beats against that pick. It was worth up to eight points and it was
    /// the least understood thing in the game, for a good reason: it is not a decision a
    /// booker has. A promotion never announces that tonight's semi-main will be a technical
    /// classic. The promise is made by who is in the match, what the feud has been, what the
    /// rules are and what is at stake, and it is kept or broken entirely by the booking.
    ///
    /// So the expectation is derived and the beat sheet is the delivery, and what the engine
    /// grades is the distance between them.
    /// </summary>
    public class MatchExpectationTests(ITestOutputHelper output)
    {
        private static Wrestler W(string name, WrestlingStyle style, int size = 3,
                                  int psych = 75, int ringIq = 75)
        {
            var w = TestRoster.Make(name, psychology: psych);
            w.Style = style;
            w.Physical!.Size = size;
            w.Mental!.Psychology = psych;
            w.Mental!.RingIQ = ringIq;
            return w;
        }

        private static List<MatchSide> Sides(Wrestler a, Wrestler b) =>
            [new MatchSide { Members = [a] }, new MatchSide { Members = [b] }];

        private static List<MatchSide> Styles(WrestlingStyle a, WrestlingStyle b,
                                              int sizeA = 3, int sizeB = 3) =>
            Sides(W("Alpha", a, sizeA), W("Bravo", b, sizeB));

        // ── Where a promise comes from ───────────────────────────────────────

        /// <summary>
        /// **The bodies in the ring are the promise nobody can avoid making.**
        ///
        /// An audience reads a card by the names on it long before it reads anything else,
        /// and doc 18 §4 has style deciding what a match can even be. Two mat wrestlers
        /// promise a contest whether or not anybody intended to promise one.
        /// </summary>
        [Theory]
        [InlineData(WrestlingStyle.Technical, WrestlingStyle.Grappler,   MatchStory.TechnicalExhibition)]
        [InlineData(WrestlingStyle.Technical, WrestlingStyle.Technical,  MatchStory.TechnicalExhibition)]
        [InlineData(WrestlingStyle.HighFlyer, WrestlingStyle.HighFlyer,  MatchStory.Spectacle)]
        [InlineData(WrestlingStyle.Brawler,   WrestlingStyle.Powerhouse, MatchStory.Grudge)]
        public void TheStylesInItPromiseSomething(WrestlingStyle a, WrestlingStyle b, MatchStory wanted)
        {
            var reading = MatchExpectation.Of(Styles(a, b));

            output.WriteLine($"  {a} v {b}: {reading.Wants} ({reading.Strength:F2}) — {reading.Reading}");

            Assert.Equal(wanted, reading.Wants);
            Assert.True(reading.Strength >= MatchExpectation.VagueBelow);
        }

        /// <summary>
        /// A giant against somebody much smaller is the only thing anybody is looking at,
        /// whatever the two of them wrestle like.
        /// </summary>
        [Fact]
        public void SizeSpeaksOverStyle()
        {
            var reading = MatchExpectation.Of(
                Styles(WrestlingStyle.Technical, WrestlingStyle.Technical, sizeA: 1, sizeB: 5));

            output.WriteLine($"  {reading.Wants} ({reading.Strength:F2}) — {reading.Reading}");

            Assert.Equal(MatchStory.DavidAndGoliath, reading.Wants);
        }

        /// <summary>
        /// Two names with nothing between them promise nothing, and the model says so rather
        /// than inventing a standard to fail them by. This is the case the old declared type
        /// handled worst: `Standard` was the safe pick and everybody learned to take it.
        /// </summary>
        [Fact]
        public void AMixedPairingWithNoStoryPromisesNothingWorthHoldingThemTo()
        {
            var reading = MatchExpectation.Of(Styles(WrestlingStyle.Striker, WrestlingStyle.Powerhouse));

            output.WriteLine($"  {reading.Wants} ({reading.Strength:F2}) — {MatchExpectation.Say(reading)}");

            Assert.True(reading.Strength < 0.4);
            Assert.Equal(0.0, MatchExpectation.Reward(reading with { Strength = 0.1 },
                                                      MatchStory.TechnicalExhibition, 0.5));
        }

        /// <summary>
        /// **A hot feud talks over the bodies.** Doc 20: the story is what the crowd has
        /// been following, and by the time it is nuclear they are not there for a wrestling
        /// match.
        /// </summary>
        [Theory]
        [InlineData(FeudIntensity.Nuclear,  MatchStory.Grudge)]
        [InlineData(FeudIntensity.Hot,      MatchStory.Grudge)]
        [InlineData(FeudIntensity.Building, MatchStory.FaceInPeril)]
        public void TheFeudTalksOverTheBodies(FeudIntensity intensity, MatchStory wanted)
        {
            // Two mat wrestlers, who would otherwise promise a technical contest.
            var sides = Styles(WrestlingStyle.Technical, WrestlingStyle.Technical);
            var feud = new Feud
            {
                SideA = [sides[0].Members[0]], SideB = [sides[1].Members[0]], Intensity = intensity
            };

            var reading = MatchExpectation.Of(sides, feud);

            output.WriteLine($"  {intensity,-9} {reading.Wants} ({reading.Strength:F2}) — {reading.Reading}");

            Assert.Equal(wanted, reading.Wants);
        }

        /// <summary>
        /// A cold feud is not a promise. It is two people who have met before, and the crowd
        /// reads the match off the bodies as it would have anyway.
        /// </summary>
        [Fact]
        public void AColdFeudIsNotAPromise()
        {
            var sides = Styles(WrestlingStyle.Technical, WrestlingStyle.Technical);
            var feud = new Feud
            {
                SideA = [sides[0].Members[0]], SideB = [sides[1].Members[0]], Intensity = FeudIntensity.Cold
            };

            Assert.Equal(MatchStory.TechnicalExhibition, MatchExpectation.Of(sides, feud).Wants);
        }

        /// <summary>
        /// **The rules are the loudest promise, because they are printed on the poster.**
        /// Doc 20 §6 has the gimmick match as the thing a feud escalates into: by the time
        /// there is a cage, the audience already knows what it is for.
        /// </summary>
        [Theory]
        [InlineData(Stipulation.SteelCage)]
        [InlineData(Stipulation.LastManStanding)]
        [InlineData(Stipulation.IQuit)]
        [InlineData(Stipulation.NoDisqualification)]
        public void TheRulesAreTheLoudestPromise(Stipulation stipulation)
        {
            // Two technicians with no story, who would otherwise promise a mat contest.
            var sides = Styles(WrestlingStyle.Technical, WrestlingStyle.Technical);
            var reading = MatchExpectation.Of(sides, feud: null, stipulation);

            output.WriteLine($"  {stipulation,-20} {reading.Wants} ({reading.Strength:F2}) — {reading.Reading}");

            Assert.Equal(MatchStory.Grudge, reading.Wants);
            Assert.True(reading.Strength >= 0.7, "a stipulation is not a subtle hint");
        }

        /// <summary>
        /// A belt firms up the promise rather than changing it. Doc 21 §2: a title match's
        /// job is to feel like it matters, which raises the bar without saying what kind of
        /// match to have.
        /// </summary>
        [Fact]
        public void ABeltFirmsUpThePromiseWithoutChangingIt()
        {
            var sides = Styles(WrestlingStyle.Technical, WrestlingStyle.Grappler);

            var plain = MatchExpectation.Of(sides);
            var forABelt = MatchExpectation.Of(sides, feud: null, Stipulation.None, forATitle: true);

            output.WriteLine($"  without {plain.Strength:F2}, with {forABelt.Strength:F2}");
            output.WriteLine($"  {forABelt.Reading}");

            Assert.Equal(plain.Wants, forABelt.Wants);
            Assert.True(forABelt.Strength > plain.Strength);
            Assert.Contains("belt", forABelt.Reading);
        }

        // ── Keeping it, and breaking it ──────────────────────────────────────

        /// <summary>
        /// Giving the crowd the match it came for pays, and the louder the promise the more
        /// it pays — a loud promise kept is the match they were waiting for.
        /// </summary>
        [Fact]
        public void GivingThemWhatTheyCameForPays()
        {
            var quiet = new Expectation(MatchStory.Grudge, 0.4, "");
            var loud  = new Expectation(MatchStory.Grudge, 0.95, "");

            double quietPay = MatchExpectation.Reward(quiet, MatchStory.Grudge, 0.7);
            double loudPay  = MatchExpectation.Reward(loud,  MatchStory.Grudge, 0.7);

            output.WriteLine($"  quiet promise kept {quietPay:F2}, loud promise kept {loudPay:F2}");

            Assert.True(quietPay > 0);
            Assert.True(loudPay > quietPay);

            // **And keeping it pays the same whoever is working.** Only defying the room is
            // a question of skill; giving a crowd the match it came for is not a trick. A
            // mutant that removed the kept-promise branch and let the miss arithmetic run on
            // a distance of zero still produced a positive number here, and only this
            // catches it — under that version the payment varied with who was in the ring.
            foreach (double skill in new[] { 0.2, 0.5, 0.95 })
                Assert.Equal(loudPay, MatchExpectation.Reward(loud, MatchStory.Grudge, skill), 6);
        }

        /// <summary>
        /// **Booking the wrong match costs, and the stories are not equidistant.**
        ///
        /// A crowd promised a fight will take a face-in-peril, which is still a story about
        /// somebody suffering. It will not take a mat classic, which is a different evening.
        /// </summary>
        [Fact]
        public void HowBadlyYouMissedItDependsOnWhatYouGaveThemInstead()
        {
            var wantsAFight = new Expectation(MatchStory.Grudge, 0.9, "");

            double nearMiss = MatchExpectation.Reward(wantsAFight, MatchStory.FaceInPeril, 0.6);
            double wideMiss = MatchExpectation.Reward(wantsAFight, MatchStory.TechnicalExhibition, 0.6);

            output.WriteLine($"  a fight, given a face-in-peril {nearMiss:F2}");
            output.WriteLine($"  a fight, given a mat classic  {wideMiss:F2}");

            Assert.True(wideMiss < nearMiss);
            Assert.True(wideMiss < -2.0, "the wide miss has to actually hurt");
        }

        /// <summary>
        /// **Going against the room is a gamble on the performers, not a flat penalty.**
        ///
        /// Doc 18 §3.2: great workers adjust in real time, which is exactly the skill of
        /// giving a crowd something it did not ask for and winning it round. Limited ones
        /// cannot, so the identical booking is a triumph for one pair and a disaster for
        /// another. This is the thing the declared match type could not express at all — it
        /// applied the same penalty to everybody.
        /// </summary>
        [Fact]
        public void GreatWorkersCanWinTheRoundRoundAndLimitedOnesCannot()
        {
            var wantsAFight = new Expectation(MatchStory.Grudge, 0.9, "");

            double byMasters = MatchExpectation.Reward(wantsAFight, MatchStory.TechnicalExhibition, 0.95);
            double byJourneymen = MatchExpectation.Reward(wantsAFight, MatchStory.TechnicalExhibition, 0.75);
            double byLimited = MatchExpectation.Reward(wantsAFight, MatchStory.TechnicalExhibition, 0.4);

            output.WriteLine($"  the same defiant booking: masters {byMasters:F2}, " +
                             $"journeymen {byJourneymen:F2}, limited {byLimited:F2}");

            Assert.True(byMasters > byJourneymen);
            Assert.True(byJourneymen > byLimited);
            Assert.True(byLimited < -5.0, "a pair who cannot work the room should not try");
        }

        /// <summary>
        /// A match is carried at the pace of whoever is least able to change it, so one
        /// great worker cannot rescue a mismatched booking alone.
        /// </summary>
        [Fact]
        public void TheWeakestLinkDecidesWhetherItCanBeCarried()
        {
            var master = W("Master", WrestlingStyle.Technical, psych: 95, ringIq: 95);
            var green  = W("Green",  WrestlingStyle.Brawler,   psych: 35, ringIq: 35);

            double pair = MatchExpectation.CanCarryIt(Sides(master, green));
            double both = MatchExpectation.CanCarryIt(Sides(master, W("Other", WrestlingStyle.Technical,
                                                                     psych: 95, ringIq: 95)));

            output.WriteLine($"  master with a green partner {pair:F2}, two masters {both:F2}");

            Assert.True(pair < 0.5);
            Assert.True(both > 0.9);
        }

        /// <summary>
        /// A promise too vague to hold anybody to costs nothing and pays nothing, whatever
        /// they book. Two midcarders with no history are free to have any match they like.
        /// </summary>
        [Fact]
        public void AVaguePromiseIsNeitherKeptNorBroken()
        {
            var vague = new Expectation(MatchStory.Grudge, MatchExpectation.VagueBelow - 0.01, "");

            foreach (MatchStory booked in Enum.GetValues<MatchStory>())
                Assert.Equal(0.0, MatchExpectation.Reward(vague, booked, 0.5));
        }

        // ── Through the engine ───────────────────────────────────────────────

        /// <summary>
        /// **A plan built by hand made no promise, so nothing is graded against it.**
        ///
        /// This is what let the term be added to the engine without moving a single one of
        /// the 802 tests that existed before it: they all build plans directly, so
        /// <see cref="MatchPlan.Brief"/> is null and the whole mechanism is inert. A scoring
        /// change with no blast radius is not luck, it is the null case being the honest
        /// answer rather than a convenient one.
        /// </summary>
        [Fact]
        public void AHandBuiltPlanIsGradedAgainstNothing()
        {
            var plan = Plan(brief: null);
            var result = new MatchEngine(StableSeed.From("expectation")).Execute(plan);

            output.WriteLine($"  promised: {result.Promised?.ToString() ?? "nothing"}, " +
                             $"nudge {result.ExpectationNudge:F2}");

            Assert.Null(result.Promised);
            Assert.Equal(0.0, result.ExpectationNudge);
        }

        /// <summary>
        /// And a plan booked from a brief is. The same beats, the same cast, the same seed —
        /// the only difference is that somebody said what they were trying to do.
        /// </summary>
        [Fact]
        public void ABriefedPlanIsGradedAgainstWhatTheMatchPromised()
        {
            var brief = new MatchBrief { Story = MatchStory.TechnicalExhibition };
            var plan = Plan(brief);

            // Two brawlers in a hot feud: the crowd wants a fight.
            plan.Sides[0].Members[0].Style = WrestlingStyle.Brawler;
            plan.Sides[1].Members[0].Style = WrestlingStyle.Powerhouse;
            plan.Feuds.Add(new Feud
            {
                SideA     = [plan.Sides[0].Members[0]],
                SideB     = [plan.Sides[1].Members[0]],
                Intensity = FeudIntensity.Hot
            });

            var result = new MatchEngine(StableSeed.From("expectation")).Execute(plan);

            output.WriteLine($"  promised {result.Promised?.Wants} " +
                             $"({result.Promised?.Strength:F2}), booked {brief.Story}");
            output.WriteLine($"  nudge {result.ExpectationNudge:F2}");

            Assert.NotNull(result.Promised);
            Assert.Equal(MatchStory.Grudge, result.Promised!.Value.Wants);
            Assert.True(result.ExpectationNudge < 0, "a mat classic is not what they came for");
        }

        /// <summary>
        /// **The same beats score differently depending on what they were promised to be.**
        ///
        /// The isolation matters and the first version of this test did not have it. It
        /// generated two plans from two different briefs and compared their scores, so the
        /// beats differed as well as the promise — and removing the expectation term from the
        /// final score entirely did not fail it, because the beats alone were carrying the
        /// difference. A test that passes with the mechanism deleted is testing something
        /// else.
        ///
        /// So: one beat sheet, generated once, run twice with a different brief attached.
        /// Nothing else in the engine can see a difference, which makes the score gap the
        /// expectation term and nothing but.
        ///
        /// It is a real scenario too, not a contrivance. A booker generates a mat classic,
        /// hand-edits it into a brawl, and the brief still records what they said they were
        /// doing.
        /// </summary>
        [Fact]
        public void TheSameBeatsScoreDifferentlyAgainstADifferentPromise()
        {
            var sides = Styles(WrestlingStyle.Brawler, WrestlingStyle.Powerhouse);

            // One sheet, generated once.
            var beats = BriefDirector.Write(
                new MatchBrief { Story = MatchStory.Grudge, Length = MatchScale.Workhorse }, sides).Beats;

            double Score(MatchStory claimed)
            {
                var plan = new MatchPlan
                {
                    Sides       = sides,
                    Beats       = beats.Select(b => b.Clone()).ToList(),
                    Stipulation = Stipulation.SteelCage,
                    Brief       = new MatchBrief { Story = claimed }
                };
                return new MatchEngine(StableSeed.From("room")).Execute(plan).FinalScore;
            }

            double served = Score(MatchStory.Grudge);
            double defied = Score(MatchStory.TechnicalExhibition);

            output.WriteLine($"  identical beats in a cage: promised a grudge {served:F2}, " +
                             $"promised a mat classic {defied:F2}");

            Assert.True(served > defied,
                        "the only difference is what the match said it was going to be");
        }

        /// <summary>A plain workhorse plan, with or without a brief attached.</summary>
        private static MatchPlan Plan(MatchBrief? brief)
        {
            var sides = Styles(WrestlingStyle.Brawler, WrestlingStyle.Technical);
            var written = BriefDirector.Write(
                brief ?? new MatchBrief { Length = MatchScale.Workhorse }, sides);

            return new MatchPlan
            {
                Sides = sides,
                Beats = written.Beats.ToList(),
                Brief = brief
            };
        }
    }
}
