using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchTypeEnum = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Engine
{
    public class MatchEngine
    {
        private readonly Random _rand;

        // Scale constants for the saturating normalisation of each raw accumulator.
        // A raw score equal to the scale reads as ~0.63 of the component; twice the
        // scale reads as ~0.86. Nothing ever reaches 1.0, so piling on beats has a
        // hard asymptote instead of a cliff at a clamp.
        private const double TechScale  = 48.0;
        private const double StoryScale = 62.0;

        // Band the weighted crowd reading is normalised against. Below the floor the
        // building is dead; at the reference ceiling it is as hot as a crowd ever gets.
        private const double CrowdFloor      = 28.0;
        private const double CrowdCeilingRef = 95.0;

        // Kept public-ish as documentation of the old ceilings; display code uses them.
        public const double MaxTechnical    = 60.0;
        public const double MaxStorytelling = 80.0;

        public MatchEngine(int? seed = null)
        {
            _rand = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        }

        // ── Per-match context ────────────────────────────────────────────────

        /// <summary>Everything a beat handler needs, resolved once so handlers stay reentrant.</summary>
        private sealed class Ctx
        {
            public required MatchPlan Plan { get; init; }
            public required MatchEngineState State { get; init; }
            public required PerformerProfile A { get; init; }
            public required PerformerProfile B { get; init; }

            public PerformerProfile For(Wrestler w) => w == Plan.WrestlerA ? A : B;

            /// <summary>Average of both performers on a factor — for beats nobody controls.</summary>
            public double Pair(Func<PerformerProfile, double> f) => (f(A) + f(B)) / 2.0;
        }

        // ── Public entry point ───────────────────────────────────────────────

        public MatchEngineResult Execute(MatchPlan plan)
        {
            var errors = plan.Validate();
            if (errors.Any())
                throw new InvalidOperationException(
                    "Invalid match plan:\n" + string.Join("\n", errors.Select(e => "  • " + e)));

            var ctx = new Ctx
            {
                Plan  = plan,
                State = new MatchEngineState(),
                A     = new PerformerProfile(plan.WrestlerA),
                B     = new PerformerProfile(plan.WrestlerB)
            };

            InitialiseState(ctx);

            var beatResults = new List<BeatResult>();

            foreach (var beat in plan.Beats)
            {
                var result = ExecuteBeat(beat, ctx);
                beatResults.Add(result);

                // Natural energy decay between beats (except after the finish)
                if (!beat.IsFinish)
                    ctx.State.ApplyDecay();
            }

            return BuildResult(ctx, beatResults);
        }

        // ── State initialisation ─────────────────────────────────────────────

        private void InitialiseState(Ctx ctx)
        {
            var plan  = ctx.Plan;
            var state = ctx.State;

            double avgPop = (plan.WrestlerA.EffectiveOverness + plan.WrestlerB.EffectiveOverness) / 2.0;

            // Crowd disposition modifier: rewards having BOTH wrestlers over, not just one.
            // Using Min rather than average means one nobody eliminates the bonus —
            // the crowd doesn't start hot just because one star is in the match.
            double bothOverBonus = Math.Min(ctx.A.Disposition, ctx.B.Disposition) * 8.0; // up to +8

            // Connection also moves the opening bell: a building that came to see these two
            // specific people starts louder than one that recognises neither.
            double connectionLift = (ctx.Pair(p => p.Connection) - 1.0) * 14.0;

            double baseEnergy = (avgPop / 100.0) * 60.0 + bothOverBonus + connectionLift;
            double feudBonus  = plan.Feud?.StartingEnergyBonus ?? 0;

            // How loud this pairing can ever get. A card full of people the audience has
            // no investment in tops out well short of a main-event reaction, which is what
            // stops crowd score from being a constant across every match on the show.
            //
            // Craft raises the ceiling a little but cannot substitute for it: two excellent
            // workers can win a cold building over somewhat, they cannot manufacture the
            // reaction that only comes from the crowd already caring who you are.
            double pairConnection = ctx.Pair(p => p.Connection);
            double pairCraft      = ctx.Pair(p => (p.Workrate + p.RingPsych) / 2.0);

            // Asking for a long match from two people who cannot go long costs you the room.
            // Nothing below seven beats is demanding enough for conditioning to show.
            double lengthStress   = Math.Max(0, plan.Beats.Count - 6) / 6.0;
            double staminaPenalty = lengthStress * (1.0 - ctx.Pair(p => p.Conditioning)) * 34.0;

            state.CrowdCeiling = Math.Clamp(
                40.0 + pairConnection * 46.0 + (pairCraft - 1.0) * 20.0 - staminaPenalty, 35, 100);

            state.CrowdEnergy = Math.Clamp(baseEnergy + feudBonus, 8, Math.Min(90, state.CrowdCeiling));
            state.Advantage    = 0;
            state.CrowdPeakEnergy = state.CrowdEnergy;

            state.RecordEnergy();
        }

        // ── Beat dispatch ────────────────────────────────────────────────────

        private BeatResult ExecuteBeat(MatchBeat beat, Ctx ctx)
        {
            var plan  = ctx.Plan;
            var state = ctx.State;

            var result = new BeatResult
            {
                BeatType = beat.Type,
                Control  = beat.Control
            };

            // Wrestler references for this beat
            Wrestler? control = plan.ControlWrestler(beat);
            Wrestler other    = control != null ? plan.OtherWrestler(control) : plan.WrestlerB;

            double iMod = beat.IntensityModifier;
            double dMod = beat.DurationModifier;

            // ── Repetition and fatigue ───────────────────────────────────────
            // The crowd's appetite for a beat type falls off each time it is repeated,
            // and both wrestlers slow down as a long match wears on.
            int timesUsed = state.RegisterBeat(beat.Type);
            double repetition = Math.Pow(RepetitionDecay(beat.Type), timesUsed - 1);
            double fade = FadeFactor(ctx);

            // Technical work accumulates more legitimately than crowd reaction does —
            // limb work repeated is a story, a third identical brawl is not.
            double repCrowd = repetition * fade;
            double repTech  = Math.Sqrt(repetition) * fade;

            result.RepetitionFactor = repetition;

            // ── Feud amplification ───────────────────────────────────────────
            double feudMult = FeudMultiplier(beat, plan, out bool resonanceActive);
            result.FeudalResonanceActivated = resonanceActive;

            switch (beat.Type)
            {
                case BeatType.HotOpening:
                    ApplyHotOpening(result, ctx, iMod, dMod);
                    break;

                case BeatType.SlowOpening:
                    ApplySlowOpening(result, ctx, iMod, dMod);
                    break;

                case BeatType.StandardOpening:
                    ApplyStandardOpening(result, ctx, iMod, dMod);
                    break;

                case BeatType.HeatSegment:
                    ApplyHeatSegment(result, beat, ctx, control, other, iMod, dMod);
                    break;

                case BeatType.Comeback:
                    ApplyComeback(result, beat, ctx, control, other, iMod, dMod);
                    break;

                case BeatType.NearFall:
                    ApplyNearFall(result, beat, ctx, control, other, iMod, feudMult, timesUsed);
                    break;

                case BeatType.HighSpot:
                    ApplyHighSpot(result, beat, ctx, control, iMod);
                    break;

                case BeatType.RestHold:
                    ApplyRestHold(result, beat, ctx, control, iMod, dMod);
                    break;

                case BeatType.CrowdBrawl:
                    ApplyCrowdBrawl(result, beat, ctx, control, iMod, dMod);
                    break;

                case BeatType.PsychologicalWarfare:
                    ApplyPsychologicalWarfare(result, beat, ctx, control, other, iMod, feudMult);
                    break;

                case BeatType.FeudalEscalation:
                    ApplyFeudalEscalation(result, ctx, iMod, feudMult);
                    break;

                case BeatType.RevengeSpot:
                    ApplyRevengeSpot(result, beat, ctx, control, other, iMod, feudMult);
                    break;

                case BeatType.ThirdPartyPullIn:
                    ApplyThirdPartyPullIn(result, ctx, iMod, feudMult);
                    break;

                case BeatType.AlliesRejected:
                    ApplyAlliesRejected(result, beat, ctx, control, iMod);
                    break;

                case BeatType.FinishClean:
                case BeatType.FinishRollup:
                case BeatType.FinishSubmission:
                case BeatType.FinishDQ:
                case BeatType.FinishCountout:
                case BeatType.FinishInterference:
                case BeatType.FinishSuperFinisher:
                    ApplyFinish(result, beat, ctx, control, other, iMod, feudMult);
                    break;
            }

            // Apply repetition + fatigue after the handler has produced raw values.
            //
            // A finish is exempt from repetition — there is only ever one, and it should
            // land at full weight — but not from fatigue. Two exhausted wrestlers do not
            // suddenly find a crisp finishing sequence in the twentieth minute.
            if (!beat.IsFinish)
            {
                if (result.CrowdEnergyDelta > 0) result.CrowdEnergyDelta *= repCrowd;
                result.TechnicalContribution    *= repTech;
                result.StorytellingContribution *= repCrowd;
            }
            else
            {
                if (result.CrowdEnergyDelta > 0) result.CrowdEnergyDelta *= fade;
                result.TechnicalContribution    *= fade;
                result.StorytellingContribution *= fade;
            }

            // Commit deltas to state
            result.CrowdEnergyBefore = state.CrowdEnergy;
            state.ApplyEnergy(result.CrowdEnergyDelta);
            state.ApplyAdvantage(result.AdvantageDelta);
            state.TechnicalScore    += result.TechnicalContribution;
            state.StorytellingScore += result.StorytellingContribution;

            // Snapshot state into result
            result.CrowdEnergyAfter       = state.CrowdEnergy;
            result.AdvantageAfter          = state.Advantage;
            result.TechnicalScoreAfter    = state.TechnicalScore;
            result.StorytellingScoreAfter = state.StorytellingScore;

            return result;
        }

        // ── Repetition / fatigue rules ───────────────────────────────────────

        /// <summary>
        /// How much of its value a beat type retains each time it is repeated in one match.
        /// Lower = the crowd tires of it faster.
        /// </summary>
        private static double RepetitionDecay(BeatType type) => type switch
        {
            // The crowd tires of a repeated pattern quickly — this is the suplex-spam brake.
            BeatType.HeatSegment          => 0.68,
            BeatType.RestHold             => 0.60,
            BeatType.CrowdBrawl           => 0.68,
            BeatType.PsychologicalWarfare => 0.66,

            // A second comeback is a real moment, but never the first one again.
            BeatType.Comeback             => 0.70,

            // Near falls hold up best — the whole point is escalation — but still decay.
            BeatType.NearFall             => 0.85,
            BeatType.HighSpot             => 0.78,

            // Feud beats are singular events; repeating them cheapens them fast.
            BeatType.FeudalEscalation     => 0.60,
            BeatType.RevengeSpot          => 0.65,
            BeatType.ThirdPartyPullIn     => 0.55,
            BeatType.AlliesRejected       => 0.50,

            _                             => 0.75
        };

        /// <summary>
        /// Late-match fade. Beyond the sixth beat, tired wrestlers contribute less.
        /// Well-conditioned performers hold up longer, which is what Stamina is for.
        /// </summary>
        private static double FadeFactor(Ctx ctx)
        {
            int beyond = Math.Max(0, ctx.State.BeatIndex - 4);
            if (beyond == 0) return 1.0;

            double conditioning = ctx.Pair(p => p.Conditioning);
            double perBeat = Math.Clamp(0.855 + 0.115 * conditioning, 0.82, 0.99);
            return Math.Pow(perBeat, beyond);
        }

        /// <summary>
        /// Resolves the feud amplification for a beat.
        ///
        /// An explicit FeudalResonance is still the strongest signal, but a feud that has
        /// actually been built now pays off on feud-flavoured beats without the booking
        /// flow having to hand-author a resonance object — previously the multiplier was
        /// unreachable in normal play.
        /// </summary>
        private static double FeudMultiplier(MatchBeat beat, MatchPlan plan, out bool resonanceActive)
        {
            resonanceActive = beat.FeudalResonance?.IsSatisfiedBy(plan.Feud) ?? false;

            var feud = plan.Feud;
            if (feud == null) return 1.0;

            if (resonanceActive)
                return feud.IntensityMultiplier;

            // Implicit resonance: beats that are *about* the rivalry draw on it directly.
            bool feudFlavoured = beat.Type is
                BeatType.FeudalEscalation or BeatType.RevengeSpot or
                BeatType.ThirdPartyPullIn or BeatType.PsychologicalWarfare or
                BeatType.AlliesRejected;

            // At Hot or above the bad blood bleeds into the near-fall drama too.
            bool hotEnoughForNearFalls =
                beat.Type == BeatType.NearFall && feud.Intensity >= FeudIntensity.Hot;

            if (!feudFlavoured && !hotEnoughForNearFalls) return 1.0;

            // Implicit resonance pays 70% of what a hand-authored resonance would.
            return 1.0 + (feud.IntensityMultiplier - 1.0) * 0.70;
        }

        /// <summary>
        /// +1 when momentum should swing toward WrestlerA for this beat, -1 toward WrestlerB.
        ///
        /// Derived from the *resolved* control wrestler rather than the raw enum. Handlers
        /// fall back to WrestlerA when control is Even/Contested, so reading the enum
        /// directly sent the momentum to B while the commentary credited A.
        /// </summary>
        private static int ControlSign(Ctx ctx, Wrestler control) =>
            ReferenceEquals(control, ctx.Plan.WrestlerA) ? 1 : -1;

        // ── Individual beat handlers ─────────────────────────────────────────

        private void ApplyHotOpening(BeatResult r, Ctx ctx, double iMod, double dMod)
        {
            var plan = ctx.Plan;
            double avgRing     = AvgRingSkill(plan);
            double avgCharisma = (plan.WrestlerA.Charisma + plan.WrestlerB.Charisma) / 2.0;

            double connection = ctx.Pair(p => p.Connection);

            // A hot opening is a sprint — it only reads as frantic if they can move.
            double pace = PerformerProfile.Blend(ctx.Pair(p => p.Athleticism), 0.50);

            r.CrowdEnergyDelta      = Rng(8, 14) * iMod * (0.7 + avgCharisma / 5.0 * 0.6) * connection * pace;
            r.AdvantageDelta         = Rng(-5, 5);
            r.TechnicalContribution = 4.0 * (avgRing / 5.0) * iMod * ctx.Pair(p => p.Workrate) * pace;
            r.StorytellingContribution = 2.5 * iMod * PerformerProfile.Blend(connection, 0.6);

            r.Commentary.Add(Pick(
                $"{plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} immediately go at each other before the bell finishes ringing!",
                $"No feeling-out process — the crowd erupts as these two collide from the first second!",
                $"{plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} are at each other's throats right away!",
                $"The bell barely sounds before {plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} are trading shots!",
                $"There will be no feeling out here — these two want each other right now!"
            ));
            r.Commentary.Add(Pick(
                "The pace is frenetic from the opening bell!",
                "Neither wrestler is willing to take a step back.",
                "The energy in the arena is electric — this is must-see television!",
                "The crowd is immediately invested — they came to see exactly this!",
                "Both wrestlers throwing everything at each other from the jump — breathtaking stuff!"
            ));
        }

        private void ApplySlowOpening(BeatResult r, Ctx ctx, double iMod, double dMod)
        {
            var plan = ctx.Plan;
            double avgTech = (plan.WrestlerA.RingSkills.Technical + plan.WrestlerB.RingSkills.Technical) / 2.0;

            // A slow start only works if the crowd trusts these two to go somewhere with it.
            double patience = PerformerProfile.Blend(ctx.Pair(p => p.Connection), 0.7);

            r.CrowdEnergyDelta      = Rng(-2, 4) * iMod * patience;
            r.AdvantageDelta         = Rng(-3, 3);
            r.TechnicalContribution = 5.5 * (avgTech / 5.0) * dMod
                                      * ctx.Pair(p => p.WorkrateFor(WrestlingStyle.Technical))
                                      * PerformerProfile.Blend(ctx.Pair(p => p.RingPsych), 0.7);
            r.StorytellingContribution = 3.0 * dMod * PerformerProfile.Blend(ctx.Pair(p => p.RingPsych), 0.6);

            r.Commentary.Add(Pick(
                $"{plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} circle each other, measuring the distance carefully.",
                $"A deliberate, methodical start as both wrestlers respect each other's ability.",
                $"The feeling-out process begins — neither willing to show their hand too soon.",
                $"{plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} are in no rush — this is going to be a war of attrition.",
                $"Slow, deliberate movements from both competitors — each hunting for an opening."
            ));
            r.Commentary.Add(Pick(
                "Both competitors are playing the long game.",
                "The chess match has begun.",
                "They know this is a marathon, not a sprint.",
                "This crowd is patient — they trust these two to take them somewhere special.",
                "Every movement is calculated. Every step deliberate. Something is being built here."
            ));
        }

        private void ApplyStandardOpening(BeatResult r, Ctx ctx, double iMod, double dMod)
        {
            var plan = ctx.Plan;
            double avgRing = AvgRingSkill(plan);

            r.CrowdEnergyDelta      = Rng(3, 8) * iMod * ctx.Pair(p => p.Connection);
            r.AdvantageDelta         = Rng(-4, 4);
            r.TechnicalContribution = 4.5 * (avgRing / 5.0) * dMod * ctx.Pair(p => p.Workrate);
            r.StorytellingContribution = 2.0 * dMod * PerformerProfile.Blend(ctx.Pair(p => p.RingPsych), 0.5);

            r.Commentary.Add(Pick(
                $"{plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} lock up.",
                $"The match gets under way with both wrestlers testing each other.",
                $"An even start as {plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} feel each other out.",
                $"A collar-and-elbow tie-up to open — both wrestlers gauging what they're dealing with.",
                $"Standard opening exchanges, but the undercurrent of tension is already obvious."
            ));
        }

        private void ApplyHeatSegment(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double dMod)
        {
            control ??= ctx.Plan.WrestlerA;
            other = ctx.Plan.OtherWrestler(control);

            var pControl = ctx.For(control);
            var pOther   = ctx.For(other);

            // Crowd energy: builds tension differently based on who's in control.
            // If the crowd dislikes the controlling wrestler, energy builds (waiting for
            // comeback); if they like them, energy holds or dips slightly.
            double tensionFactor = 1.0 - pControl.Disposition * 0.6; // range 0.4–1.0

            // But the tension only exists if the crowd is invested in the person taking it.
            // Nobody holds their breath for a babyface they have no feelings about.
            double sympathy = PerformerProfile.Blend(pOther.Connection, 0.75);

            r.CrowdEnergyDelta = Rng(3, 9) * tensionFactor * iMod * dMod * sympathy;

            // Advantage: heavy swing toward control. Deliberately smaller than the raw
            // 20–40 this used to be — one beat should not consume half the momentum scale,
            // or no single comeback can ever recover from two of them.
            double swing = Rng(12, 26) * iMod * dMod;
            r.AdvantageDelta = ControlSign(ctx, control) * swing;

            // Technical: use the beat's style hint if set (makes template choice meaningful),
            // otherwise fall back to the wrestler's natural style. The victim's selling is
            // half of what makes a beatdown look good.
            WrestlingStyle beatStyle = beat.StyleHint ?? control.Style;
            double styleSkill = control.RingSkills.GetStyleProficiency(beatStyle);

            // Power offence needs actual size and strength behind it; mat and aerial
            // control does not, so this only applies where the style calls for it.
            double physicality = beatStyle is WrestlingStyle.Powerhouse or WrestlingStyle.Brawler
                ? PerformerProfile.Blend(pControl.Power, 0.55)
                : 1.0;

            r.TechnicalContribution = 6.5 * (styleSkill / 5.0) * iMod * dMod
                                      * pControl.WorkrateFor(beatStyle)
                                      * physicality
                                      * PerformerProfile.Blend(pOther.Selling, 0.55);

            // Storytelling: pacing and control quality — pure ring psychology.
            r.StorytellingContribution = 6.0 * iMod * dMod
                                         * PerformerProfile.Blend(pControl.RingPsych, 0.65)
                                         * PerformerProfile.Blend(pOther.Selling, 0.45);

            r.Commentary.Add(Pick(
                $"{control.RingName} takes control, grounding {other.RingName} with focused, methodical offense.",
                $"{control.RingName} seizes the advantage and begins working over {other.RingName}.",
                $"{control.RingName} takes over, imposing their will on a struggling {other.RingName}.",
                $"{control.RingName} has found a target and is grinding {other.RingName} down with precision.",
                $"The tide has completely turned — {control.RingName} in total command of this match."
            ));
            r.Commentary.Add(Pick(
                $"The crowd watches on as {other.RingName} desperately tries to find a way back in.",
                $"{control.RingName} is in complete command here.",
                $"It's all {control.RingName} right now — {other.RingName} is in serious trouble.",
                $"Every time {other.RingName} tries to mount any resistance, {control.RingName} shuts it down.",
                $"{other.RingName} is being systematically picked apart. Can they find a way back?"
            ));
        }

        private void ApplyComeback(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double dMod)
        {
            control ??= ctx.Plan.WrestlerA;
            other = ctx.Plan.OtherWrestler(control);

            var state    = ctx.State;
            var pControl = ctx.For(control);

            // Bigger comeback pop when the accumulated heat is deeper (more earned).
            // RawAdvantage is uncapped, so two consecutive heat segments produce a larger bonus
            // than one — the clamped Advantage value cannot distinguish between them.
            double advantageDeficit = Math.Abs(state.RawAdvantage);
            double earnedBonus = Math.Min(advantageDeficit / 120.0 * 0.5, 0.5); // up to +50% bonus

            // The pop belongs to the person making the comeback. A crowd that does not care
            // about them does not come alive no matter how well the spot is executed.
            // A comeback is a burst of fast offence — how explosive it looks is athleticism.
            r.CrowdEnergyDelta = Rng(12, 20) * iMod * (1.0 + earnedBonus) * pControl.Connection
                                 * PerformerProfile.Blend(pControl.Athleticism, 0.45);

            // Swing momentum back hard. A comeback's job is to wipe out the heat that came
            // before it, so it recovers a share of the existing deficit on top of its own
            // base swing — otherwise one comeback can never answer two heat segments and
            // the classic face-in-peril structure can never book an earned finish.
            double baseSwing = Rng(25, 45) * iMod * dMod;
            double recoveryShare = 0.55 + 0.30 * (iMod / 1.6);
            int sign = ControlSign(ctx, control);

            // Only claw back a deficit. Booking a comeback for someone already ahead is a
            // booking mistake, not a licence to double their lead — without the sign check
            // the recovery term compounded a favourable momentum instead of reversing an
            // unfavourable one.
            double deficit = sign > 0 ? Math.Max(0, -state.Advantage) : Math.Max(0, state.Advantage);
            double swing = baseSwing + deficit * recoveryShare;

            r.AdvantageDelta = sign * swing;

            r.TechnicalContribution    = 4.5 * (AvgRingSkill(ctx.Plan) / 5.0) * iMod * pControl.Workrate
                                         * PerformerProfile.Blend(pControl.Athleticism, 0.40);
            r.StorytellingContribution = 8.0 * iMod                              // comebacks are prime storytelling
                                         * PerformerProfile.Blend(pControl.Connection, 0.55)
                                         * PerformerProfile.Blend(pControl.RingPsych, 0.35);

            r.Commentary.Add(Pick(
                $"{control.RingName} fires back! The crowd erupts!",
                $"Out of nowhere, {control.RingName} starts fighting back!",
                $"{control.RingName} refuses to stay down — the crowd is on their feet!",
                $"{control.RingName} with a sudden burst of life — they are NOT done yet!",
                $"HERE COMES {control.RingName}! The whole arena just ignited!"
            ));
            r.Commentary.Add(Pick(
                $"Nothing {other.RingName} does can keep {control.RingName} down for long!",
                $"The tide is turning! {control.RingName} is fighting with everything they have!",
                $"A blistering comeback — {other.RingName} is suddenly on the back foot!",
                $"{control.RingName} is hitting everything! This crowd is absolutely electric!",
                $"{other.RingName} can't stop the momentum — {control.RingName} is a house on fire right now!"
            ));
        }

        private void ApplyNearFall(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double feudMult, int timesUsed)
        {
            control ??= ctx.Plan.WrestlerA;
            other = ctx.Plan.OtherWrestler(control);

            var state  = ctx.State;
            var pOther = ctx.For(other);

            // Diminishing returns are handled centrally now, but the commentary still keys
            // off how deep into the near-fall sequence we are.
            double diminish = Math.Pow(0.85, timesUsed - 1);

            // Near falls land harder when crowd energy is already high
            double energyFactor = Math.Max(0.5, state.CrowdEnergy / 80.0);

            // The drama is entirely about whether the crowd believes the other person can
            // survive — that is toughness and selling, on someone they care about.
            double credibility = PerformerProfile.Blend(pOther.Resilience, 0.60)
                                 * PerformerProfile.Blend(pOther.Selling, 0.55)
                                 * PerformerProfile.Blend(pOther.Connection, 0.70);

            r.CrowdEnergyDelta = Rng(9, 16) * iMod * energyFactor * feudMult * credibility;

            // Slight moral momentum to the one who kicked out. Kept small — stacking
            // near-falls in the winner's favour should not sabotage their own finish.
            r.AdvantageDelta = -ControlSign(ctx, control)
                              * Rng(2, 5) * PerformerProfile.Blend(pOther.Resilience, 0.5);

            // Psychology / selling drive near-fall quality
            double avgPsych = (ctx.Plan.WrestlerA.Mental.Psychology + ctx.Plan.WrestlerB.Mental.Psychology) / 2.0;
            r.TechnicalContribution    = 2.5 * (avgPsych / 100.0) * iMod
                                         * PerformerProfile.Blend(pOther.Selling, 0.6);
            r.StorytellingContribution = 5.5 * iMod * feudMult * credibility;

            r.Commentary.Add(Pick(
                $"{control.RingName} covers! One... Two... {other.RingName} kicks out!",
                $"{control.RingName} goes for the pin! The ref counts — {other.RingName} gets the shoulder up!",
                $"Down goes {other.RingName}! The count reaches two — but {other.RingName} refuses to quit!",
                $"COVER! One — Two — NO! {other.RingName} gets a shoulder up at the last possible moment!",
                $"{control.RingName} with the hook of the leg — two count only! {other.RingName} still breathing!"
            ));
            r.Commentary.Add(diminish < 0.6
                ? Pick(
                    $"This crowd cannot believe {other.RingName} is STILL in this!",
                    $"HOW is {other.RingName} alive?! This crowd is losing their minds!",
                    $"{other.RingName} will not die! This is an extraordinary display of resilience!",
                    $"The sheer will of {other.RingName} — they simply refuse to stay down!")
                : Pick(
                    $"So close! The crowd reacts with a gasp.",
                    $"{other.RingName} survives — but for how much longer?",
                    $"A near fall! {control.RingName} thought they had it!",
                    $"Agony for {control.RingName} — they were convinced that was the match.",
                    $"{other.RingName} barely alive — but still in this contest."));
        }

        private void ApplyHighSpot(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, double iMod)
        {
            control ??= ctx.Plan.WrestlerA;
            var pControl = ctx.For(control);

            double flyerSkill = control.RingSkills.HighFlyer;

            // Agility and speed decide whether the spot lands clean or looks laboured.
            double execution = pControl.Athleticism;

            r.CrowdEnergyDelta      = Rng(8, 14) * (0.6 + flyerSkill / 5.0 * 0.8) * iMod
                                      * execution * PerformerProfile.Blend(pControl.Connection, 0.55);
            r.AdvantageDelta         = ControlSign(ctx, control) * Rng(5, 15);
            r.TechnicalContribution = 5.0 * (flyerSkill / 5.0) * iMod
                                      * pControl.WorkrateFor(WrestlingStyle.HighFlyer) * execution;
            r.StorytellingContribution = 3.0 * iMod * PerformerProfile.Blend(pControl.Connection, 0.5);

            r.Commentary.Add(Pick(
                $"{control.RingName} takes flight! A breathtaking high-risk manoeuvre!",
                $"Nobody does it like {control.RingName} — a spectacular aerial attack!",
                $"{control.RingName} launches off the top — the crowd is on their feet!",
                $"Oh my! {control.RingName} with a death-defying aerial assault — incredible athleticism!",
                $"{control.RingName} goes airborne and the crowd loses its mind completely!"
            ));
        }

        private void ApplyRestHold(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, double iMod, double dMod)
        {
            control ??= ctx.Plan.WrestlerA;
            Wrestler other = ctx.Plan.OtherWrestler(control);
            var pControl = ctx.For(control);

            // A rest hold from someone who can hold a crowd is a breather.
            // From someone who cannot, it is when the building starts chanting for something else.
            double drain = Rng(-9, -4) * dMod;
            drain *= 2.0 - PerformerProfile.Blend(pControl.Connection, 0.55); // low connection = steeper drain

            r.CrowdEnergyDelta      = drain;
            r.AdvantageDelta         = ControlSign(ctx, control) * Rng(4, 9);
            r.TechnicalContribution = 1.5 * (control.RingSkills.Technical / 5.0) * dMod
                                      * pControl.WorkrateFor(WrestlingStyle.Technical);
            r.StorytellingContribution = 2.0 * dMod * PerformerProfile.Blend(pControl.RingPsych, 0.7);

            r.Commentary.Add(Pick(
                $"{control.RingName} grounds {other.RingName}, slowing the pace right down.",
                $"A rest hold from {control.RingName} — methodically wearing down {other.RingName}.",
                $"{control.RingName} cinches in a hold, looking to drain {other.RingName}'s energy reserves.",
                $"The pace drops sharply as {control.RingName} locks {other.RingName} in place — smart tactics.",
                $"{control.RingName} is working smart here, conserving energy while keeping {other.RingName} grounded."
            ));
        }

        private void ApplyCrowdBrawl(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, double iMod, double dMod)
        {
            var plan = ctx.Plan;

            // Brawler skill of the controlling wrestler drives energy and technical quality.
            // When control is Even/Contested, use the average of both.
            double brawlerSkill = control != null
                ? control.RingSkills.Brawler
                : (plan.WrestlerA.RingSkills.Brawler + plan.WrestlerB.RingSkills.Brawler) / 2.0;
            double brawlFactor = 0.5 + brawlerSkill / 5.0 * 0.8; // 0.66–1.30

            double connection = control != null ? ctx.For(control).Connection : ctx.Pair(p => p.Connection);
            double workrate   = control != null
                ? ctx.For(control).WorkrateFor(WrestlingStyle.Brawler)
                : ctx.Pair(p => p.WorkrateFor(WrestlingStyle.Brawler));

            r.CrowdEnergyDelta      = Rng(6, 12) * iMod * dMod * brawlFactor
                                      * PerformerProfile.Blend(connection, 0.6);
            r.AdvantageDelta         = Rng(-8, 8);
            r.TechnicalContribution = 3.0 * (brawlerSkill / 5.0) * iMod * workrate;
            r.StorytellingContribution = 4.5 * iMod * dMod * PerformerProfile.Blend(connection, 0.5);

            r.Commentary.Add(Pick(
                $"This match spills out to the floor! The crowd parts as the brawl comes to them!",
                $"{plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} are fighting into the crowd!",
                $"Chaos! These two are taking this war everywhere!",
                $"We have completely lost control — they're brawling through the entire arena!",
                $"The guardrail is not going to contain this one — they're spilling out into the audience!"
            ));
        }

        private void ApplyPsychologicalWarfare(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double feudMult)
        {
            control ??= ctx.Plan.WrestlerA;
            other = ctx.Plan.OtherWrestler(control);

            var pControl = ctx.For(control);

            double psychSkill    = control.Mental.Psychology / 100.0;
            double charismaFactor = control.Charisma / 5.0;

            r.CrowdEnergyDelta = Rng(3, 7) * iMod * feudMult * pControl.Connection;
            r.AdvantageDelta    = ControlSign(ctx, control) * Rng(3, 10);

            r.TechnicalContribution    = 1.5 * psychSkill * iMod * pControl.RingPsych;

            // This is the charisma beat. It should be the single biggest gap between a
            // great talker and someone who cannot hold a room.
            r.StorytellingContribution = 7.0 * charismaFactor * iMod * feudMult
                                         * pControl.Connection
                                         * PerformerProfile.Blend(pControl.RingPsych, 0.4);

            if (feudMult > 1.0)
            {
                r.Commentary.Add(Pick(
                    $"{control.RingName} gets under {other.RingName}'s skin — the crowd reacts viscerally to what that means between these two!",
                    $"A pointed taunt from {control.RingName}! The crowd knows the history here and they explode!",
                    $"{control.RingName} is playing mind games, and given what's between them, it hits differently!",
                    $"{control.RingName} lands a taunt that cuts right to the core of this rivalry — the crowd erupts with recognition!",
                    $"A deeply personal jab from {control.RingName} — you could see it land on {other.RingName}'s face!"
                ));
            }
            else
            {
                r.Commentary.Add(Pick(
                    $"{control.RingName} gets in {other.RingName}'s head with a calculated taunt.",
                    $"The psychological warfare begins — {control.RingName} looking to tilt {other.RingName}.",
                    $"{control.RingName} is doing as much damage mentally as physically right now.",
                    $"{control.RingName} is working the mental game — a calculated attempt to derail {other.RingName}.",
                    $"{other.RingName} doesn't look happy about that at all — {control.RingName} is getting inside their head."
                ));
            }
        }

        private void ApplyFeudalEscalation(BeatResult r, Ctx ctx, double iMod, double feudMult)
        {
            var plan = ctx.Plan;

            // Use feudMult directly (not offset). At Nuclear (×1.5) this peaks higher than
            // RevengeSpot, which is correct — FeudalEscalation should be the match's biggest moment.
            double connection = ctx.Pair(p => p.Connection);

            r.CrowdEnergyDelta = Rng(14, 24) * iMod * feudMult * connection;
            r.AdvantageDelta    = Rng(-5, 5); // contested — both wrestlers go at it
            r.TechnicalContribution    = 2.0 * iMod;
            r.StorytellingContribution = 14.0 * iMod * feudMult
                                         * PerformerProfile.Blend(connection, 0.5);

            r.Commentary.Add(Pick(
                $"This feud reaches a boiling point! {plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} can no longer contain their hatred!",
                $"Everything this feud has been building toward is pouring out right now!",
                $"The bad blood between these two erupts — the crowd is absolutely unhinged!",
                $"The gloves are off! The real hatred between {plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} is on full display!",
                $"This match has just become something completely different — the feud has taken over everything!"
            ));
            r.Commentary.Add(Pick(
                "You can feel months of tension releasing in real time.",
                "This is what personal feuds look like at their peak.",
                "The history between these two is making every second of this feel enormous.",
                "The ringside barriers cannot contain this. The feud has turned this into something primal.",
                "This is no longer just a match. This is personal — and every person in this building feels it."
            ));
        }

        private void ApplyRevengeSpot(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double feudMult)
        {
            control ??= ctx.Plan.WrestlerA;
            other = ctx.Plan.OtherWrestler(control);

            var state    = ctx.State;
            var pControl = ctx.For(control);

            r.CrowdEnergyDelta = Rng(10, 18) * iMod * feudMult * pControl.Connection;

            // A revenge spot is a turning-the-tables moment, so like a comeback it claws
            // back part of the deficit rather than adding a flat swing. Structures such as
            // Grudge Brawl use this instead of a Comeback beat as their pivot, so it has to
            // recover enough for the booked winner to actually earn the finish.
            double baseSwing = Rng(10, 20) * iMod;
            int sign = ControlSign(ctx, control);
            double deficit = sign > 0 ? Math.Max(0, -state.Advantage) : Math.Max(0, state.Advantage);
            r.AdvantageDelta  = sign * (baseSwing + deficit * 0.62);

            r.TechnicalContribution    = 3.0 * iMod * pControl.Workrate;
            r.StorytellingContribution = 10.0 * iMod * feudMult
                                         * PerformerProfile.Blend(pControl.Connection, 0.6);

            r.Commentary.Add(Pick(
                $"{control.RingName} turns the tables — doing to {other.RingName} exactly what was done to them! The crowd erupts in recognition!",
                $"A callback! {control.RingName} uses their own weapon against them — the crowd goes ballistic!",
                $"The symmetry! {control.RingName} gives {other.RingName} a taste of their own medicine!",
                $"{control.RingName} has been waiting for this moment — and the payoff is ENORMOUS!",
                $"Pure catharsis — this crowd has been waiting all match to see {control.RingName} get their hands on {other.RingName} like this!"
            ));
        }

        private void ApplyThirdPartyPullIn(BeatResult r, Ctx ctx, double iMod, double feudMult)
        {
            double connection = ctx.Pair(p => p.Connection);

            r.CrowdEnergyDelta = Rng(10, 16) * iMod * feudMult * PerformerProfile.Blend(connection, 0.7);
            r.AdvantageDelta    = Rng(-10, 10);
            r.TechnicalContribution    = 1.0;
            r.StorytellingContribution = 9.0 * iMod * feudMult * PerformerProfile.Blend(connection, 0.5);

            r.Commentary.Add(Pick(
                "Someone connected to this feud has made their presence known!",
                "A third party has gotten involved — and the crowd reacts in a massive way!",
                "Outside interference from someone tied to this rivalry!",
                "Wait — who is THAT?! A familiar face has just made their presence felt at ringside!",
                "An unexpected arrival! Someone with a stake in this feud has just changed the dynamic completely!"
            ));
        }

        private void ApplyAlliesRejected(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, double iMod)
        {
            control ??= ctx.Plan.WrestlerA;
            var pControl = ctx.For(control);

            // The whole beat is "the crowd loves this person for refusing help".
            // It scales almost entirely with how much the crowd is invested in them.
            double dispControl = pControl.Disposition;

            r.CrowdEnergyDelta         = Rng(10, 20) * iMod * (0.5 + dispControl) * pControl.Connection;
            r.AdvantageDelta            = ControlSign(ctx, control) * Rng(12, 22);
            r.TechnicalContribution    = 1.0;
            r.StorytellingContribution = 10.0 * iMod * (0.5 + dispControl) * pControl.Connection;

            r.Commentary.Add(Pick(
                $"{control.RingName} turns on their own outside help — sending them away! The crowd erupts!",
                $"{control.RingName} wants none of it — waving off their allies! They'll do this ALONE!",
                $"Unbelievable! {control.RingName} fights off their own people! This crowd cannot believe what they're seeing!",
                $"{control.RingName} shoves their own corner away — they are doing this without any help!",
                $"The ally tries to get involved — and {control.RingName} sends them packing! Incredible!"
            ));
            r.Commentary.Add(Pick(
                "This match just changed completely — and the crowd knows it.",
                "A massive statement of intent. Just these two, the way it should be.",
                "The arena is on its feet. Whatever comes next, this just became something else entirely.",
                "The crowd has just witnessed something they won't forget for a long time.",
                $"{control.RingName} choosing honour over an easy win — or is it pure pride? Either way, this crowd respects it."
            ));
        }

        private void ApplyFinish(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double feudMult)
        {
            control ??= ctx.Plan.WrestlerA;
            other = ctx.Plan.OtherWrestler(control);

            var state    = ctx.State;
            var pControl = ctx.For(control);
            var pOther   = ctx.For(other);

            // Was the finish earned? Advantage should favour the winner
            bool advantageFavours = (beat.Control == BeatControl.WrestlerA && state.Advantage > 0)
                                || (beat.Control == BeatControl.WrestlerB && state.Advantage < 0);

            double earnedMultiplier = advantageFavours ? 1.0 : 0.55;

            // The finish only means anything if the crowd is invested in who is winning
            // and believes the loser was beaten.
            double payoff = PerformerProfile.Blend(pControl.Connection, 0.65)
                            * PerformerProfile.Blend(pOther.Selling, 0.40);

            switch (beat.Type)
            {
                case BeatType.FinishSuperFinisher:
                    // Hitting a second finisher on someone is a feat of strength as much
                    // as anything — it should read bigger from someone who can manhandle them.
                    double overkill = PerformerProfile.Blend(pControl.Power, 0.45);
                    r.CrowdEnergyDelta = Rng(16, 26) * iMod * feudMult * payoff * overkill;
                    r.StorytellingContribution = 12.0 * iMod * feudMult * earnedMultiplier * payoff * overkill;
                    r.Commentary.Add(Pick(
                        $"{control.RingName} hits a SECOND finisher! This has to be it!",
                        $"The super finisher! {other.RingName} has nowhere to go!",
                        $"{control.RingName} going deep into their arsenal — there is no coming back from this!",
                        $"A SECOND finishing manoeuvre! {control.RingName} is absolutely ruthless tonight!",
                        $"{control.RingName} is not taking any chances — they hit it again! Cover — count — that's it!"
                    ));
                    break;

                case BeatType.FinishRollup:
                    r.CrowdEnergyDelta = Rng(8, 14) * iMod * payoff;
                    r.StorytellingContribution = 6.0 * iMod * feudMult * payoff;
                    r.Commentary.Add(Pick(
                        $"{control.RingName} rolls up {other.RingName} out of nowhere! One — Two — Three!",
                        $"A surprise roll-up! {control.RingName} steals it!",
                        $"Nobody saw that coming — {control.RingName} with the small package!",
                        $"{control.RingName} with a quick inside cradle — One! Two! Three! The referee's hand hits the mat!",
                        $"An opportunistic roll-up from {control.RingName} — and just like that, this match is over!"
                    ));
                    break;

                case BeatType.FinishSubmission:
                    r.CrowdEnergyDelta = Rng(10, 18) * iMod * feudMult * payoff;
                    r.StorytellingContribution = 9.0 * iMod * feudMult * earnedMultiplier * payoff;
                    r.Commentary.Add(Pick(
                        $"{control.RingName} locks in the submission! {other.RingName} has nowhere to go — they tap!",
                        $"It's locked in! {other.RingName} is trapped — they have to tap out!",
                        $"The hold is applied — {other.RingName} fights it... but they're done! They tap!",
                        $"{control.RingName} sinks it in perfectly — {other.RingName} is going nowhere. The tap comes.",
                        $"{other.RingName} fights with everything they have — but the submission is inescapable. They tap!"
                    ));
                    break;

                case BeatType.FinishDQ:
                    r.CrowdEnergyDelta = Rng(-4, 6) * iMod;
                    r.StorytellingContribution = 4.0 * iMod * feudMult;
                    r.Commentary.Add(Pick(
                        $"{other.RingName} has been disqualified! {control.RingName} wins — but not how they wanted it.",
                        $"A disqualification! The crowd is not happy about how this ended.",
                        $"The referee has no choice — {other.RingName} is DQ'd.",
                        $"The referee finally reaches his limit — {other.RingName} is out of here via disqualification!",
                        $"{other.RingName} pushed too far — they're disqualified, and the crowd lets them know it."
                    ));
                    break;

                case BeatType.FinishCountout:
                    r.CrowdEnergyDelta = Rng(-6, 4) * iMod;
                    r.StorytellingContribution = 3.0 * iMod * feudMult;
                    r.Commentary.Add(Pick(
                        $"{other.RingName} cannot beat the count! {control.RingName} wins by count-out — and nobody is happy.",
                        $"The referee reaches ten! {other.RingName} is counted out — a hollow result.",
                        $"Count-out! {other.RingName} can't make it back in time. The crowd voices its displeasure.",
                        $"The count reaches ten and {other.RingName} is still on the floor — count-out. Nobody feels satisfied.",
                        $"{other.RingName} counted out — a frustrating, anticlimactic end to what had been a compelling match."
                    ));
                    break;

                case BeatType.FinishInterference:
                    r.CrowdEnergyDelta = Rng(4, 12) * iMod * feudMult;
                    r.StorytellingContribution = 7.0 * iMod * feudMult;
                    r.Commentary.Add(Pick(
                        $"Outside interference changes everything! {control.RingName} capitalises to take the win!",
                        $"This one is decided by outside forces — and {control.RingName} takes advantage!",
                        $"Controversy! Someone gets involved and {control.RingName} benefits!",
                        $"The match is decided by an outside party — and {control.RingName} is in the right place at the right time!",
                        $"We have interference! {control.RingName} uses the distraction to seal this one!"
                    ));
                    break;

                default: // FinishClean
                    r.CrowdEnergyDelta = Rng(10, 18) * iMod * feudMult * earnedMultiplier * payoff;
                    r.StorytellingContribution = 8.0 * iMod * feudMult * earnedMultiplier * payoff;
                    r.Commentary.Add(Pick(
                        $"{control.RingName} hits the finisher and covers! One... Two... Three! It's over!",
                        $"The finishing blow lands! {control.RingName} gets the three count!",
                        $"{other.RingName} goes down — and this time they're not getting up! {control.RingName} wins!",
                        $"{control.RingName} with the definitive exclamation point — the cover, the count, and it's done!",
                        $"Clean as a whistle — {control.RingName} with a beautiful finish to seal a hard-fought victory!"
                    ));
                    break;
            }

            // Shared finish contributions
            double styleSkill = control.RingSkills.GetStyleProficiency(control.Style);
            r.TechnicalContribution = 5.5 * (styleSkill / 5.0) * iMod * earnedMultiplier
                                      * pControl.Workrate
                                      * PerformerProfile.Blend(pOther.Selling, 0.5);

            // Final momentum swing in winner's direction
            r.AdvantageDelta = ControlSign(ctx, control) * 30;

            // Record finish quality (used in final rating)
            state.FinishQuality = Math.Clamp(
                (earnedMultiplier * 80) + (state.CrowdEnergy * 0.2),
                0, 100);

            r.Commentary.Add(advantageFavours
                ? $"A fitting end — {control.RingName} earned that victory."
                : $"A controversial finish — did {control.RingName} really deserve that outcome?");
        }

        // ── Final rating ─────────────────────────────────────────────────────

        private MatchEngineResult BuildResult(Ctx ctx, List<BeatResult> beatResults)
        {
            var plan  = ctx.Plan;
            var state = ctx.State;

            var (techWeight, storyWeight, crowdWeight) = WeightsFor(plan.MatchType);

            // Saturating normalisation. Unlike a hard clamp this has no cliff: piling on
            // beats yields ever-smaller returns and can never reach the full component.
            double techComponent  = Saturate(state.TechnicalScore, TechScale)     * 100 * techWeight;
            double storyComponent = Saturate(state.StorytellingScore, StoryScale) * 100 * storyWeight;

            // Crowd is normalised onto the same 0–1 footing as the other two. A raw reading
            // of 30 is a dead building and 95 is as loud as it gets; mapping that band to
            // the full range stops crowd from being both the largest component and the
            // least discriminating, and lets the match-type weights actually mean something.
            double crowdRaw  = (state.CrowdPeakEnergy * 0.4) + (state.CrowdAverage * 0.6);
            double crowdNorm = Math.Clamp((crowdRaw - CrowdFloor) / (CrowdCeilingRef - CrowdFloor), 0, 1);
            double crowdComponent = crowdNorm * 100 * crowdWeight;

            // Finish quality nudges the final score (±10 points), so an unearned finish
            // costs around half a star.
            double finishNudge = (state.FinishQuality - 50.0) / 100.0 * 20.0;

            // Varied booking is worth something in itself; a one-note match is not a
            // great match no matter how many beats it has.
            double varietyNudge = VarietyNudge(state, plan);

            // Does the beat mix deliver the match type the booker declared?
            //
            // Standard is the neutral choice — it promises nothing specific, so it neither
            // earns nor loses anything here. Declaring a specialised type is a bet: book a
            // plan that delivers it and you are paid, book a brawl and call it a technical
            // classic and you are not.
            double coherence = TypeCoherence(plan);
            double coherenceNudge = plan.MatchType == MatchTypeEnum.Standard
                ? 0.0
                : Math.Clamp((coherence - 0.55) * 16.0, -8.0, 8.0);

            double finalScore = Math.Clamp(
                techComponent + storyComponent + crowdComponent + finishNudge + varietyNudge + coherenceNudge,
                0, 100);

            double starRating = Math.Clamp(finalScore / 20.0, 0, 5);

            return new MatchEngineResult
            {
                Winner             = plan.BookedWinner!,
                Loser              = plan.BookedLoser!,
                BeatResults        = beatResults,
                TechnicalScore     = state.TechnicalScore,
                StorytellingScore  = state.StorytellingScore,
                CrowdPeakEnergy    = state.CrowdPeakEnergy,
                CrowdAverageEnergy = state.CrowdAverage,
                FinishQuality      = state.FinishQuality,
                MatchTypeCoherence = coherence,
                FinalScore         = finalScore,
                StarRating         = starRating
            };
        }

        /// <summary>x / (x + scale) style saturation, expressed as 1 - e^(-x/scale).</summary>
        private static double Saturate(double raw, double scale) =>
            raw <= 0 ? 0 : 1.0 - Math.Exp(-raw / scale);

        /// <summary>
        /// Component weights per match type. Declaring a match type now genuinely changes
        /// what the engine is grading, instead of being ignored entirely.
        /// </summary>
        private static (double tech, double story, double crowd) WeightsFor(MatchTypeEnum type) => type switch
        {
            MatchTypeEnum.Technical    => (0.46, 0.24, 0.30),
            MatchTypeEnum.Storytelling => (0.24, 0.42, 0.34),
            MatchTypeEnum.Spotfest     => (0.30, 0.22, 0.48),
            _                          => (0.35, 0.30, 0.35)  // Standard
        };

        /// <summary>
        /// Beat types that suit each declared match type. Used to reward a booker whose
        /// plan actually delivers what they advertised, and to penalise one who declares
        /// a technical classic and books a brawl.
        /// </summary>
        /// Every set names at least one opening and one finish. Openings and finishes are
        /// mandatory in any plan, so a type whose set omitted them could never reach full
        /// coherence however well it was booked — only Technical had both, which quietly
        /// made it the best-paying declaration on almost any plan.
        private static bool IsOnType(BeatType t, MatchTypeEnum type) => type switch
        {
            // Mat wrestling, limb work and a submission payoff.
            MatchTypeEnum.Technical => t is BeatType.SlowOpening or BeatType.StandardOpening
                or BeatType.HeatSegment or BeatType.RestHold or BeatType.NearFall
                or BeatType.FinishSubmission or BeatType.FinishClean,

            // Spectacle: get them early, keep them loud, finish emphatically.
            MatchTypeEnum.Spotfest => t is BeatType.HotOpening or BeatType.StandardOpening
                or BeatType.HighSpot or BeatType.CrowdBrawl or BeatType.NearFall or BeatType.Comeback
                or BeatType.FinishClean or BeatType.FinishSuperFinisher or BeatType.FinishRollup,

            // Character and grudge work, including the finishes that leave a story running.
            MatchTypeEnum.Storytelling => t is BeatType.SlowOpening or BeatType.StandardOpening
                or BeatType.PsychologicalWarfare or BeatType.RevengeSpot
                or BeatType.FeudalEscalation or BeatType.ThirdPartyPullIn or BeatType.AlliesRejected
                or BeatType.Comeback or BeatType.NearFall
                or BeatType.FinishClean or BeatType.FinishInterference or BeatType.FinishDQ,

            _ => true // Standard has no preference — anything is on-type
        };

        /// <summary>Fraction of the plan's beats that suit the declared match type (0–1).</summary>
        private static double TypeCoherence(MatchPlan plan)
        {
            if (plan.MatchType == MatchTypeEnum.Standard) return 1.0;
            if (plan.Beats.Count == 0) return 0.0;
            return plan.Beats.Count(b => IsOnType(b.Type, plan.MatchType)) / (double)plan.Beats.Count;
        }

        /// <summary>
        /// Rewards a plan that uses a range of beat types and penalises one that repeats
        /// itself. Expressed in final-score points (roughly −5 to +5).
        /// </summary>
        private static double VarietyNudge(MatchEngineState state, MatchPlan plan)
        {
            int beats = plan.Beats.Count;
            if (beats <= 2) return 0;

            // How close the plan came to using a distinct type for every beat.
            double variety = state.DistinctBeatTypes / (double)beats;

            // A four-beat match using four types is fully varied; a twelve-beat match using
            // four types is repetitive. Centre on 0.6 so normal structures sit near zero.
            return Math.Clamp((variety - 0.6) * 12.0, -5.0, 5.0);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private double AvgRingSkill(MatchPlan plan) =>
            (plan.WrestlerA.RingSkills.GetOverallSkill() + plan.WrestlerB.RingSkills.GetOverallSkill()) / 2.0;

        private double Rng(double min, double max) =>
            min + _rand.NextDouble() * (max - min);

        private string Pick(params string[] options) =>
            options[_rand.Next(options.Length)];
    }
}
