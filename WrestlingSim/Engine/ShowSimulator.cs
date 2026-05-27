using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Models;
using WrestlingSim.Models.Segment;

namespace WrestlingSim.Engine
{
    public class ShowSimulator
    {
        private readonly MatchSimulator _matchSimulator;
        private readonly SegmentSimulator _segmentSimulator;

        public ShowSimulator(MatchSimulator matchSimulator, SegmentSimulator segmentSimulator)
        {
            _matchSimulator = matchSimulator;
            _segmentSimulator = segmentSimulator;
        }

        public ShowResult SimulateShow(Show show)
        {
            double totalScore = 0;
            double weightSum = 0;
            var breakdown = new Dictionary<string, double>();
            double audienceMood = 5.0; // baseline excitement 0-10

            for (int i = 0; i < show.Card.Count; i++)
            {
                var item = show.Card[i];
                double score = 0;
                double weight = 1;

                if (item is Match match)
                {
                    score = _matchSimulator.CalculateMatchRating(match) * 20; // Convert 0-5 → 0-100
                    if (i == 0) weight = 1.2; // opener bonus
                    if (i == show.Card.Count - 1) weight = 1.5; // main event heavy weight
                    audienceMood += score / 100 * 2;
                }
                else if (item is Segment segment)
                {
                    _segmentSimulator.SimulateSegment(segment);
                    score = segment.AudienceImpact * 10; // Impact 0-10 → 0-100
                    audienceMood += segment.AudienceImpact / 2;
                }

                audienceMood = Math.Clamp(audienceMood, 0, 10);

                // Adjust for fatigue
                if (i > 1 && show.Card[i - 1].GetType() == item.GetType())
                    score *= 0.85; // penalty for two promos or two matches in a row

                breakdown[item is Match ? $"Match {i + 1}" : $"Segment {i + 1}"] = score;
                totalScore += score * weight;
                weightSum += weight;
            }

            double overallRating = Math.Round(totalScore / weightSum, 2);
            return new ShowResult { OverallRating = overallRating, Breakdown = breakdown };
        }
    }
}
