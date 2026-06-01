using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Engine
{
    public class MatchEngine
    {
        private readonly Random _rand;

        // Normalisation ceilings for final rating formula
        private const double MaxTechnical    = 60.0;
        private const double MaxStorytelling = 80.0;

        // Weight of each component in the 0–100 final score
        private const double TechWeight    = 0.35;
        private const double StoryWeight   = 0.30;
        private const double CrowdWeight   = 0.35;

        public MatchEngine(int? seed = null)
        {
            _rand = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        }

        // ── Public entry point ───────────────────────────────────────────────

        public MatchEngineResult Execute(MatchPlan plan)
        {
            var errors = plan.Validate();
            if (errors.Any())
                throw new InvalidOperationException(
                    "Invalid match plan:\n" + string.Join("\n", errors.Select(e => "  • " + e)));

            var state = InitialiseState(plan);
            var beatResults = new List<BeatResult>();

            foreach (var beat in plan.Beats)
            {
                var result = ExecuteBeat(beat, state, plan);
                beatResults.Add(result);

                // Natural energy decay between beats (except after the finish)
                if (!beat.IsFinish)
                    state.ApplyDecay();
            }

            return BuildResult(plan, state, beatResults);
        }

        // ── State initialisation ─────────────────────────────────────────────

        private MatchEngineState InitialiseState(MatchPlan plan)
        {
            double avgPop = (plan.WrestlerA.Popularity + plan.WrestlerB.Popularity) / 2.0;

            // Crowd disposition modifier: rewards having BOTH wrestlers over, not just one.
            // Using Min rather than average means one nobody eliminates the bonus —
            // the crowd doesn't start hot just because one star is in the match.
            double dispA = CrowdDisposition(plan.WrestlerA);
            double dispB = CrowdDisposition(plan.WrestlerB);
            double bothOverBonus = Math.Min(dispA, dispB) * 8.0; // up to +8

            double baseEnergy = (avgPop / 100.0) * 65.0 + bothOverBonus;
            double feudBonus  = plan.Feud?.StartingEnergyBonus ?? 0;

            var state = new MatchEngineState
            {
                CrowdEnergy = Math.Clamp(baseEnergy + feudBonus, 10, 90),
                Momentum    = 0
            };

            state.RecordEnergy();

            return state;
        }

        // ── Beat dispatch ────────────────────────────────────────────────────

        private BeatResult ExecuteBeat(MatchBeat beat, MatchEngineState state, MatchPlan plan)
        {
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

            // Feud resonance: check before computing contributions
            bool resonanceActive = beat.FeudalResonance?.IsSatisfiedBy(plan.Feud) ?? false;
            result.FeudalResonanceActivated = resonanceActive;
            double feudMult = resonanceActive ? plan.Feud!.IntensityMultiplier : 1.0;

            switch (beat.Type)
            {
                case BeatType.HotOpening:
                    ApplyHotOpening(result, beat, state, plan, iMod, dMod);
                    break;

                case BeatType.SlowOpening:
                    ApplySlowOpening(result, beat, state, plan, iMod, dMod);
                    break;

                case BeatType.StandardOpening:
                    ApplyStandardOpening(result, beat, state, plan, iMod, dMod);
                    break;

                case BeatType.HeatSegment:
                    ApplyHeatSegment(result, beat, state, plan, control, other, iMod, dMod);
                    break;

                case BeatType.Comeback:
                    ApplyComeback(result, beat, state, plan, control, other, iMod, dMod);
                    break;

                case BeatType.NearFall:
                    ApplyNearFall(result, beat, state, plan, control, other, iMod, feudMult);
                    break;

                case BeatType.HighSpot:
                    ApplyHighSpot(result, beat, state, plan, control, iMod);
                    break;

                case BeatType.RestHold:
                    ApplyRestHold(result, beat, state, plan, control, iMod, dMod);
                    break;

                case BeatType.CrowdBrawl:
                    ApplyCrowdBrawl(result, beat, state, plan, control, iMod, dMod);
                    break;

                case BeatType.PsychologicalWarfare:
                    ApplyPsychologicalWarfare(result, beat, state, plan, control, other, iMod, feudMult);
                    break;

                case BeatType.FeudalEscalation:
                    ApplyFeudalEscalation(result, beat, state, plan, iMod, feudMult);
                    break;

                case BeatType.RevengeSpot:
                    ApplyRevengeSpot(result, beat, state, plan, control, other, iMod, feudMult);
                    break;

                case BeatType.ThirdPartyPullIn:
                    ApplyThirdPartyPullIn(result, beat, state, plan, iMod, feudMult);
                    break;

                case BeatType.AlliesRejected:
                    ApplyAlliesRejected(result, beat, state, plan, control, iMod);
                    break;

                case BeatType.FinishClean:
                case BeatType.FinishRollup:
                case BeatType.FinishSubmission:
                case BeatType.FinishDQ:
                case BeatType.FinishCountout:
                case BeatType.FinishInterference:
                case BeatType.FinishSuperFinisher:
                    ApplyFinish(result, beat, state, plan, control, other, iMod, feudMult);
                    break;
            }

            // Commit deltas to state
            state.ApplyEnergy(result.CrowdEnergyDelta);
            state.ApplyMomentum(result.MomentumDelta);
            state.TechnicalScore    += result.TechnicalContribution;
            state.StorytellingScore += result.StorytellingContribution;

            // Snapshot state into result
            result.CrowdEnergyAfter     = state.CrowdEnergy;
            result.MomentumAfter        = state.Momentum;
            result.TechnicalScoreAfter  = state.TechnicalScore;
            result.StorytellingScoreAfter = state.StorytellingScore;

            return result;
        }

        // ── Individual beat handlers ─────────────────────────────────────────

        private void ApplyHotOpening(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, double iMod, double dMod)
        {
            double avgRing    = AvgRingSkill(plan);
            double avgCharisma = (plan.WrestlerA.Charisma + plan.WrestlerB.Charisma) / 2.0;

            r.CrowdEnergyDelta      = Rng(8, 14) * iMod * (0.7 + avgCharisma / 5.0 * 0.6);
            r.MomentumDelta         = Rng(-5, 5);
            r.TechnicalContribution = 4.0 * (avgRing / 5.0) * iMod;
            r.StorytellingContribution = 2.5 * iMod;

            r.Commentary.Add(Pick(
                $"{plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} immediately go at each other before the bell finishes ringing!",
                $"No feeling-out process — the crowd erupts as these two collide from the first second!",
                $"{plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} are at each other's throats right away!"
            ));
            r.Commentary.Add(Pick(
                "The pace is frenetic from the opening bell!",
                "Neither wrestler is willing to take a step back.",
                "The energy in the arena is electric — this is must-see television!"
            ));
        }

        private void ApplySlowOpening(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, double iMod, double dMod)
        {
            double avgTech = (plan.WrestlerA.RingSkills.Technical + plan.WrestlerB.RingSkills.Technical) / 2.0;

            r.CrowdEnergyDelta      = Rng(-2, 4) * iMod;
            r.MomentumDelta         = Rng(-3, 3);
            r.TechnicalContribution = 5.5 * (avgTech / 5.0) * dMod;
            r.StorytellingContribution = 3.0 * dMod;

            r.Commentary.Add(Pick(
                $"{plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} circle each other, measuring the distance carefully.",
                $"A deliberate, methodical start as both wrestlers respect each other's ability.",
                $"The feeling-out process begins — neither willing to show their hand too soon."
            ));
            r.Commentary.Add(Pick(
                "Both competitors are playing the long game.",
                "The chess match has begun.",
                "They know this is a marathon, not a sprint."
            ));
        }

        private void ApplyStandardOpening(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, double iMod, double dMod)
        {
            double avgRing = AvgRingSkill(plan);

            r.CrowdEnergyDelta      = Rng(3, 8) * iMod;
            r.MomentumDelta         = Rng(-4, 4);
            r.TechnicalContribution = 4.5 * (avgRing / 5.0) * dMod;
            r.StorytellingContribution = 2.0 * dMod;

            r.Commentary.Add(Pick(
                $"{plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} lock up.",
                $"The match gets under way with both wrestlers testing each other.",
                $"An even start as {plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} feel each other out."
            ));
        }

        private void ApplyHeatSegment(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, Wrestler? control, Wrestler other, double iMod, double dMod)
        {
            if (control == null) { control = plan.WrestlerA; }

            // Crowd energy: builds tension differently based on who's in control
            double dispControl = CrowdDisposition(control);
            // If the crowd dislikes the controlling wrestler, energy builds (waiting for comeback)
            // If the crowd likes them, energy holds or dips slightly
            double tensionFactor = 1.0 - dispControl * 0.6; // range 0.4–1.0
            r.CrowdEnergyDelta = Rng(3, 9) * tensionFactor * iMod * dMod;

            // Momentum: heavy swing toward control
            double momSwing = Rng(20, 40) * iMod * dMod;
            r.MomentumDelta = (beat.Control == BeatControl.WrestlerA ? 1 : -1) * momSwing;

            // Technical: use the beat's style hint if set (makes template choice meaningful),
            // otherwise fall back to the wrestler's natural style.
            WrestlingStyle beatStyle = beat.StyleHint ?? control.Style;
            double styleSkill = control.RingSkills.GetStyleProficiency(beatStyle);
            r.TechnicalContribution = 6.5 * (styleSkill / 5.0) * iMod * dMod;

            // Storytelling: pacing and control quality
            r.StorytellingContribution = 6.0 * iMod * dMod;

            r.Commentary.Add(Pick(
                $"{control.RingName} takes control, grounding {other.RingName} with focused, methodical offense.",
                $"{control.RingName} seizes the advantage and begins working over {other.RingName}.",
                $"{control.RingName} takes over, imposing their will on a struggling {other.RingName}."
            ));
            r.Commentary.Add(Pick(
                $"The crowd watches on as {other.RingName} desperately tries to find a way back in.",
                $"{control.RingName} is in complete command here.",
                $"It's all {control.RingName} right now — {other.RingName} is in serious trouble."
            ));
        }

        private void ApplyComeback(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, Wrestler? control, Wrestler other, double iMod, double dMod)
        {
            if (control == null) { control = plan.WrestlerA; }

            // Bigger comeback pop when the accumulated heat is deeper (more earned).
            // RawMomentum is uncapped, so two consecutive heat segments produce a larger bonus
            // than one — the clamped Momentum value cannot distinguish between them.
            double momentumDeficit = Math.Abs(state.RawMomentum);
            double earnedBonus = Math.Min(momentumDeficit / 200.0 * 0.5, 0.5); // up to +50% bonus

            r.CrowdEnergyDelta = Rng(12, 20) * iMod * (1.0 + earnedBonus);

            // Swing momentum back hard
            double momSwing = Rng(25, 45) * iMod;
            r.MomentumDelta = (beat.Control == BeatControl.WrestlerA ? 1 : -1) * momSwing;

            r.TechnicalContribution    = 4.5 * (AvgRingSkill(plan) / 5.0) * iMod;
            r.StorytellingContribution = 8.0 * iMod; // comebacks are prime storytelling

            r.Commentary.Add(Pick(
                $"{control.RingName} fires back! The crowd erupts!",
                $"Out of nowhere, {control.RingName} starts fighting back!",
                $"{control.RingName} refuses to stay down — the crowd is on their feet!"
            ));
            r.Commentary.Add(Pick(
                $"Nothing {other.RingName} does can keep {control.RingName} down for long!",
                $"The tide is turning! {control.RingName} is fighting with everything they have!",
                $"A blistering comeback — {other.RingName} is suddenly on the back foot!"
            ));
        }

        private void ApplyNearFall(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, Wrestler? control, Wrestler other, double iMod, double feudMult)
        {
            if (control == null) { control = plan.WrestlerA; }

            state.NearFallCount++;

            // Diminishing returns: each subsequent near fall is worth 85% of the previous
            double diminish = Math.Pow(0.85, state.NearFallCount - 1);

            // Near falls land harder when crowd energy is already high
            double energyFactor = Math.Max(0.5, state.CrowdEnergy / 80.0);

            r.CrowdEnergyDelta = Rng(9, 16) * iMod * diminish * energyFactor * feudMult;

            // Slight moral momentum to the one who kicked out
            r.MomentumDelta = (beat.Control == BeatControl.WrestlerA ? -1 : 1) * Rng(3, 8);

            // Psychology / selling drive near-fall quality
            double avgPsych = (plan.WrestlerA.Mental.Psychology + plan.WrestlerB.Mental.Psychology) / 2.0;
            r.TechnicalContribution    = 2.5 * (avgPsych / 100.0) * iMod * diminish;
            r.StorytellingContribution = 5.5 * iMod * diminish * feudMult;

            r.Commentary.Add(Pick(
                $"{control.RingName} covers! One... Two... {other.RingName} kicks out!",
                $"{control.RingName} goes for the pin! The ref counts — {other.RingName} gets the shoulder up!",
                $"Down goes {other.RingName}! The count reaches two — but {other.RingName} refuses to quit!"
            ));
            r.Commentary.Add(diminish < 0.6
                ? Pick(
                    $"This crowd cannot believe {other.RingName} is STILL in this!",
                    $"HOW is {other.RingName} alive?! This crowd is losing their minds!")
                : Pick(
                    $"So close! The crowd reacts with a gasp.",
                    $"{other.RingName} survives — but for how much longer?",
                    $"A near fall! {control.RingName} thought they had it!"));
        }

        private void ApplyHighSpot(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, Wrestler? control, double iMod)
        {
            if (control == null) { control = plan.WrestlerA; }

            double flyerSkill = control.RingSkills.HighFlyer;
            r.CrowdEnergyDelta      = Rng(8, 14) * (0.6 + flyerSkill / 5.0 * 0.8) * iMod;
            r.MomentumDelta         = (beat.Control == BeatControl.WrestlerA ? 1 : -1) * Rng(5, 15);
            r.TechnicalContribution = 5.0 * (flyerSkill / 5.0) * iMod;
            r.StorytellingContribution = 3.0 * iMod;

            r.Commentary.Add(Pick(
                $"{control.RingName} takes flight! A breathtaking high-risk manoeuvre!",
                $"Nobody does it like {control.RingName} — a spectacular aerial attack!",
                $"{control.RingName} launches off the top — the crowd is on their feet!"
            ));
        }

        private void ApplyRestHold(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, Wrestler? control, double iMod, double dMod)
        {
            if (control == null) { control = plan.WrestlerA; }
            Wrestler other = plan.OtherWrestler(control);

            r.CrowdEnergyDelta      = Rng(-9, -4) * dMod;   // crowd cools
            r.MomentumDelta         = (beat.Control == BeatControl.WrestlerA ? 1 : -1) * Rng(5, 12);
            r.TechnicalContribution = 1.5 * (control.RingSkills.Technical / 5.0) * dMod;
            r.StorytellingContribution = 2.0 * dMod;

            r.Commentary.Add(Pick(
                $"{control.RingName} grounds {other.RingName}, slowing the pace right down.",
                $"A rest hold from {control.RingName} — methodically wearing down {other.RingName}.",
                $"{control.RingName} cinches in a hold, looking to drain {other.RingName}'s energy reserves."
            ));
        }

        private void ApplyCrowdBrawl(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, Wrestler? control, double iMod, double dMod)
        {
            // Brawler skill of the controlling wrestler drives energy and technical quality.
            // When control is Even/Contested, use the average of both.
            double brawlerSkill = control != null
                ? control.RingSkills.Brawler
                : (plan.WrestlerA.RingSkills.Brawler + plan.WrestlerB.RingSkills.Brawler) / 2.0;
            double brawlFactor = 0.5 + brawlerSkill / 5.0 * 0.8; // 0.66–1.30

            r.CrowdEnergyDelta      = Rng(6, 12) * iMod * dMod * brawlFactor;
            r.MomentumDelta         = Rng(-8, 8);
            r.TechnicalContribution = 3.0 * (brawlerSkill / 5.0) * iMod;
            r.StorytellingContribution = 4.5 * iMod * dMod;

            r.Commentary.Add(Pick(
                $"This match spills out to the floor! The crowd parts as the brawl comes to them!",
                $"{plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} are fighting into the crowd!",
                $"Chaos! These two are taking this war everywhere!"
            ));
        }

        private void ApplyPsychologicalWarfare(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, Wrestler? control, Wrestler other, double iMod, double feudMult)
        {
            if (control == null) { control = plan.WrestlerA; }

            double psychSkill = control.Mental.Psychology / 100.0;
            double charismaFactor = control.Charisma / 5.0;

            r.CrowdEnergyDelta = Rng(3, 7) * iMod * feudMult;
            r.MomentumDelta    = (beat.Control == BeatControl.WrestlerA ? 1 : -1) * Rng(3, 10);

            r.TechnicalContribution    = 1.5 * psychSkill * iMod;
            r.StorytellingContribution = 7.0 * charismaFactor * iMod * feudMult;

            if (feudMult > 1.0)
            {
                r.Commentary.Add(Pick(
                    $"{control.RingName} gets under {other.RingName}'s skin — the crowd reacts viscerally to what that means between these two!",
                    $"A pointed taunt from {control.RingName}! The crowd knows the history here and they explode!",
                    $"{control.RingName} is playing mind games, and given what's between them, it hits differently!"
                ));
            }
            else
            {
                r.Commentary.Add(Pick(
                    $"{control.RingName} gets in {other.RingName}'s head with a calculated taunt.",
                    $"The psychological warfare begins — {control.RingName} looking to tilt {other.RingName}.",
                    $"{control.RingName} is doing as much damage mentally as physically right now."
                ));
            }
        }

        private void ApplyFeudalEscalation(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, double iMod, double feudMult)
        {
            // Use feudMult directly (not offset). At Nuclear (×1.5) this peaks higher than
            // RevengeSpot, which is correct — FeudalEscalation should be the match's biggest moment.
            r.CrowdEnergyDelta = Rng(14, 24) * iMod * feudMult;
            r.MomentumDelta    = Rng(-5, 5); // contested — both wrestlers go at it
            r.TechnicalContribution    = 2.0 * iMod;
            r.StorytellingContribution = 14.0 * iMod * feudMult;

            r.Commentary.Add(Pick(
                $"This feud reaches a boiling point! {plan.WrestlerA.RingName} and {plan.WrestlerB.RingName} can no longer contain their hatred!",
                $"Everything this feud has been building toward is pouring out right now!",
                $"The bad blood between these two erupts — the crowd is absolutely unhinged!"
            ));
            r.Commentary.Add(Pick(
                "You can feel months of tension releasing in real time.",
                "This is what personal feuds look like at their peak.",
                "The history between these two is making every second of this feel enormous."
            ));
        }

        private void ApplyRevengeSpot(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, Wrestler? control, Wrestler other, double iMod, double feudMult)
        {
            if (control == null) { control = plan.WrestlerA; }

            r.CrowdEnergyDelta = Rng(10, 18) * iMod * feudMult;
            r.MomentumDelta    = (beat.Control == BeatControl.WrestlerA ? 1 : -1) * Rng(10, 20);
            r.TechnicalContribution    = 3.0 * iMod;
            r.StorytellingContribution = 10.0 * iMod * feudMult;

            r.Commentary.Add(Pick(
                $"{control.RingName} turns the tables — doing to {other.RingName} exactly what was done to them! The crowd erupts in recognition!",
                $"A callback! {control.RingName} uses their own weapon against them — the crowd goes ballistic!",
                $"The symmetry! {control.RingName} gives {other.RingName} a taste of their own medicine!"
            ));
        }

        private void ApplyThirdPartyPullIn(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, double iMod, double feudMult)
        {
            r.CrowdEnergyDelta = Rng(10, 16) * iMod * feudMult;
            r.MomentumDelta    = Rng(-10, 10);
            r.TechnicalContribution    = 1.0;
            r.StorytellingContribution = 9.0 * iMod * feudMult;

            r.Commentary.Add(Pick(
                "Someone connected to this feud has made their presence known!",
                "A third party has gotten involved — and the crowd reacts in a massive way!",
                "Outside interference from someone tied to this rivalry!"
            ));
        }

        private void ApplyAlliesRejected(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, Wrestler? control, double iMod)
        {
            if (control == null) { control = plan.WrestlerA; }
            double dispControl = CrowdDisposition(control);

            r.CrowdEnergyDelta         = Rng(10, 20) * iMod * (0.5 + dispControl);
            r.MomentumDelta            = (beat.Control == BeatControl.WrestlerA ? 1 : -1) * Rng(12, 22);
            r.TechnicalContribution    = 1.0;
            r.StorytellingContribution = 10.0 * iMod * (0.5 + dispControl);

            r.Commentary.Add(Pick(
                $"{control.RingName} turns on their own outside help — sending them away! The crowd erupts!",
                $"{control.RingName} wants none of it — waving off their allies! They'll do this ALONE!",
                $"Unbelievable! {control.RingName} fights off their own people! This crowd cannot believe what they're seeing!"
            ));
            r.Commentary.Add(Pick(
                "This match just changed completely — and the crowd knows it.",
                "A massive statement of intent. Just these two, the way it should be.",
                "The arena is on its feet. Whatever comes next, this just became something else entirely."
            ));
        }

        private void ApplyFinish(BeatResult r, MatchBeat beat, MatchEngineState state,
            MatchPlan plan, Wrestler? control, Wrestler other, double iMod, double feudMult)
        {
            if (control == null) { control = plan.WrestlerA; }

            // Was the finish earned? Momentum should favour the winner
            bool momentumFavours = (beat.Control == BeatControl.WrestlerA && state.Momentum > 0)
                                || (beat.Control == BeatControl.WrestlerB && state.Momentum < 0);

            double earnedMultiplier = momentumFavours ? 1.0 : 0.55;

            switch (beat.Type)
            {
                case BeatType.FinishSuperFinisher:
                    r.CrowdEnergyDelta = Rng(16, 26) * iMod * feudMult;
                    r.StorytellingContribution = 12.0 * iMod * feudMult * earnedMultiplier;
                    r.Commentary.Add(Pick(
                        $"{control.RingName} hits a SECOND finisher! This has to be it!",
                        $"The super finisher! {other.RingName} has nowhere to go!",
                        $"{control.RingName} going deep into their arsenal — there is no coming back from this!"
                    ));
                    break;

                case BeatType.FinishRollup:
                    r.CrowdEnergyDelta = Rng(8, 14) * iMod;
                    r.StorytellingContribution = 6.0 * iMod * feudMult;
                    r.Commentary.Add(Pick(
                        $"{control.RingName} rolls up {other.RingName} out of nowhere! One — Two — Three!",
                        $"A surprise roll-up! {control.RingName} steals it!",
                        $"Nobody saw that coming — {control.RingName} with the small package!"
                    ));
                    break;

                case BeatType.FinishSubmission:
                    r.CrowdEnergyDelta = Rng(10, 18) * iMod * feudMult;
                    r.StorytellingContribution = 9.0 * iMod * feudMult * earnedMultiplier;
                    r.Commentary.Add(Pick(
                        $"{control.RingName} locks in the submission! {other.RingName} has nowhere to go — they tap!",
                        $"It's locked in! {other.RingName} is trapped — they have to tap out!",
                        $"The hold is applied — {other.RingName} fights it... but they're done! They tap!"
                    ));
                    break;

                case BeatType.FinishDQ:
                    r.CrowdEnergyDelta = Rng(-4, 6) * iMod;
                    r.StorytellingContribution = 4.0 * iMod * feudMult;
                    r.Commentary.Add(Pick(
                        $"{other.RingName} has been disqualified! {control.RingName} wins — but not how they wanted it.",
                        $"A disqualification! The crowd is not happy about how this ended.",
                        $"The referee has no choice — {other.RingName} is DQ'd."
                    ));
                    break;

                case BeatType.FinishCountout:
                    r.CrowdEnergyDelta = Rng(-6, 4) * iMod;
                    r.StorytellingContribution = 3.0 * iMod * feudMult;
                    r.Commentary.Add(Pick(
                        $"{other.RingName} cannot beat the count! {control.RingName} wins by count-out — and nobody is happy.",
                        $"The referee reaches ten! {other.RingName} is counted out — a hollow result.",
                        $"Count-out! {other.RingName} can't make it back in time. The crowd voices its displeasure."
                    ));
                    break;

                case BeatType.FinishInterference:
                    r.CrowdEnergyDelta = Rng(4, 12) * iMod * feudMult;
                    r.StorytellingContribution = 7.0 * iMod * feudMult;
                    r.Commentary.Add(Pick(
                        $"Outside interference changes everything! {control.RingName} capitalises to take the win!",
                        $"This one is decided by outside forces — and {control.RingName} takes advantage!",
                        $"Controversy! Someone gets involved and {control.RingName} benefits!"
                    ));
                    break;

                default: // FinishClean
                    r.CrowdEnergyDelta = Rng(10, 18) * iMod * feudMult * earnedMultiplier;
                    r.StorytellingContribution = 8.0 * iMod * feudMult * earnedMultiplier;
                    r.Commentary.Add(Pick(
                        $"{control.RingName} hits the finisher and covers! One... Two... Three! It's over!",
                        $"The finishing blow lands! {control.RingName} gets the three count!",
                        $"{other.RingName} goes down — and this time they're not getting up! {control.RingName} wins!"
                    ));
                    break;
            }

            // Shared finish contributions
            double styleSkill = control.RingSkills.GetStyleProficiency(control.Style);
            r.TechnicalContribution = 5.5 * (styleSkill / 5.0) * iMod * earnedMultiplier;

            // Final momentum swing in winner's direction
            r.MomentumDelta = (beat.Control == BeatControl.WrestlerA ? 1 : -1) * 30;

            // Record finish quality (used in final rating)
            state.FinishQuality = Math.Clamp(
                (earnedMultiplier * 80) + (state.CrowdEnergy * 0.2),
                0, 100);

            r.Commentary.Add(momentumFavours
                ? $"A fitting end — {control.RingName} earned that victory."
                : $"A controversial finish — did {control.RingName} really deserve that outcome?");
        }

        // ── Final rating ─────────────────────────────────────────────────────

        private MatchEngineResult BuildResult(MatchPlan plan, MatchEngineState state,
            List<BeatResult> beatResults)
        {
            double techComponent   = Math.Clamp(state.TechnicalScore / MaxTechnical, 0, 1) * 100 * TechWeight;
            double storyComponent  = Math.Clamp(state.StorytellingScore / MaxStorytelling, 0, 1) * 100 * StoryWeight;
            double crowdComponent  = ((state.CrowdPeakEnergy * 0.4) + (state.CrowdAverage * 0.6)) * CrowdWeight;

            // Finish quality nudges the final score (±10 points — doubled from original ±5
            // so an unearned finish meaningfully costs around a third of a star).
            double finishNudge = (state.FinishQuality - 50.0) / 100.0 * 20.0;

            double finalScore = Math.Clamp(techComponent + storyComponent + crowdComponent + finishNudge, 0, 100);
            double starRating = Math.Clamp(finalScore / 20.0, 0, 5);

            return new MatchEngineResult
            {
                Winner            = plan.BookedWinner!,
                Loser             = plan.BookedLoser!,
                BeatResults       = beatResults,
                TechnicalScore    = state.TechnicalScore,
                StorytellingScore = state.StorytellingScore,
                CrowdPeakEnergy   = state.CrowdPeakEnergy,
                CrowdAverageEnergy = state.CrowdAverage,
                FinishQuality     = state.FinishQuality,
                FinalScore        = finalScore,
                StarRating        = starRating
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private double AvgRingSkill(MatchPlan plan) =>
            (plan.WrestlerA.RingSkills.GetOverallSkill() + plan.WrestlerB.RingSkills.GetOverallSkill()) / 2.0;

        /// <summary>
        /// Crowd disposition (0–1): how much the crowd naturally gravitates toward this wrestler.
        /// Derived from popularity and fan group appeal — not alignment.
        /// </summary>
        private double CrowdDisposition(Wrestler w)
        {
            double popScore = w.Popularity / 100.0;

            if (w.Gimmick?.AppealRatings != null && w.Gimmick.AppealRatings.Count > 0)
            {
                double avgAppeal = w.Gimmick.AppealRatings.Average(a => a.AppealScore);
                return (popScore + avgAppeal) / 2.0;
            }

            return popScore;
        }

        private double Rng(double min, double max) =>
            min + _rand.NextDouble() * (max - min);

        private string Pick(params string[] options) =>
            options[_rand.Next(options.Length)];
    }
}
