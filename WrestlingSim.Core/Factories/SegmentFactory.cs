using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Segment;

namespace WrestlingSim.Factories
{
    public static class SegmentFactory
    {
        /// <summary>
        /// Creates a basic promo segment with one speaker.
        /// </summary>
        public static Segment CreatePromo(Wrestler speaker, string dialogue, bool isScripted = true)
        {
            var segment = new Segment($"{speaker.Gimmick.Name} Promo", SegmentType.Promo, SegmentLocation.Ring, isScripted);
            segment.AddParticipant(speaker);

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Talk,
                Performer = speaker,
                Dialogue = dialogue,
                OvernessImpact = CalculateOvernessImpact(speaker, SegmentActionType.Talk)
            });

            return segment;
        }

        /// <summary>
        /// Creates a confrontation segment where one wrestler interrupts another's promo.
        /// </summary>
        public static Segment CreateConfrontation(Wrestler speaker, Wrestler interrupter, string speakerDialogue, string interruptDialogue, bool isScripted = true)
        {
            var segment = new Segment($"{speaker.Gimmick.Name} vs {interrupter.Gimmick.Name} Confrontation", SegmentType.Confrontation, SegmentLocation.Ring, isScripted);
            segment.AddParticipant(speaker);
            segment.AddParticipant(interrupter);

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Talk,
                Performer = speaker,
                Dialogue = speakerDialogue,
                OvernessImpact = CalculateOvernessImpact(speaker, SegmentActionType.Talk)
            });

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Interrupt,
                Performer = interrupter,
                Dialogue = interruptDialogue,
                OvernessImpact = CalculateOvernessImpact(interrupter, SegmentActionType.Interrupt)
            });

            return segment;
        }

        /// <summary>
        /// Creates a contract signing template with optional fight at the end.
        /// </summary>
        public static Segment CreateContractSigning(Wrestler wrestlerA, Wrestler wrestlerB, bool endsInBrawl = true)
        {
            var segment = new Segment($"{wrestlerA.Gimmick.Name} & {wrestlerB.Gimmick.Name} Contract Signing", SegmentType.ContractSigning, SegmentLocation.Ring, isScripted: true);
            segment.AddParticipant(wrestlerA);
            segment.AddParticipant(wrestlerB);

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Talk,
                Performer = wrestlerA,
                Dialogue = "I'm signing this contract to end you.",
                OvernessImpact = CalculateOvernessImpact(wrestlerA, SegmentActionType.Talk)
            });

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Talk,
                Performer = wrestlerB,
                Dialogue = "You'll regret it.",
                OvernessImpact = CalculateOvernessImpact(wrestlerB, SegmentActionType.Talk)
            });

            if (endsInBrawl)
            {
                segment.AddAction(new SegmentAction
                {
                    ActionType = SegmentActionType.Attack,
                    Performer = wrestlerB,
                    Target = wrestlerA,
                    HeatImpact = CalculateHeatImpact(SegmentActionType.Attack),
                    OvernessImpact = CalculateOvernessImpact(wrestlerB, SegmentActionType.Attack)
                });
            }

            return segment;
        }

        /// <summary>
        /// Surprise return segment where a hidden wrestler attacks a current participant.
        /// </summary>
        public static Segment CreateSurpriseReturn(Wrestler returningWrestler, Wrestler victim)
        {
            var segment = new Segment($"{returningWrestler.Gimmick.Name} Surprise Return", SegmentType.SurpriseReturn, SegmentLocation.Ring, isScripted: false);
            segment.AddParticipant(returningWrestler);
            segment.AddParticipant(victim);

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.RunIn,
                Performer = returningWrestler,
                Dialogue = "",
                OvernessImpact = CalculateOvernessImpact(returningWrestler, SegmentActionType.RunIn)
            });

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Attack,
                Performer = returningWrestler,
                Target = victim,
                HeatImpact = CalculateHeatImpact(SegmentActionType.Attack),
                OvernessImpact = CalculateOvernessImpact(returningWrestler, SegmentActionType.Attack)
            });

            return segment;
        }
        /// <summary>
        /// A heated backstage interview where the second participant interrupts and attacks.
        /// </summary>
        public static Segment CreateBackstageInterview(Wrestler interviewee, Wrestler interrupter)
        {
            var segment = new Segment($"{interviewee.Gimmick.Name} Backstage Interview", SegmentType.Promo, SegmentLocation.Backstage, isScripted: true);
            segment.AddParticipant(interviewee);
            segment.AddParticipant(interrupter);

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Talk,
                Performer = interviewee,
                Dialogue = "Tonight, I prove why I'm the best.",
                OvernessImpact = CalculateOvernessImpact(interviewee, SegmentActionType.Talk)
            });

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Interrupt,
                Performer = interrupter,
                Dialogue = "Not if I stop you first.",
                OvernessImpact = CalculateOvernessImpact(interrupter, SegmentActionType.Interrupt)
            });

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Attack,
                Performer = interrupter,
                Target = interviewee,
                HeatImpact = CalculateHeatImpact(SegmentActionType.Attack),
                OvernessImpact = CalculateOvernessImpact(interrupter, SegmentActionType.Attack)
            });

            return segment;
        }

        /// <summary>
        /// A post-match beatdown by one or more wrestlers.
        /// </summary>
        public static Segment CreatePostMatchBeatdown(Wrestler victim, List<Wrestler> attackers)
        {
            var segment = new Segment($"{victim.Gimmick.Name} Post-Match Beatdown", SegmentType.Brawl, SegmentLocation.Ring, isScripted: false);
            segment.AddParticipant(victim);
            attackers.ForEach(a => segment.AddParticipant(a));

            foreach (var attacker in attackers)
            {
                segment.AddAction(new SegmentAction
                {
                    ActionType = SegmentActionType.Attack,
                    Performer = attacker,
                    Target = victim,
                    HeatImpact = CalculateHeatImpact(SegmentActionType.Attack),
                    OvernessImpact = CalculateOvernessImpact(attacker, SegmentActionType.Attack)
                });
            }

            return segment;
        }

        /// <summary>
        /// A betrayal segment where a tag partner or faction member turns on their ally.
        /// </summary>
        public static Segment CreateBetrayal(Wrestler betrayer, Wrestler victim)
        {
            var segment = new Segment($"{betrayer.Gimmick.Name} Betrays {victim.Gimmick.Name}", SegmentType.Brawl, SegmentLocation.Ring, isScripted: false);
            segment.AddParticipant(betrayer);
            segment.AddParticipant(victim);

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Betrayal,
                Performer = betrayer,
                Target = victim,
                HeatImpact = CalculateHeatImpact(SegmentActionType.Betrayal),
                OvernessImpact = CalculateOvernessImpact(betrayer, SegmentActionType.Betrayal)
            });

            return segment;
        }

        /// <summary>
        /// A faction beatdown with promo and group dominance display.
        /// </summary>
        public static Segment CreateFactionDominance(List<Wrestler> factionMembers, Wrestler victim)
        {
            var segment = new Segment("Faction Dominance", SegmentType.Brawl, SegmentLocation.Ring, isScripted: false);
            factionMembers.ForEach(m => segment.AddParticipant(m));
            segment.AddParticipant(victim);

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Talk,
                Performer = factionMembers[0],
                Dialogue = "We run this place now.",
                OvernessImpact = CalculateOvernessImpact(factionMembers[0], SegmentActionType.Talk)
            });

            foreach (var member in factionMembers)
            {
                segment.AddAction(new SegmentAction
                {
                    ActionType = SegmentActionType.Attack,
                    Performer = member,
                    Target = victim,
                    HeatImpact = CalculateHeatImpact(SegmentActionType.Attack),
                    OvernessImpact = CalculateOvernessImpact(member, SegmentActionType.Attack)
                });
            }

            return segment;
        }

        /// <summary>
        /// A celebration segment for a champion, possibly leading to an interruption.
        /// </summary>
        public static Segment CreateChampionCelebration(Wrestler champion, Wrestler interrupter = null)
        {
            var segment = new Segment($"{champion.Gimmick.Name} Championship Celebration", SegmentType.Celebration, SegmentLocation.Ring, isScripted: true);
            segment.AddParticipant(champion);

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Talk,
                Performer = champion,
                Dialogue = "Nobody can stop me now!",
                OvernessImpact = CalculateOvernessImpact(champion, SegmentActionType.Talk)
            });

            if (interrupter != null)
            {
                segment.AddParticipant(interrupter);
                segment.AddAction(new SegmentAction
                {
                    ActionType = SegmentActionType.Interrupt,
                    Performer = interrupter,
                    Dialogue = "Enjoy it while it lasts.",
                    OvernessImpact = CalculateOvernessImpact(interrupter, SegmentActionType.Interrupt)
                });
            }

            return segment;
        }

        /// <summary>
        /// A GM announcement segment that sets up a big match.
        /// </summary>
        public static Segment CreateGMAnnouncement(Wrestler gm, string announcement, List<Wrestler> involvedWrestlers)
        {
            var segment = new Segment("Authority Announcement", SegmentType.Promo, SegmentLocation.Ring, isScripted: true);
            segment.AddParticipant(gm);
            involvedWrestlers.ForEach(w => segment.AddParticipant(w));

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Talk,
                Performer = gm,
                Dialogue = announcement,
                OvernessImpact = CalculateOvernessImpact(gm, SegmentActionType.Talk)
            });

            return segment;
        }

        /// <summary>
        /// A wild brawl that starts in the crowd and spills backstage.
        /// </summary>
        public static Segment CreateCrowdBrawl(Wrestler wrestlerA, Wrestler wrestlerB)
        {
            var segment = new Segment("Crowd Chaos", SegmentType.Brawl, SegmentLocation.Crowd, isScripted: false);
            segment.AddParticipant(wrestlerA);
            segment.AddParticipant(wrestlerB);

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Attack,
                Performer = wrestlerA,
                Target = wrestlerB,
                HeatImpact = CalculateHeatImpact(SegmentActionType.Attack),
                OvernessImpact = CalculateOvernessImpact(wrestlerA, SegmentActionType.Attack)
            });

            segment.AddAction(new SegmentAction
            {
                ActionType = SegmentActionType.Attack,
                Performer = wrestlerB,
                Target = wrestlerA,
                HeatImpact = CalculateHeatImpact(SegmentActionType.Attack),
                OvernessImpact = CalculateOvernessImpact(wrestlerB, SegmentActionType.Attack)
            });

            return segment;
        }

        private static double CalculateOvernessImpact(Wrestler performer, SegmentActionType actionType)
        {
            double baseImpact = 0.5;

            // Promo-related actions rely on Charisma
            if (actionType == SegmentActionType.Talk || actionType == SegmentActionType.Interrupt)
                baseImpact += performer.Charisma * 0.4;

            // Attacks or betrayals boost Overness more if aggressive and shocking
            if (actionType == SegmentActionType.Attack || actionType == SegmentActionType.Betrayal)
                baseImpact += 1.0 + (performer.Physical.Strength / 100.0);

            // Clamp between 0.5 and 3.0
            return Math.Clamp(baseImpact, 0.5, 3.0);
        }

        private static double CalculateHeatImpact(SegmentActionType actionType)
        {
            return actionType switch
            {
                SegmentActionType.Attack => 3.0,
                SegmentActionType.Betrayal => 5.0,
                SegmentActionType.RunIn => 2.5,
                _ => 0.0
            };
        }
    }
}
