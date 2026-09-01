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

        /// <summary>Most a card can lose for running past its allotted runtime.</summary>
        private const double MaxOverrunPenalty = 0.35;

        public ShowSimulator(
            FeudBook feudBook,
            int? seed = null,
            TitleRegistry? titles = null,
            BrandContext? brands = null)
        {
            _feudBook = feudBook;
            _seed     = seed;
            _titles   = titles;
            _brands   = brands;
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
                    Segment segment   => RunSegment(segment, itemResult, result),
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

        private double RunMatch(
            BookedMatch match, CardItemResult itemResult, ShowResult showResult,
            int index, DateOnly showDate, string showName, double starMaking)
        {
            // How sick of this pairing the crowd is, read before the match is recorded
            // against it — docs/wrestling-reference/20-storylines-and-feuds.md §9.1.
            // Taken from the feud book rather than match.Plan.Feud, because a booker who
            // declines to attach a feud has still booked the same two men for the fifth
            // time and the audience does not care what the plan says.
            var feud = _feudBook.GetOrCreate(match.Plan.WrestlerA, match.Plan.WrestlerB);
            double familiarity = feud.Familiarity(showDate);

            var engineResult = new MatchEngine(_seed.HasValue ? _seed + index : null)
                .Execute(match.Plan, familiarity);

            itemResult.MatchResult = engineResult;
            itemResult.Notes.Add($"{engineResult.Winner.RingName} def. {engineResult.Loser.RingName} — {engineResult.StarDisplay}");
            if (engineResult.StalenessNote is { } stale) itemResult.Notes.Add(stale);

            // ── Status ───────────────────────────────────────────────────────
            // The result is not just a rating. A win moves standing, and how much depends
            // on who was beaten and how decisively — see HeatEconomy.
            var finishBeat = match.Plan.Beats.LastOrDefault(b => b.IsFinish);
            var weight = finishBeat is null
                ? FinishWeight.Decisive
                : HeatEconomy.WeightOf(finishBeat.Type);

            var outcome = HeatEconomy.ForMatch(
                engineResult.Winner, engineResult.Loser, engineResult.StarRating, weight, familiarity);

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
            var update = _feudBook.Record(
                match.Plan.WrestlerA,
                match.Plan.WrestlerB,
                heat: engineResult.StarRating * 2.0,
                tags: new[] { FeudHistoryTag.PriorMatch });

            update.Feud.RecordMatch(showDate);
            showResult.FeudUpdates.Add(update);

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
                    title, engineResult.Winner, engineResult.Loser, weight,
                    engineResult.StarRating, date, showName));
            }
            else if (_titles != null)
            {
                foreach (var held in _titles.HeldBy(engineResult.Loser))
                    updates.Add(TitleEconomy.ApplyNonTitleLoss(held, engineResult.Winner, weight));
            }

            foreach (var update in updates)
            {
                itemResult.Notes.Add(update.Reason);

                if (update.StatusBonus is { } bonus)
                {
                    HeatEconomy.Apply(bonus);
                    showResult.StatusChanges.Add(bonus);
                }

                if (update.IsMeaningful) showResult.TitleUpdates.Add(update);
            }
        }

        private double RunSegment(Segment segment, CardItemResult itemResult, ShowResult showResult)
        {
            var segResult = new SegmentSimulator(_seed).Simulate(segment);
            itemResult.SegmentResult = segResult;

            if (segResult.Botched) itemResult.Notes.Add("Botched.");
            if (segResult.Injured != null) itemResult.Notes.Add($"{segResult.Injured.RingName} injured.");

            foreach (var change in segResult.OvernessChanges)
                itemResult.Notes.Add($"{change.Wrestler.RingName} popularity {change.Delta:+0;-0}.");

            var updates = _feudBook.RecordSegment(
                segment.Participants, segResult.HeatGenerated, segResult.HistoryTags);

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
