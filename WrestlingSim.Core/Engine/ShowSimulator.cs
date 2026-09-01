using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Segment;

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

        /// <summary>Most a card can lose for running past its allotted runtime.</summary>
        private const double MaxOverrunPenalty = 0.35;

        public ShowSimulator(FeudBook feudBook, int? seed = null)
        {
            _feudBook = feudBook;
            _seed     = seed;
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
                    BookedMatch match => RunMatch(match, itemResult, result, i),
                    Segment segment   => RunSegment(segment, itemResult, result),
                    _                 => 0
                };

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

            // Everyone who worked the show was seen tonight. Absence is measured from here,
            // so this has to happen for every appearance, not just the ones that won.
            var date = DateOnly.FromDateTime(show.Date);
            foreach (var wrestler in show.Card.SelectMany(i => i.Wrestlers).Distinct())
                wrestler.LastAppearance = date;

            // ── Running long ─────────────────────────────────────────────────
            double overrun = Math.Min(MaxOverrunPenalty, show.OverrunFraction);
            if (overrun > 0)
            {
                overall *= 1 - overrun;
                result.OverrunPenalty = overrun;
            }

            result.OverallRating  = Math.Round(Math.Clamp(overall, 0, 100), 2);
            result.FinalCrowdMood = crowdMood;

            return result;
        }

        // ── Item execution ───────────────────────────────────────────────────

        private double RunMatch(BookedMatch match, CardItemResult itemResult, ShowResult showResult, int index)
        {
            var engineResult = new MatchEngine(_seed.HasValue ? _seed + index : null).Execute(match.Plan);
            itemResult.MatchResult = engineResult;
            itemResult.Notes.Add($"{engineResult.Winner.RingName} def. {engineResult.Loser.RingName} — {engineResult.StarDisplay}");

            // ── Status ───────────────────────────────────────────────────────
            // The result is not just a rating. A win moves standing, and how much depends
            // on who was beaten and how decisively — see HeatEconomy.
            var finishBeat = match.Plan.Beats.LastOrDefault(b => b.IsFinish);
            var weight = finishBeat is null
                ? FinishWeight.Decisive
                : HeatEconomy.WeightOf(finishBeat.Type);

            var outcome = HeatEconomy.ForMatch(
                engineResult.Winner, engineResult.Loser, engineResult.StarRating, weight);

            foreach (var change in outcome.All)
            {
                HeatEconomy.Apply(change);
                if (change.IsMeaningful) showResult.StatusChanges.Add(change);
            }

            // ── Feud ─────────────────────────────────────────────────────────
            // A match between rivals is itself a chapter in the feud.
            var update = _feudBook.Record(
                match.Plan.WrestlerA,
                match.Plan.WrestlerB,
                heat: engineResult.StarRating * 2.0,
                tags: new[] { FeudHistoryTag.PriorMatch });

            update.Feud.MatchCount++;
            showResult.FeudUpdates.Add(update);

            return engineResult.StarRating * 20.0; // 0–5★ → 0–100
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
