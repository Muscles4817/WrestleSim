using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Integration tests that run two iconic real-world matches through the engine
    /// and print a full play-by-play so we can see how the output reads and tune
    /// constants accordingly.
    ///
    /// Seed is fixed so results are 100% reproducible. Change the seed when you
    /// want to see how the same booking plays out with different RNG luck.
    /// </summary>
    public class MatchEngineTests(ITestOutputHelper output)
    {
        private const int Seed = 42;

        // ── Shared wrestler factory helpers ──────────────────────────────────

        // ---- WrestleMania XX (2004) -----------------------------------------

        private static Wrestler MakeGoldbergWM20() => new(
            realName : "Bill Goldberg",
            gimmick  : new Gimmick("Goldberg")
            {
                NaturalAlignment = Alignment.Face,
                AppealRatings    =
                [
                    // MSG knew he was leaving; "You sold out!" chants all match
                    new FanGroupAppeal { Group = "Casuals",      AppealScore = 0.25 },
                    new FanGroupAppeal { Group = "Hardcores",    AppealScore = 0.18 },
                    new FanGroupAppeal { Group = "LongTermFans", AppealScore = 0.15 }
                ]
            },
            overness: 48, // real pop tanked by the walk-out context
            ringSkills : new RingSkills(
                highFlyer  : 0.5,
                grappler   : 1.5,
                powerHouse : 4.8,
                technical  : 1.2,
                brawler    : 3.5,
                striker    : 3.0
            ),
            charisma : 3.5,
            style    : WrestlingStyle.Powerhouse
        )
        {
            Mental = new Models.Person.MentalAttributes
            {
                Psychology = 35, // notoriously thin on psychology
                Selling    = 32,
                RingIQ     = 42,
                Toughness  = 88
            }
        };

        private static Wrestler MakeBrockWM20() => new(
            realName : "Brock Lesnar",
            gimmick  : new Gimmick("Brock Lesnar")
            {
                NaturalAlignment = Alignment.Heel,
                AppealRatings    =
                [
                    // "Goodbye!" chants — crowd sarcastically celebrated his departure
                    new FanGroupAppeal { Group = "Casuals",      AppealScore = 0.22 },
                    new FanGroupAppeal { Group = "Hardcores",    AppealScore = 0.28 },
                    new FanGroupAppeal { Group = "LongTermFans", AppealScore = 0.18 }
                ]
            },
            overness: 50, // also being booed — leaving for the NFL
            ringSkills : new RingSkills(
                highFlyer  : 1.0,
                grappler   : 4.8,
                powerHouse : 4.9,
                technical  : 3.6,
                brawler    : 4.0,
                striker    : 3.5
            ),
            charisma : 3.8,
            style    : WrestlingStyle.Powerhouse
        )
        {
            Mental = new Models.Person.MentalAttributes
            {
                Psychology = 70,
                Selling    = 62,
                RingIQ     = 75,
                Toughness  = 95
            }
        };

        // ---- WrestleMania 34 (2018) -----------------------------------------

        private static Wrestler MakeRomanWM34() => new(
            realName : "Joe Anoa'i",
            gimmick  : new Gimmick("Roman Reigns")
            {
                NaturalAlignment = Alignment.Face,
                AppealRatings    =
                [
                    // Three-year "chosen one" push the crowd had fully rejected
                    new FanGroupAppeal { Group = "Casuals",      AppealScore = 0.38 },
                    new FanGroupAppeal { Group = "Hardcores",    AppealScore = 0.20 },
                    new FanGroupAppeal { Group = "LongTermFans", AppealScore = 0.22 },
                    new FanGroupAppeal { Group = "Children",     AppealScore = 0.58 }
                ]
            },
            overness: 72,
            ringSkills : new RingSkills(
                highFlyer  : 1.5,
                grappler   : 3.0,
                powerHouse : 4.5,
                technical  : 3.2,
                brawler    : 4.2,
                striker    : 3.8
            ),
            charisma : 3.9,
            style    : WrestlingStyle.Powerhouse
        )
        {
            Mental = new Models.Person.MentalAttributes
            {
                Psychology = 68,
                Selling    = 72,
                RingIQ     = 72,
                Toughness  = 88
            }
        };

        private static Wrestler MakeBrockWM34() => new(
            realName : "Brock Lesnar",
            gimmick  : new Gimmick("Brock Lesnar")
            {
                NaturalAlignment = Alignment.Heel,
                AppealRatings    =
                [
                    // Ironically beloved — crowd cheered every German suplex on Roman
                    new FanGroupAppeal { Group = "Casuals",      AppealScore = 0.80 },
                    new FanGroupAppeal { Group = "Hardcores",    AppealScore = 0.84 },
                    new FanGroupAppeal { Group = "LongTermFans", AppealScore = 0.76 }
                ]
            },
            overness: 85, // champion, widely cheered despite the heel character
            ringSkills : new RingSkills(
                highFlyer  : 1.0,
                grappler   : 5.0,
                powerHouse : 5.0,
                technical  : 3.8,
                brawler    : 4.2,
                striker    : 3.8
            ),
            charisma : 4.2,
            style    : WrestlingStyle.Grappler
        )
        {
            Mental = new Models.Person.MentalAttributes
            {
                Psychology = 78,
                Selling    = 68,
                RingIQ     = 80,
                Toughness  = 95
            }
        };

        // ---- Good-match wrestlers -------------------------------------------

        private static Wrestler MakeRhea() => new(
            realName : "Demi Bennett",
            gimmick  : new Gimmick("Rhea Ripley")
            {
                NaturalAlignment = Alignment.Heel,
                AppealRatings    =
                [
                    new FanGroupAppeal { Group = "Hardcores",    AppealScore = 0.92 },
                    new FanGroupAppeal { Group = "Casuals",      AppealScore = 0.75 },
                    new FanGroupAppeal { Group = "ChildrenKids", AppealScore = 0.45 }
                ]
            },
            overness: 90,
            ringSkills : new RingSkills(
                highFlyer  : 2.5,
                grappler   : 3.5,
                powerHouse : 4.8,
                technical  : 3.2,
                brawler    : 4.5,
                striker    : 3.8
            ),
            charisma : 4.6,
            style    : WrestlingStyle.Powerhouse
        )
        {
            Mental = new Models.Person.MentalAttributes
            {
                Psychology = 82,
                Selling    = 75,
                RingIQ     = 78,
                Toughness  = 90
            }
        };

        private static Wrestler MakeBecky() => new(
            realName : "Rebecca Quin",
            gimmick  : new Gimmick("Becky Lynch")
            {
                NaturalAlignment = Alignment.Face,
                AppealRatings    =
                [
                    new FanGroupAppeal { Group = "Hardcores",    AppealScore = 0.89 },
                    new FanGroupAppeal { Group = "Casuals",      AppealScore = 0.91 },
                    new FanGroupAppeal { Group = "ChildrenKids", AppealScore = 0.78 }
                ]
            },
            overness: 92,
            ringSkills : new RingSkills(
                highFlyer  : 2.8,
                grappler   : 3.8,
                powerHouse : 2.6,
                technical  : 3.9,
                brawler    : 4.2,
                striker    : 4.0
            ),
            charisma : 4.9,
            style    : WrestlingStyle.Brawler
        )
        {
            Mental = new Models.Person.MentalAttributes
            {
                Psychology = 88,
                Selling    = 85,
                RingIQ     = 82,
                Toughness  = 80
            }
        };

        private static Wrestler MakeCharlotte() => new(
            realName : "Ashley Fliehr",
            gimmick  : new Gimmick("Charlotte Flair")
            {
                NaturalAlignment = Alignment.Heel,
                AppealRatings    =
                [
                    new FanGroupAppeal { Group = "Hardcores",    AppealScore = 0.68 },
                    new FanGroupAppeal { Group = "Casuals",      AppealScore = 0.72 },
                    new FanGroupAppeal { Group = "LongTermFans", AppealScore = 0.80 }
                ]
            },
            overness: 85,
            ringSkills : new RingSkills(
                highFlyer  : 3.2,
                grappler   : 4.0,
                powerHouse : 3.5,
                technical  : 4.6,
                brawler    : 3.0,
                striker    : 3.2
            ),
            charisma : 4.3,
            style    : WrestlingStyle.Technical
        )
        {
            Mental = new Models.Person.MentalAttributes
            {
                Psychology = 85,
                Selling    = 72,
                RingIQ     = 88,
                Toughness  = 78
            }
        };

        private static Wrestler MakeAsuka() => new(
            realName : "Kanako Urai",
            gimmick  : new Gimmick("Asuka")
            {
                NaturalAlignment = Alignment.Face,
                AppealRatings    =
                [
                    new FanGroupAppeal { Group = "Hardcores",    AppealScore = 0.91 },
                    new FanGroupAppeal { Group = "InternationalFans", AppealScore = 0.88 },
                    new FanGroupAppeal { Group = "Casuals",      AppealScore = 0.65 }
                ]
            },
            overness: 82,
            ringSkills : new RingSkills(
                highFlyer  : 3.5,
                grappler   : 4.2,
                powerHouse : 2.5,
                technical  : 4.8,
                brawler    : 3.0,
                striker    : 4.7
            ),
            charisma : 4.1,
            style    : WrestlingStyle.Striker
        )
        {
            Mental = new Models.Person.MentalAttributes
            {
                Psychology = 90,
                Selling    = 80,
                RingIQ     = 92,
                Toughness  = 85
            }
        };

        // ── Helper: print full play-by-play + scorecard ──────────────────────

        private void PrintResult(MatchEngineResult result, string matchTitle)
        {
            output.WriteLine("");
            output.WriteLine(new string('═', 70));
            output.WriteLine($"  {matchTitle}");
            output.WriteLine(new string('═', 70));
            output.WriteLine("");

            foreach (var line in result.PlayByPlay)
                output.WriteLine(line);

            output.WriteLine(new string('─', 70));
            output.WriteLine("  SCORECARD");
            output.WriteLine(new string('─', 70));
            output.WriteLine($"  Winner         : {result.Winner.RingName}");
            output.WriteLine($"  Technical      : {result.Bar(result.TechnicalScore, 60)}  {result.TechnicalScore:F1} / 60");
            output.WriteLine($"  Storytelling   : {result.Bar(result.StorytellingScore, 80)}  {result.StorytellingScore:F1} / 80");
            output.WriteLine($"  Crowd Peak     : {result.Bar(result.CrowdPeakEnergy)}  {result.CrowdPeakEnergy:F1}");
            output.WriteLine($"  Crowd Average  : {result.Bar(result.CrowdAverageEnergy)}  {result.CrowdAverageEnergy:F1}");
            output.WriteLine($"  Finish Quality : {result.Bar(result.FinishQuality)}  {result.FinishQuality:F1}");
            output.WriteLine($"  Final Score    : {result.Bar(result.FinalScore)}  {result.FinalScore:F1} / 100");
            output.WriteLine($"  Star Rating    : {result.StarDisplay}");
            output.WriteLine(new string('═', 70));
            output.WriteLine("");
        }

        // ────────────────────────────────────────────────────────────────────
        // MATCH 1 — Rhea Ripley vs Becky Lynch
        // WrestleMania XL Night One (approx.)
        // Hot feud: Championship stolen + Personal insults
        // Rhea is A (heel in control initially), Becky is B (babyface)
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void RheaVsBecky_HotFeudChampionshipMatch()
        {
            var rhea  = MakeRhea();
            var becky = MakeBecky();

            var feud = new Feud
            {
                WrestlerA = rhea,
                WrestlerB = becky,
                Intensity = FeudIntensity.Hot,
                History   =
                [
                    FeudHistoryTag.ChampionshipRivalry,
                    FeudHistoryTag.PersonalInsult,
                    FeudHistoryTag.PriorMatch
                ],
                MatchCount = 2
            };

            var escalationResonance = new FeudalResonance
            {
                ResonanceType      = FeudalResonanceType.Escalation,
                RequiredHistoryTags = [FeudHistoryTag.ChampionshipRivalry, FeudHistoryTag.PersonalInsult]
            };

            var plan = new MatchPlan
            {
                WrestlerA = rhea,
                WrestlerB = becky,
                Feud      = feud,
                MatchType = MatchType.Standard,
                Beats     =
                [
                    // Bells rings and they immediately brawl
                    new MatchBeat
                    {
                        Type      = BeatType.HotOpening,
                        Control   = BeatControl.Even,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Short
                    },
                    // Rhea physically dominates, grounds Becky
                    new MatchBeat
                    {
                        Type      = BeatType.HeatSegment,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Long
                    },
                    // Becky fires back — the crowd explodes
                    new MatchBeat
                    {
                        Type      = BeatType.Comeback,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Short
                    },
                    // Becky nearly gets the pin
                    new MatchBeat
                    {
                        Type      = BeatType.NearFall,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Brief
                    },
                    // Rhea regains control, wears Becky down again
                    new MatchBeat
                    {
                        Type      = BeatType.HeatSegment,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Medium
                    },
                    // The feud history boils over — both going at it recklessly
                    new MatchBeat
                    {
                        Type             = BeatType.FeudalEscalation,
                        Control          = BeatControl.Even,
                        Intensity        = BeatIntensity.Extreme,
                        Duration         = BeatDuration.Short,
                        FeudalResonance  = escalationResonance
                    },
                    // Rhea capitalises on the chaos and nearly wins
                    new MatchBeat
                    {
                        Type      = BeatType.NearFall,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Brief
                    },
                    // Becky refuses to die — second comeback
                    new MatchBeat
                    {
                        Type      = BeatType.Comeback,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Short
                    },
                    // Becky goes for the DisArmHer — agonisingly close
                    new MatchBeat
                    {
                        Type      = BeatType.NearFall,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Brief
                    },
                    // Rhea reverses into Riptide — 1-2-3
                    new MatchBeat
                    {
                        Type      = BeatType.FinishClean,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Brief
                    }
                ]
            };

            var engine = new MatchEngine(Seed);
            var result = engine.Execute(plan);

            PrintResult(result, "RHEA RIPLEY vs BECKY LYNCH — Hot Feud Championship Match");

            Assert.Equal(rhea.RingName, result.Winner.RingName);
            Assert.Equal(becky.RingName, result.Loser.RingName);
            Assert.True(result.StarRating >= 2.5, $"Expected at least ★★½ for a hot feud main event, got {result.StarDisplay}");
        }

        // ────────────────────────────────────────────────────────────────────
        // MATCH 2 — Charlotte Flair vs Asuka
        // Technical showcase, no significant feud
        // Charlotte is A (arrogant heel), Asuka is B (respected face)
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void CharlotteVsAsuka_TechnicalShowcase()
        {
            var charlotte = MakeCharlotte();
            var asuka     = MakeAsuka();

            // No feud object — this is a pure technical match

            var plan = new MatchPlan
            {
                WrestlerA = charlotte,
                WrestlerB = asuka,
                MatchType = MatchType.Standard,
                Beats     =
                [
                    // Deliberate start, both measuring each other
                    new MatchBeat
                    {
                        Type      = BeatType.SlowOpening,
                        Control   = BeatControl.Even,
                        Intensity = BeatIntensity.Low,
                        Duration  = BeatDuration.Medium
                    },
                    // Charlotte uses size and strength to take over
                    new MatchBeat
                    {
                        Type      = BeatType.HeatSegment,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Medium,
                        Duration  = BeatDuration.Long
                    },
                    // Charlotte uses a rest hold to wear Asuka down
                    new MatchBeat
                    {
                        Type      = BeatType.RestHold,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Low,
                        Duration  = BeatDuration.Medium
                    },
                    // Asuka fights up out of the hold
                    new MatchBeat
                    {
                        Type      = BeatType.Comeback,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Short
                    },
                    // Asuka lands the Asuka Lock — Charlotte narrowly survives
                    new MatchBeat
                    {
                        Type      = BeatType.NearFall,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Brief
                    },
                    // Charlotte gets in Asuka's head with a calculated taunt
                    new MatchBeat
                    {
                        Type      = BeatType.PsychologicalWarfare,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Medium,
                        Duration  = BeatDuration.Brief
                    },
                    // Charlotte reasserts dominance with targeted limb work
                    new MatchBeat
                    {
                        Type      = BeatType.HeatSegment,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Medium
                    },
                    // Charlotte goes for the cover off a big boot
                    new MatchBeat
                    {
                        Type      = BeatType.NearFall,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Brief
                    },
                    // Asuka refuses to go down — strikes back hard
                    new MatchBeat
                    {
                        Type      = BeatType.Comeback,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Short
                    },
                    // Asuka so close with a roll-up counter
                    new MatchBeat
                    {
                        Type      = BeatType.NearFall,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Brief
                    },
                    // Charlotte locks in the Figure Eight — Asuka taps
                    new MatchBeat
                    {
                        Type      = BeatType.FinishSubmission,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Brief
                    }
                ]
            };

            var engine = new MatchEngine(Seed);
            var result = engine.Execute(plan);

            PrintResult(result, "CHARLOTTE FLAIR vs ASUKA — Technical Showcase");

            Assert.Equal(charlotte.RingName, result.Winner.RingName);
            Assert.Equal(asuka.RingName, result.Loser.RingName);
            // Technical showcase from two elite workers should still rate well even without a feud
            Assert.True(result.StarRating >= 2.0, $"Expected at least ★★ for a technical match, got {result.StarDisplay}");
        }

        // ────────────────────────────────────────────────────────────────────
        // MATCH 3 — Goldberg vs Brock Lesnar — WrestleMania XX (2004)
        //
        // One of the most infamously received WrestleMania matches ever.
        // Both men were leaving WWE that night; MSG knew it and booed them
        // from bell to bell ("You sold out!" at Goldberg, "Goodbye!" at Brock).
        // Neither man was emotionally invested. The match had zero storytelling,
        // no heat, no comebacks that landed, and a flat finish the crowd didn't
        // react to. Pure attrition wrestling with an actively hostile audience.
        //
        // Modelling choices:
        //  • Both men's AppealRatings are very low — the crowd disposition
        //    formula starts them with minimal crowd energy at the bell.
        //  • No feud object — whatever narrative existed, the audience rejected it.
        //  • Beat plan is deliberately monotonous: two rest holds, two flat heat
        //    segments, no near-falls, no storytelling beats, short finish.
        //  • Goldberg's Psychology (35) is historically accurate — he was not
        //    known for in-ring storytelling, only explosive spots.
        //  • An unearned finish: momentum was split/neutral at the end.
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The WM20 match as it was actually booked. Shared so the better-booking test can
        /// compare against it directly rather than against a hard-coded number.
        /// </summary>
        private static MatchPlan WM20OriginalPlan()
        {
            var goldberg = MakeGoldbergWM20();
            var brock    = MakeBrockWM20();

            // No feud — whatever they built, the audience rejected it completely
            return new MatchPlan
            {
                WrestlerA = goldberg,
                WrestlerB = brock,
                MatchType = MatchType.Standard,
                Beats     =
                [
                    // Lethargic opening — neither man engaged, crowd already hostile
                    new MatchBeat
                    {
                        Type      = BeatType.SlowOpening,
                        Control   = BeatControl.Even,
                        Intensity = BeatIntensity.Low,
                        Duration  = BeatDuration.Short
                    },
                    // Brock works a basic ground game; crowd boos instead of building tension
                    new MatchBeat
                    {
                        Type      = BeatType.HeatSegment,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.Low,
                        Duration  = BeatDuration.Medium
                    },
                    // Brock sits in a chinlock while the MSG crowd chants "boring"
                    new MatchBeat
                    {
                        Type      = BeatType.RestHold,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.Low,
                        Duration  = BeatDuration.Long
                    },
                    // Goldberg's attempted comeback — crowd barely reacts
                    new MatchBeat
                    {
                        Type      = BeatType.HeatSegment,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Low,
                        Duration  = BeatDuration.Short
                    },
                    // Goldberg grinding a hold of his own; crowd has fully given up
                    new MatchBeat
                    {
                        Type      = BeatType.RestHold,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Low,
                        Duration  = BeatDuration.Short
                    },
                    // Spear, Jackhammer — flat landing; crowd had already mentally left
                    new MatchBeat
                    {
                        Type      = BeatType.FinishClean,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Medium,
                        Duration  = BeatDuration.Brief
                    }
                ]
            };
        }

        [Fact]
        public void GoldbergVsBrock_WM20_CrowdChecksOut()
        {
            var plan = WM20OriginalPlan();

            var engine = new MatchEngine(Seed);
            var result = engine.Execute(plan);

            PrintResult(result, "GOLDBERG vs BROCK LESNAR — WrestleMania XX (Both leaving, crowd hostile)");

            Assert.Equal(plan.WrestlerA.RingName, result.Winner.RingName);
            Assert.Equal(plan.WrestlerB.RingName, result.Loser.RingName);
            Assert.True(result.StarRating <= 1.75,
                $"Expected a poor rating (≤★¾) for this historically bad match, got {result.StarDisplay}");
            Assert.True(result.CrowdAverageEnergy < 50,
                $"Crowd should have been dead throughout — average was {result.CrowdAverageEnergy:F1}");
        }

        // ────────────────────────────────────────────────────────────────────
        // MATCH 4 — Roman Reigns vs Brock Lesnar — WrestleMania 34 (2018)
        //
        // The culmination of a three-year push the audience refused to accept.
        // Brock hit eight German suplexes and multiple F5s; Roman kicked out
        // repeatedly. The crowd sat on their hands for Roman's resilience spots
        // and actively cheered Brock despite him being the heel champion.
        // Roman eventually won the Universal title, to near silence and boos.
        //
        // Modelling choices:
        //  • Roman's AppealRatings are low — his face persona had no credibility
        //    with the paying audience despite his real skill.
        //  • Brock has very high AppealRatings — the "cool heel" problem.
        //    His HeatSegments don't build proper tension because the crowd
        //    *enjoys* watching him dominate Roman (tensionFactor stays low).
        //  • Three back-to-back HeatSegments for Brock = the suplex-spam pattern.
        //    No PsychologicalWarfare, no FeudalEscalation, no narrative variety.
        //  • A Building feud (PriorMatch tag) acknowledges they've fought before,
        //    but the crowd never bought into the story.
        //  • Roman's finish is unearned: momentum favoured Brock throughout,
        //    so the engine correctly flags the win as controversial.
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void RomanVsBrock_WM34_CrowdRejectsThePush()
        {
            var roman = MakeRomanWM34();
            var brock = MakeBrockWM34();

            // They've fought before but the crowd never bought into the story
            var feud = new Feud
            {
                WrestlerA  = roman,
                WrestlerB  = brock,
                Intensity  = FeudIntensity.Cold,   // crowd never emotionally invested
                History    = [FeudHistoryTag.PriorMatch, FeudHistoryTag.ChampionshipRivalry],
                MatchCount = 3
            };

            var plan = new MatchPlan
            {
                WrestlerA = roman,
                WrestlerB = brock,
                Feud      = feud,
                MatchType = MatchType.Standard,
                Beats     =
                [
                    // Brief feeling-out; crowd muted from the start
                    new MatchBeat
                    {
                        Type      = BeatType.StandardOpening,
                        Control   = BeatControl.Even,
                        Intensity = BeatIntensity.Medium,
                        Duration  = BeatDuration.Short
                    },
                    // Germans 1–3: crowd cheers Brock suplexing Roman — wrong heat entirely
                    new MatchBeat
                    {
                        Type      = BeatType.HeatSegment,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Long
                    },
                    // Germans 4–6: pattern repeats; crowd entertained but not invested
                    new MatchBeat
                    {
                        Type      = BeatType.HeatSegment,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Medium
                    },
                    // Bear-hug: Brock grinding, taunting instead of covering
                    new MatchBeat
                    {
                        Type      = BeatType.RestHold,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.Low,
                        Duration  = BeatDuration.Short
                    },
                    // Germans 7–8: match has well worn out its welcome
                    new MatchBeat
                    {
                        Type      = BeatType.HeatSegment,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Short
                    },
                    // F5 — Roman survives; the one moment the crowd briefly reacts
                    new MatchBeat
                    {
                        Type      = BeatType.NearFall,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Brief
                    },
                    // Roman counters into a Spear — no sustained comeback, just an abrupt pin.
                    // Brock was still dominant; the crowd didn't get a hero-wins moment.
                    new MatchBeat
                    {
                        Type      = BeatType.FinishClean,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Low,  // flat pop — crowd had nothing to cheer
                        Duration  = BeatDuration.Brief
                    }
                ]
            };

            var engine = new MatchEngine(Seed);
            var result = engine.Execute(plan);

            PrintResult(result, "ROMAN REIGNS vs BROCK LESNAR — WrestleMania 34 (Crowd rejects the chosen-one push)");

            Assert.Equal(roman.RingName, result.Winner.RingName);
            Assert.Equal(brock.RingName, result.Loser.RingName);
            // Repetitive heat + cold crowd investment = distinctly below the good matches (≥★★★★)
            Assert.True(result.StarRating <= 3.75,
                $"Suplex spam with a rejected crowd push should cap below ★★★¾, got {result.StarDisplay}");
            Assert.True(result.StorytellingScore < 50,
                $"Three identical heat segments and no storytelling beats = low story score, got {result.StorytellingScore:F1}");
        }

        // ────────────────────────────────────────────────────────────────────
        // MATCH 5 — Goldberg vs Brock Lesnar — WM20, Better Booking
        //
        // SAME wrestlers, SAME no-feud context, SAME hostile crowd.
        // The crowd disposition cannot be improved — both men start at ~37.
        //
        // What changes: the STRUCTURE of the match.
        //
        // The original sin of WM20 was making it long and slow when neither
        // man had the in-ring psychology to sustain that format. Goldberg's
        // best matches in WCW were 90 seconds of raw violence. His one WCW
        // tool that actually works on a live crowd is the unstoppable monster
        // who overwhelms through sheer power.
        //
        // The better booking:
        //  • HotOpening — grab the crowd before they settle into booing.
        //    Even a hostile MSG audience reacts to an immediate brawl.
        //  • ONE brief high-intensity heat segment for Brock, then stop.
        //    The crowd won't sustain interest through long grappling.
        //  • HighSpot — Brock's Shooting Star Press was the only genuine
        //    "wow" moment in the real match. Book it on purpose, successfully,
        //    before the finish sequence rather than as an accidental botch.
        //  • Goldberg's power comeback is the match's centrepiece. A large
        //    momentum deficit from Brock's heat earns a real earnedBonus.
        //  • Finish is Extreme intensity and momentum-earned — no ambiguity.
        //  • Total length: 5 beats. Keep it short. There's no fixing the
        //    crowd disposition, but you can avoid giving them time to boo.
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void GoldbergVsBrock_WM20_BetterBooking()
        {
            var goldberg = MakeGoldbergWM20();
            var brock    = MakeBrockWM20();

            // No feud — identical to the real match
            var plan = new MatchPlan
            {
                WrestlerA = goldberg,
                WrestlerB = brock,
                MatchType = MatchType.Standard,
                Beats     =
                [
                    // Grab the crowd immediately — don't let them settle into hostility
                    new MatchBeat
                    {
                        Type      = BeatType.HotOpening,
                        Control   = BeatControl.Even,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Short
                    },
                    // Brock gets a brief, high-intensity advantage — not a long grind
                    new MatchBeat
                    {
                        Type      = BeatType.HeatSegment,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Short
                    },
                    // Brock goes for the Shooting Star Press — book it successfully this time
                    new MatchBeat
                    {
                        Type      = BeatType.HighSpot,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Brief
                    },
                    // Goldberg's monster comeback — earned by the accumulated deficit
                    new MatchBeat
                    {
                        Type      = BeatType.Comeback,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Short
                    },
                    // Spear. Jackhammer. Cover. Done — before the crowd can re-settle
                    new MatchBeat
                    {
                        Type      = BeatType.FinishClean,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Brief
                    }
                ]
            };

            var engine = new MatchEngine(Seed);
            var result = engine.Execute(plan);

            PrintResult(result, "GOLDBERG vs BROCK — WM20 BETTER BOOKING (Sprint, no rest holds, earned finish)");

            Assert.Equal(goldberg.RingName, result.Winner.RingName);
            Assert.Equal(brock.RingName, result.Loser.RingName);
            // Same hostile crowd, better structure — should significantly beat the original.
            // The bar here was 2.5 when crowd energy had no per-pairing ceiling and every
            // match could climb to 100. Now that two rejected performers cap out around 64
            // crowd energy, a well-worked sprint between them lands at ★★¼–★★½ rather than
            // ★★½+, which is the more honest read of the hypothetical.
            Assert.True(result.StarRating >= 2.25,
                $"A tight explosive sprint should rescue this from the original, got {result.StarDisplay}");

            // The crowd assertion is deliberately relative rather than an absolute threshold.
            // Two performers this thoroughly rejected have a hard ceiling on how loud they can
            // get a building — booking cannot conjure a reaction the audience does not have.
            // What better booking *can* do is get much closer to that ceiling, so that is what
            // we measure: the same two men, same crowd, materially bigger peak.
            var original = new MatchEngine(Seed).Execute(WM20OriginalPlan());

            Assert.True(result.CrowdPeakEnergy > original.CrowdPeakEnergy * 1.5,
                $"Better booking should pop this crowd far harder than the original: " +
                $"got {result.CrowdPeakEnergy:F1} vs original {original.CrowdPeakEnergy:F1}");
            Assert.True(result.StarRating > original.StarRating + 1.0,
                $"Better booking should be worth more than a full star here: " +
                $"got {result.StarRating:F2} vs original {original.StarRating:F2}");
        }

        // ────────────────────────────────────────────────────────────────────
        // MATCH 6 — Roman Reigns vs Brock Lesnar — WM34, Better Booking
        //
        // SAME wrestlers, SAME cold feud (PriorMatch + ChampionshipRivalry,
        // MatchCount=3, FeudIntensity.Cold), SAME crowd disposition problem.
        // Roman's appeal ratings are still low. Brock's are still high.
        // FeudalEscalation remains unavailable (requires Building feud minimum).
        //
        // The diagnostic: why did the original fail?
        //  1. Three back-to-back heat segments — the crowd got bored of suplexes.
        //  2. Brock's high crowd disposition means his heat doesn't build proper
        //     "save Roman" tension — tensionFactor stays at 0.51.
        //  3. Roman was purely reactive; he never looked genuinely dangerous.
        //  4. The finish was unearned — momentum was entirely Brock's.
        //
        // The fixes, within the same constraints:
        //  • HotOpening — establish energy before the crowd files into their seats.
        //  • ONE heat segment for Brock (not three). Establishes dominance without
        //    exhausting the crowd's patience for the pattern.
        //  • PsychologicalWarfare (Brock) — Brock's 4.2 charisma scores well even
        //    in a cold crowd. Adds story variety. Gives Roman something to respond to.
        //  • Roman's first Comeback + NearFall — Roman nearly wins BEFORE Brock
        //    hits his F5. Now he's dangerous, not just durable.
        //  • Brock's F5 NearFall — the crowd pops for Roman surviving, rather than
        //    for Brock being cool.
        //  • Roman's second Comeback from a deep momentum deficit earns a massive
        //    earnedBonus — this is where the crowd energy peaks legitimately.
        //  • Earned finish — momentum has swung to Roman before the pin.
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void RomanVsBrock_WM34_BetterBooking()
        {
            var roman = MakeRomanWM34();
            var brock = MakeBrockWM34();

            // Exact same feud as the bad version — no improvements possible here
            var feud = new Feud
            {
                WrestlerA  = roman,
                WrestlerB  = brock,
                Intensity  = FeudIntensity.Cold,
                History    = [FeudHistoryTag.PriorMatch, FeudHistoryTag.ChampionshipRivalry],
                MatchCount = 3
            };

            var plan = new MatchPlan
            {
                WrestlerA = roman,
                WrestlerB = brock,
                Feud      = feud,
                MatchType = MatchType.Standard,
                Beats     =
                [
                    // Grab the crowd before they settle — don't open with a flat lock-up
                    new MatchBeat
                    {
                        Type      = BeatType.HotOpening,
                        Control   = BeatControl.Even,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Short
                    },
                    // ONE extended heat from Brock (Germans 1–5): establishes dominance
                    // without repeating the same note three times
                    new MatchBeat
                    {
                        Type      = BeatType.HeatSegment,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Long
                    },
                    // Brock taunts — uses his 4.2 charisma to build story even in a cold crowd
                    new MatchBeat
                    {
                        Type      = BeatType.PsychologicalWarfare,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Brief
                    },
                    // Roman fires back — the taunt gave him something to respond to
                    new MatchBeat
                    {
                        Type      = BeatType.Comeback,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Short
                    },
                    // Roman nearly wins — NOW the crowd knows he's actually dangerous
                    new MatchBeat
                    {
                        Type      = BeatType.NearFall,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Brief
                    },
                    // Brock answers with an F5 — regains control dramatically
                    new MatchBeat
                    {
                        Type      = BeatType.HeatSegment,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Short
                    },
                    // Roman kicks out of the F5 — crowd reacts to a man they've seen nearly
                    // win, not just absorb punishment
                    new MatchBeat
                    {
                        Type      = BeatType.NearFall,
                        Control   = BeatControl.WrestlerB,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Brief
                    },
                    // Roman's final push from a deep deficit — maximum earnedBonus
                    new MatchBeat
                    {
                        Type      = BeatType.Comeback,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.Extreme,
                        Duration  = BeatDuration.Short
                    },
                    // Roman wins with momentum behind him — not a controversial abrupt pin
                    new MatchBeat
                    {
                        Type      = BeatType.FinishClean,
                        Control   = BeatControl.WrestlerA,
                        Intensity = BeatIntensity.High,
                        Duration  = BeatDuration.Brief
                    }
                ]
            };

            var engine = new MatchEngine(Seed);
            var result = engine.Execute(plan);

            PrintResult(result, "ROMAN REIGNS vs BROCK LESNAR — WM34 BETTER BOOKING (Variety, arc, earned finish)");

            Assert.Equal(roman.RingName, result.Winner.RingName);
            Assert.Equal(brock.RingName, result.Loser.RingName);
            // Better structure should clearly outperform the 3.17 original
            Assert.True(result.StarRating >= 3.75,
                $"Structural variety + earned finish should beat the 3.17 original, got {result.StarDisplay}");
            // Storytelling score should be dramatically higher than the original's 40.7
            Assert.True(result.StorytellingScore >= 55,
                $"Two comebacks + near-fall exchange + PsychWarfare should lift story well above original's 40.7, got {result.StorytellingScore:F1}");
        }
    }
}
