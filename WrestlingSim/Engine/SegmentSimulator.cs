using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Enums;
using WrestlingSim.Models.Segment;

namespace WrestlingSim.Engine
{
    public class SegmentSimulator
    {
        private readonly Random _rand = new Random();

        public void SimulateSegment(Segment segment)
        {
            Console.WriteLine($"\n--- Simulating Segment: {segment.Name} ---");
            LogSegmentInfo(segment);

            double baseImpact = 0;
            double totalCharisma = 0;
            double improvRisk = 0;
            double injuryRisk = 0;

            foreach (var action in segment.Actions)
            {
                Console.WriteLine($"{action.Performer.Gimmick.Name} performs {action.ActionType}");

                baseImpact += CalculateBaseImpact(action, segment, ref injuryRisk);
                totalCharisma += CalculateCharismaContribution(action);
                ApplyOvernessImpact(action);
                improvRisk += CalculateImprovRisk(action, segment);
            }

            FinalizeAudienceImpact(segment, baseImpact, totalCharisma);
            HandleBotch(segment, improvRisk);
            HandleInjury(segment, injuryRisk);
            ClampAudienceImpact(segment);

            LogSegmentResult(segment);
        }

        private void LogSegmentInfo(Segment segment)
        {
            Console.WriteLine($"Type: {segment.Type}, Location: {segment.Location}, Scripted: {segment.IsScripted}");
            Console.WriteLine($"Participants: {string.Join(", ", segment.Participants.Select(p => p.Gimmick.Name))}\n");
        }

        private double CalculateBaseImpact(SegmentAction action, Segment segment, ref double injuryRisk)
        {
            switch (action.ActionType)
            {
                case SegmentActionType.Talk:
                    return 2.0;
                case SegmentActionType.Interrupt:
                    return 1.5;
                case SegmentActionType.Attack:
                    segment.HeatImpact += 4.0;
                    injuryRisk += 5.0;
                    return 3.0;
                case SegmentActionType.RunIn:
                    segment.HeatImpact += 3.0;
                    return 2.5;
                case SegmentActionType.Betrayal:
                    segment.HeatImpact += 6.0;
                    return 4.0;
                default:
                    return 0;
            }
        }

        private double CalculateCharismaContribution(SegmentAction action)
        {
            if (action.ActionType == SegmentActionType.Talk || action.ActionType == SegmentActionType.Interrupt)
                return action.Performer.Charisma;
            return 0;
        }

        private void ApplyOvernessImpact(SegmentAction action)
        {
            action.Performer.Popularity += (int)Math.Round(action.OvernessImpact);
        }

        private double CalculateImprovRisk(SegmentAction action, Segment segment)
        {
            if (!segment.IsScripted)
            {
                double psych = action.Performer.Mental.Psychology;
                return (psych < 70) ? 10 : 2;
            }
            return 0;
        }

        private void FinalizeAudienceImpact(Segment segment, double baseImpact, double totalCharisma)
        {
            double charismaFactor = totalCharisma / (segment.Actions.Count > 0 ? segment.Actions.Count : 1);
            segment.AudienceImpact = baseImpact + (charismaFactor * 0.5);
        }

        private void HandleBotch(Segment segment, double improvRisk)
        {
            if (!segment.IsScripted && improvRisk > 15 && _rand.Next(0, 100) < improvRisk)
            {
                Console.WriteLine("\n❗ Botch occurred! Audience cooled off.");
                segment.AudienceImpact *= 0.6;
            }
        }

        private void HandleInjury(Segment segment, double injuryRisk)
        {
            if (injuryRisk > 0 && _rand.Next(0, 100) < injuryRisk)
            {
                var victim = segment.Actions.Where(a => a.Target != null).Select(a => a.Target).FirstOrDefault();
                if (victim != null)
                {
                    Console.WriteLine($"❗ Injury: {victim.Gimmick.Name} injured during brawl!");
                    // Future: integrate injury system
                }
            }
        }

        private void ClampAudienceImpact(Segment segment)
        {
            segment.AudienceImpact = Math.Clamp(segment.AudienceImpact, 0, 10);
        }

        private void LogSegmentResult(Segment segment)
        {
            Console.WriteLine($"\n--- Segment Result ---");
            Console.WriteLine($"Audience Impact: {segment.AudienceImpact:F1} / 10");
            Console.WriteLine($"Heat Impact: {segment.HeatImpact:F1}");
        }
    }
}
