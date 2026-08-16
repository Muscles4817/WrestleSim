using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Segment;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Executes a booked segment and returns what it produced: crowd reaction, feud heat,
    /// history tags, popularity movement and any botch or injury.
    ///
    /// The simulator is pure with respect to output — it never writes to the console.
    /// The one side effect it does have is intentional: a segment that gets somebody over
    /// changes their Popularity, because that is the point of booking one.
    /// </summary>
    public class SegmentSimulator
    {
        private readonly Random _rand;

        public SegmentSimulator(int? seed = null)
        {
            _rand = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        }

        // ── Public entry point ───────────────────────────────────────────────

        public SegmentResult Simulate(Segment segment)
        {
            var errors = segment.Validate();
            if (errors.Any())
                throw new InvalidOperationException(
                    "Invalid segment:\n" + string.Join("\n", errors.Select(e => "  • " + e)));

            var result = new SegmentResult
            {
                SegmentName = segment.Name,
                Type        = segment.Type,
                Location    = segment.Location
            };

            double baseImpact    = 0;
            double verbalTotal   = 0;
            int    verbalCount   = 0;
            double actionHeat    = 0;
            double improvRisk    = 0;
            double injuryRisk    = 0;

            foreach (var action in segment.Actions)
            {
                baseImpact += ImpactOf(action);
                actionHeat += action.HeatImpact;

                if (IsVerbal(action.ActionType))
                {
                    verbalTotal += action.Performer.Charisma;
                    verbalCount++;

                    // Talking at somebody builds a feud too. Only physical actions carry
                    // HeatImpact from the factory, which left promo-driven angles unable
                    // to move a feud at all.
                    if (segment.Participants.Count >= 2)
                        actionHeat += VerbalHeat(action.Performer);
                }

                improvRisk += ImprovRiskOf(action, segment);
                injuryRisk += InjuryRiskOf(action);

                result.Commentary.Add(Describe(action));
            }

            // Charisma lifts talking segments; it does nothing for a run-in.
            double charismaFactor = verbalCount > 0 ? verbalTotal / verbalCount : 0;
            double impact = baseImpact + charismaFactor * 0.5;

            // Where it happens matters. The ring is the biggest stage; a GM office is not.
            impact *= LocationModifier(segment.Location);

            // Unscripted is a real trade-off: rawer and more memorable, but it can fall apart.
            if (!segment.IsScripted) impact *= 1.15;

            // ── Botch ────────────────────────────────────────────────────────
            // Risk is a straight percentage. The old version gated on improvRisk > 15,
            // which a single-action promo could never reach, so promos could not botch.
            if (improvRisk > 0 && _rand.Next(0, 100) < improvRisk)
            {
                result.Botched = true;
                impact *= 0.6;
                result.Commentary.Add("The segment falls apart — a blown line and dead air. The crowd cools off.");
            }

            result.AudienceImpact = Math.Clamp(impact, 0, 10);

            // ── Injury ───────────────────────────────────────────────────────
            if (injuryRisk > 0 && _rand.Next(0, 100) < injuryRisk)
            {
                var victim = segment.Actions
                    .Where(a => a.IsPhysical && a.Target != null)
                    .Select(a => a.Target!)
                    .FirstOrDefault();

                if (victim != null)
                {
                    result.Injured = victim;
                    result.HistoryTags.Add(FeudHistoryTag.InjuryAngle);
                    result.Commentary.Add($"{victim.RingName} is not getting up — officials are checking on them.");
                }
            }

            // ── Heat ─────────────────────────────────────────────────────────
            // Physical spots carry the heat, but a promo that genuinely connects
            // builds a feud too — otherwise talking could never start one.
            result.HeatGenerated = Math.Max(0, actionHeat + result.AudienceImpact * 0.5);
            if (result.Botched) result.HeatGenerated *= 0.5;

            foreach (var tag in segment.HistoryTags)
                if (!result.HistoryTags.Contains(tag))
                    result.HistoryTags.Add(tag);

            // ── Overness ─────────────────────────────────────────────────────
            ApplyOverness(segment, result);

            segment.AudienceImpact = result.AudienceImpact;
            segment.HeatImpact     = result.HeatGenerated;

            return result;
        }

        // ── Impact ───────────────────────────────────────────────────────────

        private static double ImpactOf(SegmentAction action) =>
            action.BaseImpact > 0 ? action.BaseImpact : DefaultImpact(action.ActionType);

        private static double DefaultImpact(SegmentActionType type) => type switch
        {
            SegmentActionType.Talk      => 2.0,
            SegmentActionType.Interrupt => 1.5,
            SegmentActionType.Attack    => 3.0,
            SegmentActionType.RunIn     => 2.5,
            SegmentActionType.Betrayal  => 4.0,
            _                           => 0
        };

        private static bool IsVerbal(SegmentActionType type) =>
            type is SegmentActionType.Talk or SegmentActionType.Interrupt;

        /// <summary>
        /// Feud heat generated by talking at a rival. Scales with charisma, so a great
        /// promo builds a programme faster than a bad one — but never as fast as a chair shot.
        /// </summary>
        private static double VerbalHeat(Wrestler performer) =>
            0.8 + performer.Charisma * 0.4;

        /// <summary>
        /// Wires up the previously-dead SegmentLocation axis. A backstage promo is
        /// intimate but reaches a smaller crowd; the crowd itself is chaos.
        /// </summary>
        private static double LocationModifier(SegmentLocation location) => location switch
        {
            SegmentLocation.Ring       => 1.00,
            SegmentLocation.Crowd      => 1.10,
            SegmentLocation.ParkingLot => 0.90,
            SegmentLocation.Backstage  => 0.85,
            SegmentLocation.GMOffice   => 0.80,
            _                          => 1.00
        };

        // ── Risk ─────────────────────────────────────────────────────────────

        private static double ImprovRiskOf(SegmentAction action, Segment segment)
        {
            if (segment.IsScripted) return 0;

            // Psychology is what keeps an unscripted segment on the rails.
            double psych = action.Performer.Mental?.Psychology ?? 0;
            return psych < 70 ? 10 : 2;
        }

        private static double InjuryRiskOf(SegmentAction action)
        {
            if (!action.IsPhysical || action.Target == null) return 0;

            // Toughness resists — previously this stat was defined and never read.
            double toughness = action.Target.Mental?.Toughness ?? 0;
            return 5.0 * (1.0 - toughness / 150.0);
        }

        // ── Overness ─────────────────────────────────────────────────────────

        private void ApplyOverness(Segment segment, SegmentResult result)
        {
            var deltas = new Dictionary<Wrestler, double>();

            foreach (var action in segment.Actions)
            {
                double delta = action.OvernessImpact;

                // A segment that falls apart costs the people in it. Previously overness
                // was clamped to 0.5..3.0 and only ever added, so promos were free points.
                if (result.Botched) delta = -Math.Abs(delta) * 0.5;

                deltas.TryGetValue(action.Performer, out double running);
                deltas[action.Performer] = running + delta;
            }

            // Getting beaten up in someone else's angle costs you a little.
            if (result.Injured != null)
            {
                deltas.TryGetValue(result.Injured, out double running);
                deltas[result.Injured] = running - 1.0;
            }

            foreach (var (wrestler, raw) in deltas)
            {
                int delta = (int)Math.Round(raw);
                if (delta == 0) continue;

                int before = wrestler.Popularity;
                wrestler.Popularity = Math.Clamp(before + delta, 0, 100);

                int applied = wrestler.Popularity - before;
                if (applied != 0)
                    result.OvernessChanges.Add(new OvernessChange { Wrestler = wrestler, Delta = applied });
            }
        }

        // ── Commentary ───────────────────────────────────────────────────────

        private string Describe(SegmentAction action)
        {
            string who    = action.Performer.RingName;
            string target = action.Target?.RingName ?? "";
            string label  = string.IsNullOrWhiteSpace(action.Label) ? action.ActionType.ToString() : action.Label;

            string line = action.ActionType switch
            {
                SegmentActionType.Talk =>
                    Pick($"{who} takes the microphone.",
                         $"{who} addresses the crowd.",
                         $"{who} has something to get off their chest."),

                SegmentActionType.Interrupt =>
                    Pick($"{who}'s music hits — they are cutting this off!",
                         $"{who} interrupts, and the crowd reacts!",
                         $"{who} has heard enough and cuts in."),

                SegmentActionType.Attack =>
                    Pick($"{who} attacks {target} out of nowhere!",
                         $"{who} lays out {target}!",
                         $"{who} jumps {target} before they can react!"),

                SegmentActionType.RunIn =>
                    Pick($"{who} comes charging down the ramp!",
                         $"{who} hits the ring at full speed!",
                         $"{who} is here — and they are not alone in this story!"),

                SegmentActionType.Betrayal =>
                    Pick($"{who} turns on {target}! Nobody saw this coming!",
                         $"Betrayal! {who} has been playing {target} all along!",
                         $"{who} stabs {target} in the back — the crowd is stunned!"),

                _ => $"{who} — {label}."
            };

            if (!string.IsNullOrWhiteSpace(action.Dialogue))
                line += $"  \"{action.Dialogue}\"";

            return line;
        }

        private string Pick(params string[] options) => options[_rand.Next(options.Length)];
    }
}
