using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Segment;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Runs a full show card top to bottom. Matches go through the beat engine and
    /// segments through the segment simulator; both deposit heat into the shared
    /// FeudBook, which is what carries a story from one item to the next.
    /// </summary>
    public class ShowSimulator
    {
        private readonly FeudBook _feudBook;
        private readonly int? _seed;
        private readonly BrandContext? _brands;

        /// <summary>
        /// The promotion's belts, when it has any. Optional because exhibition mode books
        /// matches in a world with no championships in it — a plan can still put a title
        /// on the line without one, but only the registry can spot a champion losing a
        /// *non-title* match, which is doc 21 §4.1's whole subject.
        /// </summary>
        private readonly TitleRegistry? _titles;

        /// <summary>
        /// When the promotion last ran each gimmick match, or null in exhibition — where
        /// there is no calendar, nothing has happened before, and every stipulation is
        /// therefore as fresh as it will ever be.
        /// </summary>
        private readonly StipulationBook? _stipulations;

        /// <summary>Most a card can lose for running past its allotted runtime.</summary>
        private const double MaxOverrunPenalty = 0.35;

        public ShowSimulator(
            FeudBook feudBook,
            int? seed = null,
            TitleRegistry? titles = null,
            BrandContext? brands = null,
            StipulationBook? stipulations = null)
        {
            _feudBook     = feudBook;
            _seed         = seed;
            _titles       = titles;
            _brands       = brands;
            _stipulations = stipulations;
        }

        public ShowResult Simulate(Show show)
        {
            var result = new ShowResult
            {
                BookedMinutes = show.BookedMinutes,
                BudgetMinutes = show.TotalDurationMinutes
            };

            double totalScore = 0;
            double weightSum  = 0;
            double crowdMood  = 5.0; // baseline excitement, 0–10

            // Freshness is measured against the night the show happens, not the night it
            // was written — see MatchEngine.Execute.
            var showDate = DateOnly.FromDateTime(show.Date);

            // ── Brands ───────────────────────────────────────────────────────
            // Read once, before anything on the card has run: the crossover bonus and the
            // star-making penalty are both priced at the integrity the show started with,
            // so the cost of tonight's crossovers is paid by later shows, not this one.
            bool branded    = _brands is { Enforced: true };
            var  split      = _brands?.Split;
            var  homeBrand  = _brands?.HomeBrand;
            double integrity = branded ? split!.Integrity : 100;

            var crossovers = branded
                ? BrandIntegrity.Detect(show.Card, split!, homeBrand)
                : Array.Empty<Crossover>();

            var crossoverById = crossovers.ToDictionary(
                c => c.Wrestler.Id, StringComparer.OrdinalIgnoreCase);

            // A brand that cannot make its own stars is not a brand. Both halves of this
            // are doc 22: §4.1 for integrity, §4.2 for the B-show spiral.
            double starMaking = branded
                ? BrandIntegrity.StarMakingFactor(integrity) * BrandIntegrity.StandingFactor(homeBrand!, split!)
                : 1.0;

            for (int i = 0; i < show.Card.Count; i++)
            {
                var item = show.Card[i];

                var itemResult = new CardItemResult
                {
                    Label           = $"{i + 1}. {item.Name}",
                    Kind            = item.Kind,
                    DurationMinutes = item.DurationMinutes,
                    CrowdMoodBefore = crowdMood
                };

                // ── Run it ───────────────────────────────────────────────────
                double raw = item switch
                {
                    BookedMatch match => RunMatch(
                        match, itemResult, result, i, showDate, show.Name, starMaking),
                    Segment segment   => RunSegment(segment, itemResult, result, showDate),
                    Models.Rumble.RumblePlan rumble => RunRumble(rumble, itemResult, result, index: i),
                    Models.Rumble.RumbleDraw draw => RunDraw(draw, itemResult, result, showDate, index: i),

                    // Deliberately still here, and deliberately still zero. A card item this
                    // does not know how to run should score nothing rather than something
                    // plausible — a Rumble fell through here scoring zero until it was given
                    // an arm, which is exactly the failure the fallthrough is for surfacing.
                    _                 => 0
                };

                // ── The crossover, paid out ──────────────────────────────────
                // Someone who is not supposed to be here is an event, and the audience
                // responds to it. This is the locally correct decision every time — and it
                // is worth less every time, because the bonus scales with an integrity that
                // this same booking is spending. Doc 22 §4.1.
                double attraction = item.Wrestlers
                    .Where(w => crossoverById.ContainsKey(w.Id))
                    .Select(w => BrandIntegrity.AttractionBonus(w, integrity))
                    .DefaultIfEmpty(0)
                    .Max();

                if (attraction > 0)
                {
                    raw *= 1 + attraction;
                    itemResult.Notes.Add(
                        $"Crossover appearance — a name from another brand (+{attraction * 100:F0}%).");
                }

                itemResult.RawScore = raw;
                double score = raw;

                // ── Position on the card ─────────────────────────────────────
                double weight = 1.0;
                if (i == 0) weight = 1.2;                       // opener sets the tone
                if (i == show.Card.Count - 1) weight = 1.5;     // main event carries the show
                itemResult.PositionWeight = weight;

                // ── Same thing twice in a row ────────────────────────────────
                // Was `i > 1`, which let the second item on the card escape the rule.
                if (i > 0 && show.Card[i - 1].Kind == item.Kind)
                {
                    score *= 0.85;
                    itemResult.FatiguePenaltyApplied = true;
                    itemResult.Notes.Add("Follows the same kind of item — crowd fatigue (×0.85).");
                }

                // ── Crowd mood carries between items ─────────────────────────
                // crowdMood was previously computed and never read.
                score *= 0.9 + crowdMood / 10.0 * 0.2;

                itemResult.Score = Math.Clamp(score, 0, 100);

                crowdMood = Math.Clamp(crowdMood + MoodSwing(item, raw), 0, 10);

                totalScore += itemResult.Score * weight;
                weightSum  += weight;

                result.Items.Add(itemResult);
            }

            double overall = weightSum > 0 ? totalScore / weightSum : 0;

            // ── Brand-based stakes ───────────────────────────────────────────
            // A show that kept to its own roster is worth a little more, because the
            // audience believes the brand is a real place with a real top of the card. That
            // belief is exactly what integrity measures, so the bonus disappears with it —
            // doc 22 §3.4 on stakes, §4.1 on where they go.
            double exclusivity = 0;
            if (branded && crossovers.Count == 0)
            {
                exclusivity = BrandIntegrity.ExclusivityBonus(integrity);
                overall *= 1 + exclusivity;
            }

            // Everyone who worked the show was seen tonight. Absence is measured from here,
            // so this has to happen for every appearance, not just the ones that won.
            foreach (var wrestler in show.Card.SelectMany(i => i.Wrestlers).Distinct())
                wrestler.LastAppearance = showDate;

            // And everyone who wrestled paid for it, and got sharper for it. Charged per
            // card item rather than once per night, so a wrestler booked twice pays twice —
            // which is the only thing stopping a booker working their main eventer three
            // times on the same show for free.
            foreach (var item in show.Card) ChargeRingCondition(item);

            // ── Running long ─────────────────────────────────────────────────
            double overrun = Math.Min(MaxOverrunPenalty, show.OverrunFraction);
            if (overrun > 0)
            {
                overall *= 1 - overrun;
                result.OverrunPenalty = overrun;
            }

            result.OverallRating  = Math.Round(Math.Clamp(overall, 0, 100), 2);
            result.FinalCrowdMood = crowdMood;

            if (branded) result.Brand = SettleBrand(
                show, split!, homeBrand!, crossovers, integrity, starMaking, exclusivity, result.OverallRating);

            return result;
        }

        // ── Brands ───────────────────────────────────────────────────────────

        /// <summary>
        /// Charges the show's crossovers to the split and records the brand's form.
        ///
        /// This is where the erosion is actually committed, and it is deliberately after
        /// the card has been scored: the player sees the rating the crossover bought before
        /// they see what it cost, which is the order in which the decision looks correct.
        /// </summary>
        private static BrandShowReport SettleBrand(
            Show show,
            BrandSplit split,
            Brand homeBrand,
            IReadOnlyList<Crossover> crossovers,
            double integrityBefore,
            double starMaking,
            double exclusivity,
            double overallRating)
        {
            var date = DateOnly.FromDateTime(show.Date);
            var notes = new List<CrossoverNote>();

            foreach (var crossover in crossovers)
            {
                split.ApplyCrossover(new CrossoverRecord
                {
                    WrestlerId    = crossover.Wrestler.Id,
                    WrestlerName  = crossover.Wrestler.RingName ?? crossover.Wrestler.RealName,
                    HomeBrandName = crossover.Home.Name,
                    ShowBrandName = crossover.ShowBrand.Name,
                    ShowName      = show.Name,
                    Date          = date,
                    Cost          = crossover.Cost
                }, BrandIntegrity.PermanentShare);

                notes.Add(new CrossoverNote
                {
                    Wrestler   = crossover.Wrestler.RingName ?? crossover.Wrestler.RealName,
                    HomeBrand  = crossover.Home.Name,
                    Cost       = crossover.Cost,
                    Attraction = BrandIntegrity.AttractionBonus(crossover.Wrestler, integrityBefore)
                });
            }

            homeBrand.RecordShow(overallRating);

            return new BrandShowReport
            {
                BrandName        = homeBrand.Name,
                IntegrityBefore  = integrityBefore,
                IntegrityAfter   = split.Integrity,
                Ceiling          = split.Ceiling,
                Crossovers       = notes,
                StarMakingFactor = starMaking,
                ExclusivityBonus = exclusivity
            };
        }

        // ── Item execution ───────────────────────────────────────────────────

        /// <summary>
        /// How much of a tag match's heat each singles pairing inside it picks up.
        ///
        /// Revised after review, which found the original 0.25 doing the opposite of what
        /// it was documented to do. Two things were wrong. The justification was
        /// arithmetically self-defeating — four cross-pairs at a quarter each is a whole
        /// singles match's worth of singles heat, landing exactly on the boundary it was
        /// chosen to stay under. And the heat was discounted while the *staleness* was not:
        /// every cross-pair also took a full RecordMatch, so after three tag matches each
        /// singles pairing sat below the cold threshold — no feud benefit at all — while
        /// carrying a 35% familiarity penalty. The singles blow-off a tag programme is
        /// supposed to build arrived actively worse off than a fresh pairing.
        ///
        /// Now a sixth, and no staleness at all: seeing two men on opposite sides of a tag
        /// match is not the audience having seen that singles match. It is what makes them
        /// want it.
        /// </summary>
        private const double CrossPairHeatShare = 1.0 / 6.0;

        /// <summary>The chemistry of whichever plan side these people are, for the status economy.</summary>
        private static double ChemistryOf(Models.MatchPlan.MatchPlan plan, IReadOnlyList<Wrestler> members) =>
            members.Count > 0 && plan.SideA.Contains(members[0]) ? plan.SideA.Chemistry : plan.SideB.Chemistry;

        /// <summary>
        /// Runs a battle royal on a card.
        ///
        /// A sibling of <see cref="RunMatch"/> rather than a wrapper around it: the two share
        /// the shape — run it, note it, move standing — and nothing else, because the thing
        /// being run is graded on moments and the thing being moved is a whole field's
        /// standing rather than two men's.
        ///
        /// No feud familiarity read, and that is a decision rather than an omission. A
        /// battle royal is not a pairing, so there is no pairing for the crowd to be sick
        /// of; running the same thirty people again next month is a different problem and
        /// not one the staleness rule models.
        /// </summary>
        private double RunRumble(
            Models.Rumble.RumblePlan rumble, CardItemResult itemResult, ShowResult showResult,
            int index)
        {
            var result = new RumbleEngine(_seed.HasValue ? _seed.Value + index : Random.Shared.Next())
                .Execute(rumble);

            itemResult.RumbleResult = result;
            itemResult.Notes.Add($"{result.Winner.RingName} wins the {rumble.Field.Count}-wrestler " +
                                 $"{(rumble.IsBattleRoyal ? "battle royal" : "rumble")} — {result.StarRating:F2}★");

            if (result.IronMan is { } iron && iron != result.Winner && result.IronManOutlasted > 0)
                itemResult.Notes.Add($"{iron.RingName} outlasted {result.IronManOutlasted} of them.");

            var outcome = HeatEconomy.ForRumble(
                result.Winner, rumble.Wrestlers, result.StarRating,
                result.IronMan, result.IronManShare);

            foreach (var change in outcome.All)
            {
                HeatEconomy.Apply(change);
                if (change.IsMeaningful) showResult.StatusChanges.Add(change);
            }

            return result.FinalScore;
        }

        /// <summary>
        /// Runs a number drawing on a card.
        ///
        /// Scored and paced as a segment, because that is what a crowd is sitting through,
        /// and it deposits heat like one. What it does that no other card item does is
        /// reach forward: the numbers it draws are written onto a match booked on a *later*
        /// show, which is the whole reason a drawing is worth a slot.
        /// </summary>
        private double RunDraw(
            Models.Rumble.RumbleDraw draw, CardItemResult itemResult, ShowResult showResult,
            DateOnly showDate, int index)
        {
            // A dangling reference is the one failure mode a booker can create from outside
            // this card — deleting the show that held the match, say — so it is reported
            // rather than thrown. Every other way a drawing can be wrong is something the
            // builder would not let them book in the first place, and those still throw.
            if (draw.Rumble is null)
            {
                itemResult.Notes.Add(
                    "The match this was drawing for is not booked any more. Nothing to draw.");
                return 0;
            }

            var result = new RumbleDrawEngine(_seed.HasValue ? _seed.Value + index : Random.Shared.Next())
                .Execute(draw);

            itemResult.DrawResult = result;

            foreach (var pull in result.Pulls)
                itemResult.Notes.Add(
                    $"{pull.Wrestler.RingName} — number {pull.Number}" +
                    (pull.Rigged ? ", handed over." : "."));

            if (result.Credibility < 1.0)
                itemResult.Notes.Add(
                    $"The drum was worth {result.Credibility * 100:F0}% of what an honest one is.");

            showResult.FeudUpdates.AddRange(_feudBook.RecordSegment(
                result.HeatParticipants, result.HeatGenerated, result.HistoryTags, showDate));

            return result.FinalScore;
        }

        /// <summary>
        /// What the gimmick did, in a line the booker can act on.
        ///
        /// Says *why* rather than only how much: an unearned stipulation and an overused one
        /// are different mistakes with different fixes — build the feud, or wait — and a
        /// report that only printed a number would leave the player to guess which.
        /// </summary>
        private static string StipulationNote(
            Stipulation stipulation, double stakes, int? daysSince)
        {
            string label = StipulationRules.Label(stipulation);

            if (stakes < 0)
                return $"{label} — the story had not got there yet. The room knew " +
                       $"({stakes:F1} crowd energy).";

            double freshness = StipulationRules.Scarcity(daysSince);
            if (freshness < 0.9 && daysSince is { } days)
                return $"{label} — last one was {days} days ago, so it was worth " +
                       $"{freshness * 100:F0}% of a rare one (+{stakes:F1}).";

            return $"{label} — earned, and it has been a while (+{stakes:F1}).";
        }

        /// <summary>
        /// Bills one card item to everybody who worked it: fatigue up, sharpness up.
        ///
        /// Reads the booking rather than the result, deliberately. What a match takes out
        /// of somebody is how long they were out there and how fast they went — decided
        /// when it was laid out — not how well it happened to go on the night.
        /// </summary>
        private static void ChargeRingCondition(ICardItem item)
        {
            switch (item)
            {
                case BookedMatch match:
                {
                    double minutes = match.Plan.Beats.Sum(b => b.DurationMinutes);
                    double pace    = RingCondition.Pace(match.Plan.Beats);

                    foreach (var side in match.Plan.Sides)
                    {
                        double share = RingCondition.WorkShare(side.Members.Count);
                        foreach (var w in side.Members)
                            Bill(w, minutes, pace, share);
                    }
                    break;
                }

                case Models.Rumble.RumblePlan rumble:
                {
                    // No beats to read, so the cost comes from how much of it each of them
                    // was actually in — a battle royal is cheap for the twelve people
                    // thrown out early and expensive for whoever went the distance. Pace is
                    // taken as brisk-but-not-frantic: it is a scramble, not a sprint.
                    double full = rumble.DurationMinutes;
                    int field   = Math.Max(1, rumble.Field.Count);

                    foreach (var (entrant, i) in rumble.Field.Select((e, i) => (e, i)))
                    {
                        // Without a result to read, entry order is the honest proxy for how
                        // long somebody was in there: the last man out started last.
                        double survived = full * (0.35 + 0.65 * (i + 1) / field);
                        Bill(entrant.Wrestler, survived, pace: 1.2, share: 1.0);
                    }
                    break;
                }

                // A segment is not a match. Standing in the ring talking costs a wrestler
                // nothing worth modelling and sharpens nothing — and a drawing, a promo or
                // a beatdown all read the same way here, which is why none of them are
                // billed at all.
            }
        }

        private static void Bill(Wrestler w, double minutes, double pace, double share)
        {
            double conditioning = new PerformerProfile(w).BaseConditioning;

            w.Fatigue = Math.Clamp(
                w.Fatigue + RingCondition.MatchCost(minutes, pace, w.Style, share, conditioning),
                0, 100);

            double upkeep = RingCondition.SelfMaintenance(
                w.Mental?.Psychology ?? 70, w.Mental?.RingIQ ?? 70);

            w.Sharpness = Math.Clamp(
                w.Sharpness + RingCondition.SharpnessGain(minutes, pace, upkeep, share), 0, 100);
        }

        private double RunMatch(
            BookedMatch match, CardItemResult itemResult, ShowResult showResult,
            int index, DateOnly showDate, string showName, double starMaking)
        {
            // How sick of this pairing the crowd is, read before the match is recorded
            // against it — docs/wrestling-reference/20-storylines-and-feuds.md §9.1.
            // Taken from the feud book rather than match.Plan.Feud, because a booker who
            // declines to attach a feud has still booked the same two men for the fifth
            // time and the audience does not care what the plan says.
            // Keyed on the two *sides*, not on the two starters. A crowd's appetite for
            // The Usos against The New Day is its own thing, separate from its appetite
            // for any one of those men against any other, and it has to wear out
            // separately — docs/wrestling-reference/20-storylines-and-feuds.md §9.1.
            var sideA = match.Plan.SideA.Members;
            var sideB = match.Plan.SideB.Members;

            var feud = _feudBook.GetOrCreate(sideA, sideB);
            double familiarity = feud.Familiarity(showDate);

            // Read before this match is recorded, so a card running two cages does not let
            // the first one launder the second — the second is the one a day old.
            int? sinceStipulation = _stipulations?.DaysSince(match.Plan.Stipulation, showDate);

            var engineResult = new MatchEngine(_seed.HasValue ? _seed + index : null)
                .Execute(match.Plan, familiarity, sinceStipulation);

            _stipulations?.Record(match.Plan.Stipulation, showDate);

            itemResult.MatchResult = engineResult;
            itemResult.Notes.Add($"{engineResult.Winner.RingName} def. {engineResult.Loser.RingName} — {engineResult.StarDisplay}");
            if (engineResult.StalenessNote is { } stale) itemResult.Notes.Add(stale);

            if (match.Plan.Stipulation != Stipulation.None)
                itemResult.Notes.Add(StipulationNote(
                    match.Plan.Stipulation, engineResult.StipulationStakes, sinceStipulation));

            // ── Status ───────────────────────────────────────────────────────
            // The result is not just a rating. A win moves standing, and how much depends
            // on who was beaten and how decisively — see HeatEconomy.
            // Weighed under the rules the match was worked under. In a No-DQ match a run-in
            // is not an excuse, so the loss is a clean one — and, through
            // TitleEconomy.ChangesHands and without a line of code there, the belt moves.
            var finishBeat = match.Plan.Beats.LastOrDefault(b => b.IsFinish);
            var weight = finishBeat is null
                ? FinishWeight.Decisive
                : StipulationRules.Weigh(match.Plan.Stipulation, finishBeat.Type);

            // In a tag match the fall is between two men, but the statement is between two
            // teams: the pool is set by each side's standing, and then the man who scored
            // and the man who was pinned take it in full while their partners take a share.
            // That asymmetry is what makes "have the other guy take the fall" a real,
            // costed booking lever — doc 12 §6.1.
            var outcome = match.Plan.IsTagMatch
                ? HeatEconomy.ForSides(
                    engineResult.WinningSide, engineResult.Pinner,
                    engineResult.LosingSide, engineResult.Pinned,
                    engineResult.StarRating, weight, familiarity,
                    winningChemistry: ChemistryOf(match.Plan, engineResult.WinningSide),
                    losingChemistry:  ChemistryOf(match.Plan, engineResult.LosingSide))
                : HeatEconomy.ForMatch(
                    engineResult.Winner, engineResult.Loser, engineResult.StarRating, weight, familiarity,
                    sideCount: match.Plan.Sides.Count);

            foreach (var raw in outcome.All)
            {
                // Overness *won* on a brand show is scaled by what the brand is worth.
                // Losses are not: a bad night costs the same wherever it happens, and
                // making the B-show a safe place to lose would invert the whole point.
                var change = starMaking < 1.0 && raw.OvernessDelta > 0
                    ? raw with { OvernessDelta = raw.OvernessDelta * starMaking }
                    : raw;

                HeatEconomy.Apply(change);
                if (change.IsMeaningful) showResult.StatusChanges.Add(change);
            }

            // ── Championships ────────────────────────────────────────────────
            ResolveTitles(match, engineResult, weight, itemResult, showResult, showDate, showName);

            // ── Feud ─────────────────────────────────────────────────────────
            // A match between rivals is itself a chapter in the feud.
            double heat = engineResult.StarRating * 2.0;

            var update = _feudBook.Record(
                sideA, sideB, heat, tags: new[] { FeudHistoryTag.PriorMatch }, date: showDate);

            update.Feud.RecordMatch(showDate);

            // In a multi-man match sides A and B are not "the two sides" — they are the
            // first two listed, and everybody in there wrestled everybody. Recording only
            // the first pair credited a rivalry to whoever happened to be typed first and
            // gave the third side's stories nothing, which a test looking for the *absence*
            // of heat is how this surfaced.
            //
            // The other pairs get the same fraction the cross-pairs of a tag match get, and
            // for the same reason: they were in there together, which is not the same as
            // having had the match.
            if (match.Plan.IsMultiMan)
            {
                for (int i = 0; i < match.Plan.Sides.Count; i++)
                for (int j = i + 1; j < match.Plan.Sides.Count; j++)
                {
                    if (i == 0 && j == 1) continue;      // recorded in full above
                    _feudBook.Record(match.Plan.Sides[i].Members, match.Plan.Sides[j].Members,
                                     heat * CrossPairHeatShare, date: showDate);
                }
            }

            // Did this settle anything? Doc 20 §6 — a blow-off resolves, and until one does
            // the audience is being asked to keep caring about a question nobody is
            // answering. Past the third such match they stop, and the pairing carries that
            // for good (§9's interference loop: nothing resolves, so nothing matters).
            if (match.Plan.IsBlowOff && match.Plan.Feud is { } declared)
            {
                // A blow-off has to *resolve* — doc 20 §6.1 puts that first. Booked to a
                // disqualification, a count-out or a run-in it settles nothing, and it
                // costs more than an ordinary unfinished match because the crowd was told
                // this was the ending.
                if (weight == FinishWeight.Protected)
                {
                    declared.RecordBrokenPromise();
                    itemResult.Notes.Add(
                        $"The {declared.SideAName}–{declared.SideBName} blow-off settled nothing. " +
                        "They were promised an ending and did not get one.");
                }
                else
                {
                    declared.BlowOff(showDate);
                    itemResult.Notes.Add(
                        $"The {declared.SideAName}–{declared.SideBName} feud is settled. That story is over.");
                }
            }
            else
            {
                var beforeDistrust = update.Feud.Distrust;
                update.Feud.RecordUnresolved();
                if (update.Feud.Distrust > beforeDistrust)
                    itemResult.Notes.Add(
                        $"{update.Feud.MatchesSinceHot} matches and nothing settled — the crowd is " +
                        "starting to believe this is not going anywhere.");
            }

            showResult.FeudUpdates.Add(update);

            // ── Blame ────────────────────────────────────────────────────────
            // A multi-man match advances the stories *inside* it, not just the one it was
            // billed as. Doc 18 §2.5: the reason to run two rivals into a three-way is that
            // being cost the match by somebody you already hate escalates the feud without
            // spending the singles match on it — which is why the finish where two big names
            // wreck each other and the third steals it is booked as often as it is.
            //
            // The grievance runs one way. Whoever was denied leaves with it, and how much
            // depends on what it actually cost them.
            foreach (var moment in engineResult.GrudgeMoments)
            {
                bool aggrievedPinned = engineResult.LosingSide.Contains(moment.Against);
                bool aggrievedLost   = aggrievedPinned
                                       || !engineResult.WinningSide.Contains(moment.Against);

                double blame = MatchEngine.BlameHeat(
                    engineResult.StarRating, aggrievedLost, aggrievedPinned);
                if (blame <= 0) continue;

                var blamed = _feudBook.Record(
                    moment.Against, moment.By, blame,
                    tags: new[] { FeudHistoryTag.PersonalInsult }, date: showDate);

                showResult.FeudUpdates.Add(blamed);

                if (aggrievedPinned)
                    itemResult.Notes.Add(
                        $"{moment.Against.RingName} took the fall after {moment.By.RingName} " +
                        "cost them the match. That is not going to be forgotten.");
            }

            // A tag programme also builds the singles rivalries inside it, at a fraction —
            // which is how a team feud pays off in a singles blow-off. Only recorded for a
            // genuine tag match; in singles the cross-pair *is* the feud above.
            //
            // Deliberately no RecordMatch on the cross-pairs. Staleness measures how often
            // the crowd has been asked to watch *this* match, and they have not watched it:
            // two men on opposite sides of a tag match is the thing that makes people want
            // the singles match, not a substitute for having seen it.
            if (match.Plan.IsTagMatch)
            {
                foreach (var a in sideA)
                foreach (var b in sideB)
                    _feudBook.Record(a, b, heat * CrossPairHeatShare, date: showDate);
            }

            // ── Teams ────────────────────────────────────────────────────────
            // Chemistry is built out of matches actually worked together, so it is
            // recorded here rather than when a team is formed. Career.AdvanceOneDay
            // decays it again for every day they do not.
            foreach (var side in new[] { match.Plan.SideA, match.Plan.SideB })
                side.Team?.RecordMatch(showDate);

            return engineResult.StarRating * 20.0; // 0–5★ → 0–100
        }

        /// <summary>
        /// Applies the result to whatever championship it touched: the belt on the line,
        /// or — when nothing was on the line — the belts held by whoever just lost.
        ///
        /// The second half is doc 21 §4.1. Putting a champion in a non-title match and
        /// beating them is the standard booking shortcut, and it should cost something
        /// every single time it is used.
        /// </summary>
        private void ResolveTitles(
            BookedMatch match, Models.MatchPlan.MatchEngineResult engineResult, FinishWeight weight,
            CardItemResult itemResult, ShowResult showResult, DateOnly date, string showName)
        {
            var updates = new List<TitleUpdate>();

            if (match.Plan.TitleAtStake is { } title && !title.Retired)
            {
                updates.Add(TitleEconomy.ResolveTitleMatch(
                    title,
                    engineResult.WinningSide, engineResult.LosingSide,
                    engineResult.Pinner, engineResult.Pinned,
                    weight, engineResult.StarRating, date, showName));
            }
            else if (_titles != null)
            {
                // Charged against the man who was actually beaten, not against whichever
                // holder happens to be listed first on the belt.
                foreach (var held in _titles.HeldBy(engineResult.Pinned))
                    updates.Add(TitleEconomy.ApplyNonTitleLoss(
                        held, engineResult.Pinned, engineResult.Pinner, weight));
            }

            foreach (var update in updates)
            {
                itemResult.Notes.Add(update.Reason);

                if (update.StatusBonus is { } bonus)
                {
                    HeatEconomy.Apply(bonus);
                    showResult.StatusChanges.Add(bonus);
                }

                // A tag belt is won by the team, so the partner gets it too.
                foreach (var partnerBonus in update.PartnerBonuses)
                {
                    HeatEconomy.Apply(partnerBonus);
                    showResult.StatusChanges.Add(partnerBonus);
                }

                if (update.IsMeaningful) showResult.TitleUpdates.Add(update);
            }
        }

        private double RunSegment(Segment segment, CardItemResult itemResult, ShowResult showResult,
            DateOnly showDate)
        {
            var segResult = new SegmentSimulator(_seed).Simulate(segment);
            itemResult.SegmentResult = segResult;

            if (segResult.Botched) itemResult.Notes.Add("Botched.");
            if (segResult.Injured != null) itemResult.Notes.Add($"{segResult.Injured.RingName} injured.");

            foreach (var change in segResult.OvernessChanges)
                itemResult.Notes.Add($"{change.Wrestler.RingName} popularity {change.Delta:+0;-0}.");

            var updates = _feudBook.RecordSegment(
                segment.Participants, segResult.HeatGenerated, segResult.HistoryTags, showDate);

            showResult.FeudUpdates.AddRange(updates);

            return segResult.Score;
        }

        /// <summary>How much this item moved the crowd for whatever comes next.</summary>
        private static double MoodSwing(ICardItem item, double rawScore) => item.Kind switch
        {
            CardItemKind.Match   => rawScore / 100.0 * 2.0 - 0.5,
            CardItemKind.Segment => rawScore / 100.0 * 1.5 - 0.4,
            _                    => 0
        };
    }
}
