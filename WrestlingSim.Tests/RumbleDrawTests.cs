using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Rumble;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **The drawing, as a thing that happens on a show.**
    ///
    /// The entry-number work shipped `RumblePlan.DrawNumbers` behind a 🎲 in the match
    /// builder. It was honest and it was invisible: the numbers moved and no crowd ever saw
    /// it happen. Doc 18 §2.5 says it is the entries that make the Rumble a story rather
    /// than a scramble, and a story the audience is not told is not a story — so the drawing
    /// became a card item, with a slot, a runtime, a crowd and a price.
    ///
    /// The three claims under test, in the order they matter:
    ///
    ///   • **A number means the opposite thing to a face and a heel**, at the drawing as
    ///     much as in the match — and the two are read from opposite ends, which is why
    ///     they are separate functions rather than one shared with a sign flipped.
    ///   • **Announcing buys anticipation and costs the surprise.** Both halves have to be
    ///     real or the drawing is free, and a free thing is a thing everybody books.
    ///   • **Rigging is the only way to guarantee a number, and it is not free either.**
    ///     The credibility discount is what stops "hand the winner number two" being the
    ///     answer to every Rumble, which is the exact hole the entry-number work closed and
    ///     which a drawing could very easily have reopened.
    /// </summary>
    public class RumbleDrawTests(ITestOutputHelper output)
    {
        private static readonly int Seed = StableSeed.From("rumble-draw");

        private static Wrestler W(string n, int over = 70, Alignment align = Alignment.Face)
        {
            var w = TestRoster.Make(n, overness: over, charisma: 3.0, skill: 3.0);
            w.Gimmick.NaturalAlignment = align;
            return w;
        }

        private static RumblePlan Rumble(IReadOnlyList<Wrestler> field, Wrestler winner,
                                         int interval = 90) => new()
        {
            Field = field.Select((w, i) => new RumbleEntrant { Wrestler = w, Number = i + 1 }).ToList(),
            EntryIntervalSeconds = interval,
            Winner = winner
        };

        private static Career NewCareer(List<Wrestler> roster)
        {
            var start = new DateOnly(2026, 1, 6);
            return new Career
            {
                Promotion   = new Promotion { Name = "Drum Wrestling" },
                StartDate   = start,
                CurrentDate = start,
                Roster      = roster
            };
        }

        // ── What a number is worth to a building ─────────────────────────────

        /// <summary>
        /// **Number one and number thirty are both something. Number fifteen is nothing.**
        ///
        /// The drama in a number is its distance from the middle, and it is symmetrical
        /// because both ends make a building react. What the two ends *mean* is not
        /// symmetrical at all — that is <see cref="RumbleScoring.PullReaction"/>'s job, and
        /// keeping them apart is what lets either be asserted without running a drawing.
        /// </summary>
        [Fact]
        public void TheDramaInANumberIsItsDistanceFromTheMiddle()
        {
            double first = RumbleScoring.NumberDrama(1, 30);
            double mid   = RumbleScoring.NumberDrama(15, 30);
            double last  = RumbleScoring.NumberDrama(30, 30);

            output.WriteLine($"  #1 {first:F3}   #15 {mid:F3}   #30 {last:F3}");

            Assert.Equal(1.0, first, 3);
            Assert.Equal(1.0, last, 3);
            Assert.True(mid < 0.05, $"the middle of the field should be nothing to react to, got {mid:F3}");

            // Monotone on the way in from either end, so there is no number in between that
            // is somehow more of a story than number one.
            Assert.True(RumbleScoring.NumberDrama(2, 30) > RumbleScoring.NumberDrama(5, 30));
            Assert.True(RumbleScoring.NumberDrama(29, 30) > RumbleScoring.NumberDrama(25, 30));
        }

        /// <summary>
        /// **A face wants the bad number and a heel wants the good one — at the drawing too.**
        ///
        /// The same asymmetry the match reads, from the other end. In the match, a number is
        /// worth what the winner can do with it; at the drawing it is worth what the crowd
        /// does the second they hear it. A face pulling number two is a sentence the building
        /// gets behind. A face pulling number thirty has nothing to sell — relief is not a
        /// reaction — and that is the one flat corner of the table.
        /// </summary>
        [Fact]
        public void AFaceWantsTheBadNumber_AndAHeelWantsTheGoodOne()
        {
            double faceEarly = RumbleScoring.PullReaction(1,  30, Alignment.Face, 1.0);
            double faceLate  = RumbleScoring.PullReaction(30, 30, Alignment.Face, 1.0);
            double heelEarly = RumbleScoring.PullReaction(1,  30, Alignment.Heel, 1.0);
            double heelLate  = RumbleScoring.PullReaction(30, 30, Alignment.Heel, 1.0);

            output.WriteLine($"        early   late");
            output.WriteLine($"  face  {faceEarly:F3}   {faceLate:F3}");
            output.WriteLine($"  heel  {heelEarly:F3}   {heelLate:F3}");

            Assert.True(faceEarly > faceLate,
                "a face drawing number one is the whole reason to televise a drawing");
            Assert.True(heelLate > heelEarly,
                "a heel swanning in last is the injustice, and the injustice is the point");

            // The flat corner. A face with a comfortable number is the only cell here worth
            // less than every other cell — if it were not, there would be no bad draw.
            Assert.True(faceLate < heelEarly && faceLate < heelLate && faceLate < faceEarly,
                $"a face at number thirty should be the flattest pull on the board, got {faceLate:F3}");
        }

        /// <summary>
        /// **Nobody reacts to a number pulled by somebody they do not care about.**
        ///
        /// Connection gates the whole thing, which is what stops a booker parading six
        /// enhancement talents past a drum and calling it a segment.
        /// </summary>
        [Fact]
        public void ANumberOnlyMattersIfTheBuildingCaresWhoPulledIt()
        {
            double star   = RumbleScoring.PullReaction(1, 30, Alignment.Face, 1.30);
            double jobber = RumbleScoring.PullReaction(1, 30, Alignment.Face, 0.50);

            output.WriteLine($"  connection 1.30 -> {star:F3}   connection 0.50 -> {jobber:F3}");
            Assert.True(star > jobber * 1.2,
                $"the same number should be worth much more to a name: {star:F3} vs {jobber:F3}");
        }

        /// <summary>
        /// **The night is remembered for its best number, but padding it still drags.**
        ///
        /// Weighted to the maximum rather than the mean, because that is the pull people
        /// talk about on the way out — but the mean is a third of it, so bringing four more
        /// people out to hear four middling numbers makes the segment worse, not longer.
        /// </summary>
        [Fact]
        public void TheBestPullCarriesIt_ButPaddingStillDrags()
        {
            var one   = new[] { 0.9 };
            var padded = new[] { 0.9, 0.1, 0.1, 0.1 };

            double lean = RumbleScoring.DrawCrowd(one);
            double fat  = RumbleScoring.DrawCrowd(padded);

            output.WriteLine($"  one great pull {lean:F3}   plus three duds {fat:F3}");

            Assert.True(fat < lean, "padding a drawing out should not improve it");
            // But the great pull still dominates: the mean alone would put this at 0.30.
            Assert.True(fat > padded.Average() * 1.4,
                $"the best pull should still carry it, got {fat:F3} against a mean of {padded.Average():F3}");
        }

        // ── The price of a guaranteed number ─────────────────────────────────

        /// <summary>
        /// **Every number handed out is one the crowd stops believing.**
        ///
        /// This is the load-bearing rule of the whole feature. Without it, a booker who
        /// wants their winner at number two books a drawing, fixes it, and collects the
        /// anticipation bonus for free — which is the dominant strategy the entry-number
        /// work was written to kill, walking back in through a new door.
        ///
        /// It floors rather than reaching zero, because a crowd that knows the fix is in is
        /// still watching the fix, and that is its own kind of angle.
        /// </summary>
        [Fact]
        public void EveryFixedNumberCostsTheDrumItsCredibility()
        {
            double honest  = RumbleScoring.DrawCredibility(0, 4);
            double oneFix  = RumbleScoring.DrawCredibility(1, 4);
            double allFixed = RumbleScoring.DrawCredibility(4, 4);

            output.WriteLine($"  0/4 {honest:F2}   1/4 {oneFix:F2}   4/4 {allFixed:F2}");

            Assert.Equal(1.0, honest, 3);
            Assert.True(oneFix < honest);
            Assert.True(allFixed < oneFix);
            Assert.True(allFixed >= 0.4, "a crowd watching an open fix is still watching");
            Assert.Equal(0.4, allFixed, 2);
        }

        // ── What announcing buys the match ───────────────────────────────────

        /// <summary>
        /// **Nothing announced, nothing gained — and most of what is gained rides on the
        /// winner's number.**
        ///
        /// Zero when no drawing ran, deliberately: a Rumble booked without one scores exactly
        /// what it scored before drawings existed. The drawing is a thing to gain rather than
        /// a tax on not having one, which matters because the alternative silently rebalances
        /// every Rumble already in every save.
        /// </summary>
        [Fact]
        public void AnticipationIsZeroWithoutADrawing_AndRidesOnTheWinnersNumber()
        {
            double none        = RumbleScoring.Anticipation(false, 0, 30);
            double breadthOnly = RumbleScoring.Anticipation(false, 15, 30);
            double winnerOnly  = RumbleScoring.Anticipation(true, 1, 30);

            output.WriteLine($"  none {none:F3}   half the field {breadthOnly:F3}   " +
                             $"just the winner {winnerOnly:F3}");

            Assert.Equal(0.0, none, 5);
            Assert.True(winnerOnly > breadthOnly,
                "one announced number matters more than fifteen anonymous ones if it is the winner's");
            Assert.True(RumbleScoring.Anticipation(true, 30, 30) <= 0.5);
        }

        // ── Booking it ───────────────────────────────────────────────────────

        /// <summary>
        /// **You cannot announce a surprise, and the booker finds out while they are booking.**
        ///
        /// The cost of a drawing is that somebody whose number was read out on television
        /// cannot walk out to a shocked building. That is a validation error rather than a
        /// quiet scoring cut, because it is a decision and the booker should meet it at the
        /// point of making it rather than read about it in the show report.
        /// </summary>
        [Fact]
        public void ASurpriseCannotBeAnnounced()
        {
            var field = Enumerable.Range(1, 8).Select(i => W($"W{i}")).ToList();
            var plan  = Rumble(field, field[0]);
            plan.Moments = [new() { Kind = RumbleMomentKind.SurpriseReturn, Cast = [field[5]], At = 0.6 }];

            var draw = new RumbleDraw { Rumble = plan, Cast = [field[0], field[5]] };
            var errors = draw.Validate();
            foreach (var e in errors) output.WriteLine($"  • {e}");

            Assert.Contains(errors, e => e.Contains(field[5].RingName) && e.Contains("surprise"));

            // Take them out of the drawing and it books.
            draw.Cast = [field[0]];
            Assert.Empty(draw.Validate());
        }

        /// <summary>
        /// **A fix nobody sees is not on offer.** Rigging a number for somebody who never
        /// comes out to collect it would cost nothing, and a thing that costs nothing is
        /// what everybody does — so it is a booking error rather than a free option.
        /// </summary>
        [Fact]
        public void ANumberCannotBeFixedForSomebodyWhoNeverComesOut()
        {
            var field = Enumerable.Range(1, 8).Select(i => W($"W{i}")).ToList();
            var plan  = Rumble(field, field[0]);

            var draw = new RumbleDraw
            {
                Rumble = plan,
                Cast   = [field[0]],
                Rigged = [field[3]]          // never in the cast
            };

            var errors = draw.Validate();
            foreach (var e in errors) output.WriteLine($"  • {e}");
            Assert.Contains(errors, e => e.Contains(field[3].RingName));
        }

        /// <summary>
        /// A battle royal has nothing to draw, for exactly the reason it scores no entry
        /// story: everybody starts together, so there is no number to have a story about.
        /// </summary>
        [Fact]
        public void ABattleRoyalHasNothingToDraw()
        {
            var field = Enumerable.Range(1, 8).Select(i => W($"W{i}")).ToList();
            var plan  = Rumble(field, field[0], interval: 0);

            var errors = new RumbleDraw { Rumble = plan, Cast = [field[0]] }.Validate();
            foreach (var e in errors) output.WriteLine($"  • {e}");
            Assert.Contains(errors, e => e.Contains("starts together"));
        }

        // ── Running it ───────────────────────────────────────────────────────

        /// <summary>
        /// **The drawing draws, and only a fix survives it.**
        ///
        /// The engine calls <see cref="RumblePlan.DrawNumbers"/> on the night rather than
        /// reading numbers somebody set in the builder. That is what a drawing *is*, and it
        /// is what makes rigging the only way to keep a number once the drum is on
        /// television — which is the whole shape of the decision.
        /// </summary>
        [Fact]
        public void TheDrawingDraws_AndOnlyAFixSurvivesIt()
        {
            var field = Enumerable.Range(1, 20).Select(i => W($"W{i}")).ToList();
            var plan  = Rumble(field, field[0]);
            var kept  = field[0];

            var draw = new RumbleDraw
            {
                Rumble = plan,
                Cast   = [kept, field[1], field[2]],
                Host   = W("The General Manager"),
                Rigged = [kept]
            };

            var before = plan.Field.ToDictionary(e => e.Wrestler, e => e.Number);
            var result = new RumbleDrawEngine(Seed).Execute(draw);

            foreach (var line in result.Commentary) output.WriteLine($"  {line}");

            var after = plan.Field.ToDictionary(e => e.Wrestler, e => e.Number);

            Assert.Equal(before[kept], after[kept]);
            Assert.True(field.Skip(1).Any(w => before[w] != after[w]),
                "a drawing that changed nobody's number did not draw");

            // Only the cast's numbers are known. The rest of the field was drawn tonight
            // too — the building simply did not hear it.
            Assert.Equal(3, plan.Announced.Count());
            Assert.All(draw.Cast, w => Assert.True(
                plan.Field.Single(e => e.Wrestler == w).NumberAnnounced));
            Assert.All(field.Skip(3), w => Assert.False(
                plan.Field.Single(e => e.Wrestler == w).NumberAnnounced));
        }

        /// <summary>
        /// **A fixed drawing scores worse and makes an enemy.** Both halves, because either
        /// on its own would be the wrong trade: a cost with no angle is a punishment, and an
        /// angle with no cost is free heat.
        /// </summary>
        [Fact]
        public void AFixedDrawingScoresWorse_AndMakesAnEnemy()
        {
            var field = Enumerable.Range(1, 20).Select(i => W($"W{i}")).ToList();
            var host  = W("Authority");

            RumbleDrawResult Run(bool rig)
            {
                var plan = Rumble(field, field[0]);
                return new RumbleDrawEngine(Seed).Execute(new RumbleDraw
                {
                    Rumble = plan,
                    Cast   = [field[0], field[1]],
                    Host   = host,
                    Rigged = rig ? [field[0]] : []
                });
            }

            var honest = Run(rig: false);
            var fixedUp = Run(rig: true);

            output.WriteLine($"  honest  {honest.FinalScore:F1}  credibility {honest.Credibility:F2}");
            output.WriteLine($"  fixed   {fixedUp.FinalScore:F1}  credibility {fixedUp.Credibility:F2}");

            Assert.True(fixedUp.Credibility < honest.Credibility);
            Assert.Contains(FeudHistoryTag.PersonalInsult, fixedUp.HistoryTags);
            Assert.Contains(host, fixedUp.HeatParticipants);
            Assert.Contains(field[0], fixedUp.HeatParticipants);
            Assert.Empty(honest.HistoryTags);
        }

        // ── What it buys, end to end ─────────────────────────────────────────

        /// <summary>
        /// **The same Rumble is worth more when the building already knew the number.**
        ///
        /// The end-to-end claim, and the reason to book a drawing at all. Both sides run the
        /// same field, the same winner, the same number and the same seed — the only
        /// difference is whether the crowd was told — so the gap is the anticipation and
        /// nothing else, and it is asserted against
        /// <see cref="RumbleResult.Anticipation"/> rather than inferred from the total.
        /// </summary>
        [Fact]
        public void KnowingTheNumberIsWorthSomething_AndItIsTheAnticipationThatPaysIt()
        {
            var field = Enumerable.Range(1, 20).Select(i => W($"W{i}")).ToList();

            RumbleResult Run(bool announce)
            {
                var plan = Rumble(field, field[0]);
                if (announce)
                    plan.Field.Single(e => e.Wrestler == field[0]).NumberAnnounced = true;
                return new RumbleEngine(Seed).Execute(plan);
            }

            var quiet = Run(announce: false);
            var told  = Run(announce: true);

            output.WriteLine($"  unannounced  entry {quiet.EntryStory:F3}  " +
                             $"anticipation {quiet.Anticipation:F3}  total {quiet.FinalScore:F1}");
            output.WriteLine($"  announced    entry {told.EntryStory:F3}  " +
                             $"anticipation {told.Anticipation:F3}  total {told.FinalScore:F1}");

            Assert.Equal(0.0, quiet.Anticipation, 5);
            Assert.True(told.Anticipation > 0.3, "the winner's own number is most of the bonus");
            Assert.Equal(quiet.EntryStory * (1 + told.Anticipation), told.EntryStory, 5);
            Assert.True(told.FinalScore > quiet.FinalScore);
        }

        /// <summary>
        /// A battle royal gains nothing from a drawing that cannot have happened. Guarded
        /// separately from <see cref="RumbleScoring.Anticipation"/> because the engine is
        /// where the two facts meet, and an announced flag on a battle royal's field is a
        /// state a save file could hold even though the builder will not produce it.
        /// </summary>
        [Fact]
        public void ABattleRoyalGainsNothingFromAnnouncedNumbers()
        {
            var field = Enumerable.Range(1, 12).Select(i => W($"W{i}")).ToList();
            var plan  = Rumble(field, field[0], interval: 0);
            foreach (var e in plan.Field) e.NumberAnnounced = true;

            var result = new RumbleEngine(Seed).Execute(plan);
            output.WriteLine($"  entry {result.EntryStory:F3}  anticipation {result.Anticipation:F3}");

            Assert.Equal(0.0, result.Anticipation, 5);
            Assert.Equal(0.0, result.EntryStory, 5);
        }

        // ── On a card ────────────────────────────────────────────────────────

        /// <summary>
        /// **A drawing booked on the television writes onto the match on the pay-per-view.**
        ///
        /// The reach-forward is the whole feature, and it is the one thing no other card item
        /// does — so it is asserted through two real shows rather than by calling the engine
        /// directly. Show one runs the drawing; show two runs the Rumble and reads back what
        /// the drawing wrote.
        /// </summary>
        [Fact]
        public void ADrawingOnOneShowSetsTheNumbersForTheMatchOnTheNext()
        {
            var roster = Enumerable.Range(1, 20).Select(i => W($"W{i}")).ToList();
            var career = NewCareer(roster);

            var rumble = Rumble(roster, roster[0]);
            var ppv = career.Schedule("Royal Rumble", career.CurrentDate.AddDays(7),
                                      ShowType.PremiumEvent);
            ppv.Card.Add(rumble);

            var draw = new RumbleDraw
            {
                Rumble      = rumble,
                RumbleId    = rumble.Id,
                RumbleLabel = "the Royal Rumble",
                Cast        = [roster[0], roster[1]],
                Host        = roster[19]
            };
            var tv = career.Schedule("Go-home", career.CurrentDate, ShowType.Television);
            tv.Card.Add(draw);

            var feuds = new FeudBook();
            var night = new ShowSimulator(feuds, seed: Seed).Simulate(tv.ToShow());

            var item = night.Items.Single();
            Assert.NotNull(item.DrawResult);
            foreach (var line in item.DrawResult!.Commentary) output.WriteLine($"  {line}");
            output.WriteLine($"  scored {item.Score:F1}");

            Assert.True(item.Score > 0, "a drawing that scored nothing did not run");
            Assert.Equal(CardItemKind.Segment, item.Kind);

            // And the match it drew for now knows two of its numbers.
            Assert.Equal(2, rumble.Announced.Count());

            var match = new ShowSimulator(feuds, seed: Seed).Simulate(ppv.ToShow());
            var rumbleResult = match.Items.Single().RumbleResult;
            Assert.NotNull(rumbleResult);
            output.WriteLine($"  the match itself: anticipation {rumbleResult!.Anticipation:F3}");
            Assert.True(rumbleResult.Anticipation > 0,
                "the drawing wrote numbers the match never read");
        }

        // ── Saved and reloaded ───────────────────────────────────────────────

        /// <summary>
        /// **A drawing survives a save, and finds its match on the other show.**
        ///
        /// The only cross-show reference on a card, and a save file reads one card at a time
        /// — the December television is deserialised before the January pay-per-view exists.
        /// So the link is held by id and bound in a second pass, and this is the test that
        /// the second pass exists.
        /// </summary>
        [Fact]
        public void ADrawingFindsItsMatchAgainAfterAReload()
        {
            var roster = Enumerable.Range(1, 12).Select(i => W($"W{i}")).ToList();
            var career = NewCareer(roster);

            var rumble = Rumble(roster, roster[0]);
            career.Schedule("Royal Rumble", career.CurrentDate.AddDays(7), ShowType.PremiumEvent)
                  .Card.Add(rumble);

            career.Schedule("Go-home", career.CurrentDate, ShowType.Television)
                  .Card.Add(new RumbleDraw
                  {
                      Rumble      = rumble,
                      RumbleId    = rumble.Id,
                      RumbleLabel = "the Royal Rumble",
                      Cast        = [roster[0], roster[1]],
                      Host        = roster[11],
                      Rigged      = [roster[0]]
                  });

            var reloaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career), roster);

            var back = reloaded.Shows.SelectMany(s => s.Card).OfType<RumbleDraw>().Single();
            var backRumble = reloaded.Shows.SelectMany(s => s.Card).OfType<RumblePlan>().Single();

            output.WriteLine($"  {back.Name}, cast {back.Cast.Count}, " +
                             $"host {back.Host?.RingName}, rigged {back.Rigged.Count}");

            Assert.Same(backRumble, back.Rumble);
            Assert.Equal(2, back.Cast.Count);
            Assert.Equal(roster[11].Id, back.Host!.Id);
            Assert.Equal(roster[0].Id, back.Rigged.Single().Id);
            Assert.Empty(back.Validate());
        }

        /// <summary>
        /// And a drawing whose match is gone is dropped rather than left on the sheet
        /// pointing at nothing. The Rumble here is dropped by the existing departed-wrestler
        /// rule, which makes this the realistic version of the failure rather than a
        /// contrived one.
        /// </summary>
        [Fact]
        public void ADrawingWhoseMatchHasGone_IsDroppedToo()
        {
            var roster = Enumerable.Range(1, 12).Select(i => W($"W{i}")).ToList();
            var career = NewCareer(roster);

            var rumble = Rumble(roster, roster[0]);
            career.Schedule("Royal Rumble", career.CurrentDate.AddDays(7), ShowType.PremiumEvent)
                  .Card.Add(rumble);
            career.Schedule("Go-home", career.CurrentDate, ShowType.Television)
                  .Card.Add(new RumbleDraw
                  {
                      Rumble   = rumble,
                      RumbleId = rumble.Id,
                      Cast     = [roster[0], roster[1]]
                  });

            string json = SaveSerializer.ToJson(career);

            // One of the field has left the promotion, so the Rumble is dropped whole — and
            // the drawing has nothing left to draw for.
            var reloaded = SaveSerializer.FromJson(json, roster.Take(11).ToList());

            output.WriteLine("  card items after the departure: " +
                             string.Join(", ", reloaded.Shows.Select(s => $"{s.Name} {s.Card.Count}")));

            Assert.Empty(reloaded.Shows.SelectMany(s => s.Card));
        }
    }
}
