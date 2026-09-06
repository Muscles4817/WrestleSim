using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;
using SegmentModel = WrestlingSim.Models.Segment.Segment;
using SegmentActionModel = WrestlingSim.Models.Segment.SegmentAction;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Saving is where a shared object graph goes wrong quietly: serialise wrestlers by
    /// value and every feud gets its own copy, so reference equality — which FeudBook,
    /// MatchPlan and the engine all rely on — silently stops holding after a load.
    /// These tests exist to catch that.
    /// </summary>
    public class SaveSerializerTests
    {
        private static List<Wrestler> Roster() =>
        [
            TestRoster.Make("Alpha One", overness: 80),
            TestRoster.Make("Beta Two", overness: 60),
            TestRoster.Make("Gamma Three", overness: 40)
        ];

        private static Career NewCareer(List<Wrestler> roster)
        {
            var start = new DateOnly(2025, 1, 6);
            return new Career
            {
                Promotion   = new Promotion { Name = "Round Trip Wrestling", Tier = PromotionTier.National },
                StartDate   = start,
                CurrentDate = start,
                Roster      = roster
            };
        }

        private static Career RoundTrip(Career career)
        {
            string json = SaveSerializer.ToJson(career);
            // A load always binds against a fresh roster, exactly as the app does.
            return SaveSerializer.FromJson(json, Roster());
        }

        /// <summary>Round-trip against a roster the caller controls, for tag cards.</summary>
        private static Career RoundTrip(Career career, List<Wrestler> roster)
        {
            string json = SaveSerializer.ToJson(career);
            // Rebind against equivalent-but-fresh instances, as a real reload does.
            var fresh = Roster();
            foreach (var extra in roster.Where(w => fresh.All(f => f.Id != w.Id)))
                fresh.Add(TestRoster.Make(extra.RingName, overness: extra.Overness));
            return SaveSerializer.FromJson(json, fresh);
        }

        [Fact]
        public void RoundTripKeepsPromotionAndClock()
        {
            var career = NewCareer(Roster());
            career.CurrentDate = career.CurrentDate.AddDays(30);

            var loaded = RoundTrip(career);

            Assert.Equal("Round Trip Wrestling", loaded.Promotion.Name);
            Assert.Equal(PromotionTier.National, loaded.Promotion.Tier);
            Assert.Equal(new DateOnly(2025, 2, 5), loaded.CurrentDate);
            Assert.Equal(new DateOnly(2025, 1, 6), loaded.StartDate);
        }

        [Fact]
        public void RoundTripKeepsPopularityChanges()
        {
            var roster = Roster();
            var career = NewCareer(roster);

            roster[0].Overness = 93;

            var loaded = RoundTrip(career);

            Assert.Equal(93, loaded.FindWrestler("alpha-one")!.Overness);
        }

        [Fact]
        public void RoundTripKeepsFeudHeatTagsAndMatchCount()
        {
            var roster = Roster();
            var career = NewCareer(roster);

            career.FeudBook.Record(roster[0], roster[1], heat: 34,
                tags: [FeudHistoryTag.Betrayal, FeudHistoryTag.TitleStolen]);
            career.FeudBook.Find(roster[0], roster[1])!.MatchCount = 3;

            var loaded = RoundTrip(career);
            var feud = loaded.FeudBook.Find(
                loaded.FindWrestler("alpha-one")!, loaded.FindWrestler("beta-two")!);

            Assert.NotNull(feud);
            Assert.Equal(34, feud!.Heat, 3);
            Assert.Equal(FeudIntensity.Hot, feud.Intensity);
            Assert.Equal(3, feud.MatchCount);
            Assert.Contains(FeudHistoryTag.Betrayal, feud.History);
            Assert.Contains(FeudHistoryTag.TitleStolen, feud.History);
        }

        [Fact]
        public void LoadedWrestlersAreTheSameInstancesTheRosterHolds()
        {
            var roster = Roster();
            var career = NewCareer(roster);
            career.FeudBook.Record(roster[0], roster[1], heat: 20);

            var loaded = RoundTrip(career);
            var feud = loaded.FeudBook.AllIncludingDormant.Single();

            // This is the whole point: a feud's wrestlers must BE the roster's wrestlers,
            // not equal-looking copies, or overness changes land on the wrong object.
            Assert.Same(loaded.FindWrestler(feud.WrestlerA.Id), feud.WrestlerA);
            Assert.Same(loaded.FindWrestler(feud.WrestlerB.Id), feud.WrestlerB);
        }

        [Fact]
        public void RoundTripKeepsABookedCard()
        {
            var roster = Roster();
            var career = NewCareer(roster);
            var show = career.Schedule("Weekly", career.CurrentDate.AddDays(7), ShowType.Television, "Test Arena");

            show.Card.Add(new BookedMatch
            {
                StructureName = "TV Formula",
                Plan = new MatchPlanModel
                {
                    WrestlerA = roster[0],
                    WrestlerB = roster[1],
                    MatchType = Enums.MatchType.Technical,
                    Beats =
                    [
                        new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                        new MatchBeat { Type = BeatType.HeatSegment, Control = BeatControl.WrestlerB,
                                        Intensity = BeatIntensity.High, Duration = BeatDuration.Long },
                        new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }
                    ]
                }
            });

            var segment = new SegmentModel("Betrayal", SegmentType.Brawl, SegmentLocation.Ring, isScripted: true);
            segment.AddParticipant(roster[0]);
            segment.AddParticipant(roster[2]);
            segment.AddAction(new SegmentActionModel
            {
                ActionType = SegmentActionType.Betrayal,
                Performer  = roster[2],
                Target     = roster[0],
                HeatImpact = 4,
                Label      = "Turn on a Partner"
            });
            segment.HistoryTags.Add(FeudHistoryTag.Betrayal);
            show.Card.Add(segment);

            var loaded = RoundTrip(career);
            var loadedShow = loaded.Shows.Single();

            Assert.Equal("Weekly", loadedShow.Name);
            Assert.Equal("Test Arena", loadedShow.Venue);
            Assert.Equal(ShowType.Television, loadedShow.Type);
            Assert.Equal(2, loadedShow.Card.Count);

            var match = Assert.IsType<BookedMatch>(loadedShow.Card[0]);
            Assert.Equal("TV Formula", match.StructureName);
            Assert.Equal(Enums.MatchType.Technical, match.Plan.MatchType);
            Assert.Equal(3, match.Plan.Beats.Count);
            Assert.Equal(BeatDuration.Long, match.Plan.Beats[1].Duration);
            Assert.Equal("alpha-one", match.Plan.WrestlerA.Id);

            var loadedSegment = Assert.IsType<SegmentModel>(loadedShow.Card[1]);
            Assert.Equal(2, loadedSegment.Participants.Count);
            Assert.Single(loadedSegment.Actions);
            Assert.Equal("gamma-three", loadedSegment.Actions[0].Performer.Id);
            Assert.Equal("alpha-one", loadedSegment.Actions[0].Target!.Id);
        }

        [Fact]
        public void ATagMatchRoundTrips_WithBothPartnersAndTheBeatsIntact()
        {
            var roster = Roster();
            var extra  = TestRoster.Make("Delta Four", overness: 50);
            roster.Add(extra);
            var career = NewCareer(roster);
            var show = career.Schedule("Weekly", career.CurrentDate, ShowType.Television);

            show.Card.Add(new BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    SideA = MatchSide.Of(roster[0], roster[1]),
                    SideB = MatchSide.Of(roster[2], extra),
                    Beats =
                    [
                        new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                        new MatchBeat { Type = BeatType.Cutoff,    Control = BeatControl.WrestlerB },
                        new MatchBeat { Type = BeatType.Isolation,  Control = BeatControl.WrestlerB },
                        new MatchBeat { Type = BeatType.HotTag,     Control = BeatControl.WrestlerA, IncomingIndex = 1 },
                        new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }
                    ]
                },
                StructureName = "Formula Tag"
            });

            var loaded = RoundTrip(career, roster);
            var match = Assert.IsType<BookedMatch>(loaded.Shows.Single().Card.Single());

            Assert.Equal(2, match.Plan.SideA.Size);
            Assert.Equal(2, match.Plan.SideB.Size);
            Assert.Equal(new[] { "alpha-one", "beta-two" }, match.Plan.SideA.Members.Select(w => w.Id));
            Assert.Equal(new[] { "gamma-three", "delta-four" }, match.Plan.SideB.Members.Select(w => w.Id));
            Assert.Equal(1, match.Plan.Beats.Single(b => b.Type == BeatType.HotTag).IncomingIndex);
            Assert.Empty(match.Plan.Validate());
        }

        [Fact]
        public void ATagMatchSurvivesReload_AndStillRuns()
        {
            var roster = Roster();
            var extra  = TestRoster.Make("Delta Four", overness: 50);
            roster.Add(extra);
            var career = NewCareer(roster);
            var show = career.Schedule("Weekly", career.CurrentDate, ShowType.Television);

            var structure = MatchStructureLibrary.Find("Formula Tag")!;
            show.Card.Add(new BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    SideA = MatchSide.Of(roster[0], roster[1]),
                    SideB = MatchSide.Of(roster[2], extra),
                    Beats = structure.Beats.Select(b => b.Clone()).ToList()
                },
                StructureName = structure.Name
            });

            var loaded = RoundTrip(career, roster);
            var result = new Engine.ShowSimulator(loaded.FeudBook)
                .Simulate(loaded.Shows.Single().ToShow());

            Assert.True(result.OverallRating > 0);
            Assert.Single(result.Items);
        }

        [Fact]
        public void StartingIndexSurvivesTheRoundTrip()
        {
            var roster = Roster();
            var extra  = TestRoster.Make("Delta Four", overness: 50);
            roster.Add(extra);
            var career = NewCareer(roster);
            var show = career.Schedule("Weekly", career.CurrentDate, ShowType.Television);

            show.Card.Add(new BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    SideA = new MatchSide { Members = { roster[0], roster[1] }, StartingIndex = 1 },
                    SideB = MatchSide.Of(roster[2], extra),
                    Beats =
                    [
                        new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                        new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }
                    ]
                }
            });

            var match = Assert.IsType<BookedMatch>(
                RoundTrip(career, roster).Shows.Single().Card.Single());

            Assert.Equal(1, match.Plan.SideA.StartingIndex);
            Assert.Equal("beta-two", match.Plan.SideA.Starter.Id);
        }

        [Fact]
        public void AV2Save_LoadsAsTwoSidesOfOne()
        {
            // The migration that matters: every save anybody already has is v2, and none
            // of them may break. A v2 card item names one wrestler per side.
            const string v2Json = """
            {
              "Version": 2,
              "CareerId": "abc",
              "PromotionName": "Legacy Wrestling",
              "Tier": "National",
              "CurrentDate": "2025-01-06",
              "StartDate": "2025-01-06",
              "Wrestlers": [],
              "ShowDefinitions": [],
              "Feuds": [],
              "Titles": [],
              "Shows": [
                {
                  "Id": "show1",
                  "Name": "Weekly",
                  "Date": "2025-01-06",
                  "Type": "Television",
                  "Venue": "",
                  "RuntimeMinutes": 120,
                  "Attendance": 5000,
                  "Card": [
                    {
                      "Kind": "Match",
                      "WrestlerA": "alpha-one",
                      "WrestlerB": "beta-two",
                      "MatchType": "Standard",
                      "StructureName": "TV Formula",
                      "Beats": [
                        { "Type": "StandardOpening", "Control": "Even", "Intensity": "Medium", "Duration": "Medium" },
                        { "Type": "FinishClean", "Control": "WrestlerA", "Intensity": "Medium", "Duration": "Medium" }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

            var career = SaveSerializer.FromJson(v2Json, Roster());
            var match = Assert.IsType<BookedMatch>(career.Shows.Single().Card.Single());

            Assert.Equal(1, match.Plan.SideA.Size);
            Assert.Equal(1, match.Plan.SideB.Size);
            Assert.Equal("alpha-one", match.Plan.WrestlerA.Id);
            Assert.Equal("beta-two", match.Plan.WrestlerB.Id);
            Assert.False(match.Plan.IsTagMatch);
            Assert.Empty(match.Plan.Validate());
        }

        [Fact]
        public void ACardNamingSomebodyTheRosterLost_IsDroppedWholeRatherThanRebuiltAManShort()
        {
            var roster = Roster();
            var departed = TestRoster.Make("Departed", overness: 50);
            var career = NewCareer(roster);
            var show = career.Schedule("Weekly", career.CurrentDate, ShowType.Television);

            show.Card.Add(new BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    SideA = MatchSide.Of(roster[0], departed),
                    SideB = MatchSide.Of(roster[1], roster[2]),
                    Beats =
                    [
                        new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                        new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }
                    ]
                }
            });

            // Reloaded against a roster that never had "Departed" in it.
            var loaded = RoundTrip(career, Roster());

            Assert.Empty(loaded.Shows.Single().Card);
        }

        [Fact]
        public void ARealisticV2Save_LoadsEveryPathPhase5Touched()
        {
            // The thin v2 fixture above has empty Wrestlers, Feuds and Titles arrays, so it
            // exercised none of the v2 read paths phase 5 added — FeudDto.WrestlerA/B,
            // TitleReignDto.Champion, and an absent Title.SideSize. Review flagged that
            // nothing in the suite would have caught a regression in any of them.
            const string v2Json = """
            {
              "Version": 2,
              "CareerId": "legacy",
              "PromotionName": "Legacy Wrestling",
              "Tier": "National",
              "CurrentDate": "2025-03-01",
              "StartDate": "2025-01-06",
              "Wrestlers": [
                { "Id": "alpha-one", "Overness": 81.5, "Momentum": 4.25, "LastAppearance": "2025-02-22" },
                { "Id": "beta-two",  "Overness": 58.0, "Momentum": -2.0, "LastAppearance": "2025-02-22" }
              ],
              "ShowDefinitions": [],
              "Feuds": [
                {
                  "WrestlerA": "alpha-one",
                  "WrestlerB": "beta-two",
                  "Heat": 34.0,
                  "MatchCount": 3,
                  "RememberedMeetings": 3,
                  "LastMatchDate": "2025-02-22",
                  "Intensity": "Hot",
                  "History": ["PriorMatch"]
                }
              ],
              "Titles": [
                {
                  "Id": "world",
                  "Name": "Legacy World Championship",
                  "Tier": "World",
                  "Division": "Mens",
                  "Established": "2025-01-06",
                  "Standing": 64.5,
                  "Retired": false,
                  "Lineage": [
                    {
                      "Champion": "beta-two", "ReignNumber": 1,
                      "Won": "2025-01-06", "Lost": "2025-02-01",
                      "WonAt": "Debut", "LostAt": "February Show", "Defences": 2
                    },
                    {
                      "Champion": "alpha-one", "ReignNumber": 2,
                      "Won": "2025-02-01", "LastDefended": "2025-02-22",
                      "WonAt": "February Show", "Defences": 1
                    }
                  ]
                }
              ],
              "Shows": []
            }
            """;

            var career = SaveSerializer.FromJson(v2Json, Roster());

            // Feud: read from the v2 single-wrestler fields into a side of one.
            var feud = Assert.Single(career.FeudBook.AllIncludingDormant);
            Assert.Equal("alpha-one", feud.WrestlerA.Id);
            Assert.Equal("beta-two", feud.WrestlerB.Id);
            Assert.False(feud.IsTeamFeud);
            Assert.Equal(34.0, feud.Heat, 3);
            Assert.Equal(3, feud.MatchCount);

            // Title: SideSize absent in v2 must default to a singles belt, and both reigns
            // must come back through the Champions list.
            var title = Assert.Single(career.Titles.Active);
            Assert.Equal(1, title.SideSize);
            Assert.False(title.IsTagTitle);
            Assert.Equal(2, title.Lineage.Count);
            Assert.Equal("beta-two", title.Lineage[0].Champion.Id);
            Assert.Equal("alpha-one", title.Lineage[1].Champion.Id);
            Assert.Equal("Alpha One", title.CurrentReign!.ChampionName);
            Assert.True(title.IsHeldBy(career.Roster.Single(w => w.Id == "alpha-one")));

            // And the reign binds to the live roster instance, not a copy.
            Assert.Same(career.Roster.Single(w => w.Id == "alpha-one"), title.Champion);

            // No teams in a v2 save, and that is correct rather than lossy.
            Assert.Empty(career.Teams);
        }

        [Fact]
        public void AV3SaveDoesNotWriteTheV2FieldsItDocumentsAsUnwritten()
        {
            // They were being written as empty strings, because a non-nullable string
            // property is never omitted by WhenWritingNull — contradicting both the XML docs
            // and the stated reason for not writing half-truths.
            var roster = Roster();
            var career = NewCareer(roster);
            career.FeudBook.Record(roster[0], roster[1], 12);

            var title = career.Titles.Create("Test Belt", TitleTier.Tertiary, Division.Mens, career.CurrentDate);
            title.Lineage.Add(new TitleReign { Champions = [roster[0]], ReignNumber = 1, Won = career.CurrentDate });

            string json = SaveSerializer.ToJson(career);

            Assert.DoesNotContain("\"WrestlerA\"", json);
            Assert.DoesNotContain("\"WrestlerB\"", json);
            Assert.DoesNotContain("\"Champion\":", json);
            Assert.Contains("\"SideA\"", json);
            Assert.Contains("\"Champions\"", json);
        }

        [Fact]
        public void ALoadedCardIsStillRunnable()
        {
            var roster = Roster();
            var career = NewCareer(roster);
            var show = career.Schedule("Weekly", career.CurrentDate, ShowType.Television);

            show.Card.Add(new BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    WrestlerA = roster[0],
                    WrestlerB = roster[1],
                    Beats =
                    [
                        new MatchBeat { Type = BeatType.HotOpening, Control = BeatControl.Even },
                        new MatchBeat { Type = BeatType.HeatSegment, Control = BeatControl.WrestlerB },
                        new MatchBeat { Type = BeatType.Comeback, Control = BeatControl.WrestlerA },
                        new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }
                    ]
                }
            });

            var loaded = RoundTrip(career);
            var result = new Engine.ShowSimulator(loaded.FeudBook)
                .Simulate(loaded.Shows.Single().ToShow());

            Assert.True(result.OverallRating > 0);
            Assert.Single(result.Items);
        }

        [Fact]
        public void RoundTripKeepsAShowResult()
        {
            var roster = Roster();
            var career = NewCareer(roster);
            var show = career.Schedule("Weekly", career.CurrentDate, ShowType.Television);

            show.Result = new ShowResult
            {
                OverallRating  = 72.5,
                FinalCrowdMood = 6.4,
                BookedMinutes  = 100,
                BudgetMinutes  = 120,
                Items =
                [
                    new CardItemResult { Label = "1. A vs B", Kind = CardItemKind.Match,
                                         Score = 68, DurationMinutes = 18, StarRating = 3.4,
                                         Notes = { "A def. B" } }
                ]
            };

            var loaded = RoundTrip(career);
            var loadedShow = loaded.Shows.Single();

            Assert.True(loadedShow.HasRun);
            Assert.Equal(72.5, loadedShow.Result!.OverallRating, 3);

            // The full engine result is not persisted, so the star rating has to survive
            // on its own or a reloaded card renders with no stars at all.
            Assert.Equal(3.4, loadedShow.Result.Items[0].StarRating!.Value, 3);
            Assert.Equal("1. A vs B", loadedShow.Result.Items[0].Label);
            Assert.Contains("A def. B", loadedShow.Result.Items[0].Notes);
        }

        [Fact]
        public void AWrestlerMissingFromTheRosterDropsAlongWithWhatReferencedThem()
        {
            var roster = Roster();
            var career = NewCareer(roster);

            career.FeudBook.Record(roster[0], roster[2], heat: 25);
            var show = career.Schedule("Weekly", career.CurrentDate, ShowType.Television);
            show.Card.Add(new BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    WrestlerA = roster[0],
                    WrestlerB = roster[2],
                    Beats = [new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }]
                }
            });

            string json = SaveSerializer.ToJson(career);

            // Simulate the roster losing someone between sessions.
            var trimmed = Roster().Where(w => w.Id != "gamma-three").ToList();
            var loaded = SaveSerializer.FromJson(json, trimmed);

            Assert.Null(loaded.FindWrestler("gamma-three"));
            Assert.Empty(loaded.FeudBook.AllIncludingDormant);
            Assert.Empty(loaded.Shows.Single().Card);
        }

        // -- Brands -----------------------------------------------------------

        [Fact]
        public void RoundTripKeepsTheSplitAndItsRosters()
        {
            var roster = Roster();
            var career = NewCareer(roster);

            var red  = new Brand { Name = "Red",  Identity = "Sports-focused", Colour = "#ff0000" };
            var blue = new Brand { Name = "Blue", Identity = "Glossier" };
            career.BeginSplit([red, blue]);

            career.Brands.Assign(roster[0], red);
            career.Brands.Assign(roster[1], blue);
            red.RecordShow(72.5);

            var loaded = RoundTrip(career);
            var loadedRed = loaded.Brands.Brands.Single(b => b.Name == "Red");

            Assert.True(loaded.Brands.Active);
            Assert.Equal(2, loaded.Brands.Brands.Count);
            Assert.Equal("Sports-focused", loadedRed.Identity);
            Assert.Equal("#ff0000", loadedRed.Colour);
            Assert.Equal(new DateOnly(2025, 1, 6), loaded.Brands.StartedOn);
            Assert.Equal(72.5, loadedRed.Form);

            Assert.Equal("Red", loaded.Brands.BrandOf("alpha-one")!.Name);
            Assert.Equal("Blue", loaded.Brands.BrandOf("beta-two")!.Name);
            Assert.Null(loaded.Brands.BrandOf("gamma-three"));
        }

        [Fact]
        public void LoadedBrandRostersAreTheSameInstancesTheRosterHolds()
        {
            var roster = Roster();
            var career = NewCareer(roster);

            var red = new Brand { Name = "Red" };
            career.BeginSplit([red, new Brand { Name = "Blue" }]);
            career.Brands.Assign(roster[0], red);

            var loaded = RoundTrip(career);
            var loadedRed = loaded.Brands.Brands.Single(b => b.Name == "Red");

            // Brand membership is stored by id precisely so this holds: resolving it must
            // produce the roster's own objects, not copies.
            var member = loaded.RosterOf(loadedRed).Single();
            Assert.Same(loaded.FindWrestler("alpha-one"), member);
        }

        [Fact]
        public void RoundTripKeepsIntegrityTheCeilingAndTheLedger()
        {
            var roster = Roster();
            var career = NewCareer(roster);

            var red  = new Brand { Name = "Red" };
            var blue = new Brand { Name = "Blue" };
            career.BeginSplit([red, blue]);
            career.Brands.Assign(roster[0], red);

            career.Brands.ApplyCrossover(new CrossoverRecord
            {
                WrestlerId    = roster[0].Id,
                WrestlerName  = "Alpha One",
                HomeBrandName = "Red",
                ShowBrandName = "Blue",
                ShowName      = "Blue Night",
                Date          = new DateOnly(2025, 2, 3),
                Cost          = 3.2
            }, BrandIntegrity.PermanentShare);

            var loaded = RoundTrip(career);

            Assert.Equal(career.Brands.Integrity, loaded.Brands.Integrity, 3);
            Assert.Equal(career.Brands.Ceiling, loaded.Brands.Ceiling, 3);
            Assert.Equal(1, loaded.Brands.CrossoverCount);
            Assert.Equal("Alpha One", loaded.Brands.Crossovers.Single().WrestlerName);
            Assert.Equal(new DateOnly(2025, 2, 3), loaded.Brands.Crossovers.Single().Date);
        }

        [Fact]
        public void RoundTripKeepsWhichBrandRunsWhichShow()
        {
            var roster = Roster();
            var career = NewCareer(roster);

            var blue = new Brand { Name = "Blue" };
            career.BeginSplit([new Brand { Name = "Red" }, blue]);

            career.ShowDefinitions.Add(new ShowDefinition
            {
                Name       = "Blue Night",
                Recurrence = RecurrenceKind.Weekly,
                Day        = DayOfWeek.Tuesday,
                BrandId    = blue.Id
            });
            career.MaterialiseSchedule();

            var loaded = RoundTrip(career);
            var loadedBlue = loaded.Brands.Brands.Single(b => b.Name == "Blue");

            Assert.Equal(loadedBlue.Id, loaded.ShowDefinitions.Single().BrandId);
            Assert.NotEmpty(loaded.Shows);
            Assert.All(loaded.Shows, s => Assert.Equal(loadedBlue, loaded.BrandOfShow(s)));
        }

        [Fact]
        public void ABrandDropsAnyoneWhoIsNoLongerOnTheRoster()
        {
            var roster = Roster();
            var career = NewCareer(roster);

            var red = new Brand { Name = "Red" };
            career.BeginSplit([red, new Brand { Name = "Blue" }]);
            foreach (var w in roster) career.Brands.Assign(w, red);

            string json = SaveSerializer.ToJson(career);
            var trimmed = Roster().Where(w => w.Id != "gamma-three").ToList();
            var loaded = SaveSerializer.FromJson(json, trimmed);

            var loadedRed = loaded.Brands.Brands.Single(b => b.Name == "Red");
            Assert.Equal(2, loadedRed.RosterIds.Count);
            Assert.DoesNotContain("gamma-three", loadedRed.RosterIds);
        }

        [Fact]
        public void APromotionThatNeverSplitWritesNoBrandSection()
        {
            string json = SaveSerializer.ToJson(NewCareer(Roster()));
            Assert.DoesNotContain("\"Brands\"", json);
        }

        [Fact]
        public void ASaveFromBeforeTheSplitExistedStillOpens()
        {
            // v1 saves carry no brand section at all; they must load as one roster.
            string json = SaveSerializer.ToJson(NewCareer(Roster()))
                .Replace($"\"Version\":{SaveGame.CurrentVersion}", "\"Version\":1");

            var loaded = SaveSerializer.FromJson(json, Roster());

            Assert.False(loaded.Brands.Active);
            Assert.Empty(loaded.Brands.Brands);
            Assert.Equal(100, loaded.Brands.Integrity);
        }

        [Fact]
        public void ASaveFromANewerVersionIsRejectedWithAReadableMessage()
        {
            // Pinned to the current version rather than a literal, so bumping the save
            // format does not turn this into a test that quietly stops testing anything.
            string json = SaveSerializer.ToJson(NewCareer(Roster()))
                .Replace($"\"Version\":{SaveGame.CurrentVersion}", "\"Version\":99");

            var ex = Assert.Throws<SaveLoadException>(() => SaveSerializer.FromJson(json, Roster()));
            Assert.Contains("newer version", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GarbageIsRejectedWithAReadableMessage()
        {
            Assert.Throws<SaveLoadException>(() => SaveSerializer.FromJson("not json at all", Roster()));
        }
    }
}
