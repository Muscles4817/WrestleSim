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

        /// <summary>The promotion's creative team, 1–10 (Promos), for whatever of a promo is written.</summary>
        private readonly int _creative;

        public SegmentSimulator(int? seed = null, int creative = 5)
        {
            _rand = seed.HasValue ? new Random(seed.Value) : Random.Shared;
            _creative = Math.Clamp(creative, Promos.MinCreative, Promos.MaxCreative);
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
            double worstPromo    = 100;
            string? worstLine    = null;
            var    staging       = Promos.StagingOf(segment);
            double actionHeat    = 0;
            double improvRisk    = 0;
            double injuryRisk    = 0;

            foreach (var action in segment.Actions)
            {
                baseImpact += ImpactOf(action);
                actionHeat += action.HeatImpact;

                result.Commentary.Add(Describe(action));

                if (IsVerbal(action.ActionType))
                {
                    // A promo is a performance (docs/gimmick-plan.md Part 3): what is written,
                    // where, in what note, by whom. Charisma is in it as presence; the rest is
                    // the person's skill at exactly this, and the writers' for what is written.
                    var register = Promos.RegisterFor(segment, action.Performer);
                    var reading  = Promos.Read(action.Performer, segment.Form, staging, register, Promos.Writing(_creative));
                    double cut   = Promos.Cut(reading, _rand);
                    result.Promos[action.Performer] = cut;

                    verbalTotal += Promos.OnSegmentScale(cut);     // the 0–5 scale charisma used to fill
                    verbalCount++;

                    if (Promos.Line(action.Performer, segment.Form, staging, register, reading, cut) is { } line)
                    {
                        result.Commentary.Add(line);
                        result.PromoLines.Add(line);
                    }
                    if (cut < worstPromo) { worstPromo = cut; worstLine = Promos.Line(action.Performer, segment.Form, staging, register, reading, cut); }

                    // Talking at somebody builds a feud too, by how well it was said.
                    if (segment.Participants.Count >= 2)
                        actionHeat += VerbalHeat(cut);
                }
                else
                {
                    improvRisk += ImprovRiskOf(action, segment);
                }

                injuryRisk += InjuryRiskOf(action);
            }

            // How well it was said lifts talking segments; it does nothing for a run-in.
            double charismaFactor = verbalCount > 0 ? verbalTotal / verbalCount : 0;
            double impact = baseImpact + charismaFactor * 0.5;

            // Where it happens matters. The ring is the biggest stage; a GM office is not.
            impact *= LocationModifier(segment.Location);

            // No bonus for either form (owner, 17 Nov 2026). Unscripted used to get ×1.15 here,
            // and a scripted promo could not go wrong: the form was deciding what the people
            // in it should.

            // ── Botch ────────────────────────────────────────────────────────
            // A promo falls apart when somebody in it bombs — scripted or not; a script read
            // badly dies as surely as dead air. A planned brawl worked loose can still go wrong
            // the old way: psychology, when nothing is planned.
            if (worstPromo < Promos.Bombed)
            {
                result.Botched = true;
                impact *= 0.6;
                if (worstLine is null) result.Commentary.Add("The segment falls apart. The crowd cools off.");
            }
            else if (improvRisk > 0 && _rand.Next(0, 100) < improvRisk)
            {
                result.Botched = true;
                impact *= 0.6;
                result.Commentary.Add("The segment falls apart — a spot is blown and the brawl goes nowhere. The crowd cools off.");
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
        /// Feud heat generated by talking at a rival. Scales with how well it was said, so a great
        /// promo builds a programme faster than a bad one — but never as fast as a chair shot.
        /// </summary>
        private static double VerbalHeat(double promo) =>
            0.8 + Promos.OnSegmentScale(promo) * 0.4;

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
            if (segment.Form != PromoForm.Free) return 0;

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
                // Segments move momentum far more than overness. A hot angle makes you hot
                // this week; it does not by itself make the audience care about you.
                double momentum = raw * 3.0;

                // Gains compress near the ceiling and losses near the floor, so a promo is
                // no longer a free point that ratchets everyone toward 100 over a career.
                double overness = raw >= 0
                    ? HeatEconomy.DampenGain(wrestler, raw * 0.5)
                    : -HeatEconomy.DampenLoss(wrestler.Overness, -raw * 0.5);

                double before = wrestler.Overness;
                wrestler.Overness = Math.Clamp(before + overness, 0, 100);
                wrestler.Momentum = Math.Clamp(wrestler.Momentum + momentum, -100, 100);

                double applied = wrestler.Overness - before;
                if (Math.Abs(applied) >= 0.01 || Math.Abs(momentum) >= 0.5)
                    result.OvernessChanges.Add(new OvernessChange
                    {
                        Wrestler      = wrestler,
                        Delta         = applied,
                        MomentumDelta = momentum
                    });
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
