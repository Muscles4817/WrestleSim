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
        /// <summary>
        /// How many isolation beats the hot tag's charge counts — and, the same number for
        /// the same reason, how many a crowd will sit through in a row before it stops
        /// waiting and starts entertaining itself.
        ///
        /// One constant rather than two so the payoff and the patience can never drift
        /// apart: the crowd's limit and the booking's value have to be the same limit, or
        /// the player is being asked to optimise against two different rules.
        /// </summary>
        private const int IsolationPatience = 3;

        private const double TechScale  = 48.0;
        private const double StoryScale = 62.0;

        // Band the weighted crowd reading is normalised against. Below the floor the
        // building is dead; at the reference ceiling it is as hot as a crowd ever gets.
        /// <summary>
        /// The investment reading a normal match produces. Matches here score exactly as
        /// they did before the reaction vector existed, so this feature moves the tails
        /// rather than shifting the whole scale.
        /// </summary>
        private const double TypicalInvestment = 0.50;

        /// <summary>
        /// How hard investment swings the crowd component either side of typical.
        ///
        /// The clamp is deliberately **asymmetric** — down to 0.65, up only to 1.06 — and
        /// that asymmetry is the point rather than a tuning convenience. Doc 16 §2.1 is
        /// about the cost of silence, not a bonus for engagement: an invested crowd is the
        /// baseline a match is supposed to earn, and being ignored is the failure. A
        /// symmetric version was tried and pushed 2.15% of all matches to a flat 5.00,
        /// because the pairings that draw the most investment are already near the ceiling
        /// and had nowhere to go.
        /// </summary>
        private const double InvestmentSwing = 0.70;

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

            /// <summary>
            /// One profile per participant, resolved once. A dictionary rather than two
            /// fields because a side can have more than one member — and because the old
            /// two-field version silently returned side B's profile for any wrestler it
            /// did not recognise, which is a wrong answer rather than an error.
            /// </summary>
            public required IReadOnlyDictionary<Wrestler, PerformerProfile> Profiles { get; init; }

            /// <summary>How much the crowd still wants to see this specific pairing, 0–1.</summary>
            public double Familiarity { get; init; } = 1.0;

            /// <summary>Whoever is legal for side A right now.</summary>
            public Wrestler LegalA => Plan.SideA.Members[State.LegalA];

            /// <summary>Whoever is legal for side B right now.</summary>
            public Wrestler LegalB => Plan.SideB.Members[State.LegalB];

            /// <summary>Profile of side A's legal performer.</summary>
            public PerformerProfile A => For(LegalA);

            /// <summary>Profile of side B's legal performer.</summary>
            public PerformerProfile B => For(LegalB);

            public PerformerProfile For(Wrestler w) =>
                Profiles.TryGetValue(w, out var profile)
                    ? profile
                    : throw new InvalidOperationException(
                        $"{w.RingName} has no profile in this match — they are not in it.");

            public bool IsSideA(Wrestler w) => Plan.SideA.Contains(w);

            /// <summary>The legal performer for a given side.</summary>
            public Wrestler LegalOf(MatchSide side) => side == Plan.SideA ? LegalA : LegalB;

            /// <summary>The legal performer on the side this wrestler is *not* on.</summary>
            public Wrestler Opponent(Wrestler w) => IsSideA(w) ? LegalB : LegalA;

            /// <summary>The legal performer for a beat's control, or null for Even / Contested.</summary>
            public Wrestler? ControlLegal(MatchBeat beat) => beat.Control switch
            {
                BeatControl.WrestlerA => LegalA,
                BeatControl.WrestlerB => LegalB,
                _                     => null
            };

            public MatchSide SideOf(Wrestler w) => IsSideA(w) ? Plan.SideA : Plan.SideB;

            // ── Aggregation ──────────────────────────────────────────────────
            //
            // Which aggregator a beat handler uses is decided by the *field it is
            // assigning*, not by taste:
            //
            //   CrowdEnergyDelta, and the opening-bell crowd numbers
            //       → whole side. The audience is looking at everyone it can see,
            //         including the man standing on the apron.
            //
            //   TechnicalContribution, StorytellingContribution, AdvantageDelta,
            //   FinishQuality
            //       → the legal performers only. The man on the apron is not working.
            //
            // Where one intermediate feeds both, compute it twice.
            //
            // Three documented carve-outs, all of the same kind — the *subject* of the
            // beat is the pair itself rather than one person's work:
            //   • FadeFactor reads the whole side, because tagging out is precisely how a
            //     team resists fatigue.
            //   • ApplyShine's teamwork and ApplyDoubleTeam's combined/weakestLink read the
            //     whole side, because a shine and a double team are worked by both men.
            //   • ApplyAllFourBrawl reads both whole sides, because nobody is on the apron.

            /// <summary>
            /// How much a side's weakest member drags its reading toward the average.
            /// 0 = the side reads as its best member, 1 = a flat mean.
            ///
            /// A flat mean is wrong, and wrong in a specific way: it makes a star-and-jobber
            /// team grade as exactly the average of the star's match and the jobber's match,
            /// so the star loses precisely what the jobber gains. That is a conservation law,
            /// not a wrestling model, and it contradicts the reference directly — a top star
            /// working with a weaker man is "the single most effective star-making tool that
            /// exists" (docs/wrestling-reference/12-pushes-and-positioning.md §3.2), and
            /// proximity transfers heat *to* the weaker party
            /// (docs/wrestling-reference/17-heat-and-getting-over.md §2.8).
            ///
            /// At 0.5 the star carries: a partner nobody knows dilutes the team's reading
            /// without halving it. Phase 4's team chemistry lowers this for an established
            /// team, which reads to a crowd as one act rather than two people.
            /// </summary>
            public const double DragWeight = 0.5;

            /// <summary>
            /// How much of the drag an established team removes. At full chemistry a team
            /// reads as one act, so the weaker man barely pulls the side down at all —
            /// which is the mechanical form of "the tag division is where you elevate
            /// somebody" (docs/wrestling-reference/12-pushes-and-positioning.md §2.2.1).
            /// </summary>
            public const double ChemistryLift = 0.7;

            /// <summary>
            /// The drag actually applied to a side, after its chemistry. Two strangers get
            /// the full <see cref="DragWeight"/>; a team that moves as one gets a fraction
            /// of it and therefore reads much closer to its best member.
            /// </summary>
            private static double DragFor(MatchSide side) =>
                DragWeight * (1.0 - ChemistryLift * Math.Clamp(side.Chemistry, 0, 1));

            /// <summary>
            /// A profile factor for one whole side, weighted toward its strongest member.
            /// For a side of one this is exactly that member's value.
            /// </summary>
            public double SideAvg(MatchSide side, Func<PerformerProfile, double> f) =>
                TopWeighted(side.Members.Select(m => f(For(m))), DragFor(side));

            /// <summary>Both sides on a factor, each side read as a side first.</summary>
            public double Pair(Func<PerformerProfile, double> f) =>
                (SideAvg(Plan.SideA, f) + SideAvg(Plan.SideB, f)) / 2.0;

            /// <summary>A raw wrestler stat across both sides, each side read as a side first.</summary>
            public double PairStat(Func<Wrestler, double> f) =>
                (TopWeighted(Plan.SideA.Members.Select(f), DragFor(Plan.SideA))
                 + TopWeighted(Plan.SideB.Members.Select(f), DragFor(Plan.SideB))) / 2.0;

            /// <summary>The two performers actually working, for fields that describe work.</summary>
            public double LegalPair(Func<PerformerProfile, double> f) => (f(A) + f(B)) / 2.0;

            /// <summary>A raw stat across the two performers actually working.</summary>
            public double LegalPairStat(Func<Wrestler, double> f) => (f(LegalA) + f(LegalB)) / 2.0;

            private static double TopWeighted(IEnumerable<double> values, double drag)
            {
                var list = values as IList<double> ?? values.ToList();
                if (list.Count == 1) return list[0];

                double best = list.Max();
                return best + (list.Average() - best) * drag;
            }
        }

        // ── Public entry point ───────────────────────────────────────────────

        /// <summary>
        /// Runs the plan.
        ///
        /// <paramref name="familiarity"/> is how much the crowd still wants to see this
        /// specific pairing, 1.0 being the first time — see
        /// <see cref="Models.MatchPlan.Feud.Familiarity"/> and
        /// docs/wrestling-reference/20-storylines-and-feuds.md §9.1.
        ///
        /// It is a parameter rather than a field on the plan because it is a property of
        /// the audience on the night, not of the booking. A card written six weeks out
        /// must be graded against how stale the pairing is when it actually goes on, and
        /// a plan the player never runs should not carry a stale reading around with it.
        /// </summary>
        public MatchEngineResult Execute(MatchPlan plan, double familiarity = 1.0)
        {
            var errors = plan.Validate();
            if (errors.Any())
                throw new InvalidOperationException(
                    "Invalid match plan:\n" + string.Join("\n", errors.Select(e => "  • " + e)));

            var state = new MatchEngineState();
            state.InitialiseLegal(plan.SideA.StartingIndex, plan.SideB.StartingIndex);

            var ctx = new Ctx
            {
                Plan        = plan,
                State       = state,
                Profiles    = plan.AllParticipants.ToDictionary(w => w, w => new PerformerProfile(w)),
                Familiarity = Math.Clamp(familiarity, 0.0, 1.5)
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

            double avgPop = ctx.PairStat(w => w.EffectiveOverness);

            // Crowd disposition modifier: rewards having BOTH SIDES over, not just one.
            // Min rather than average across the two sides, so a card-filler opposite a
            // star does not let the room start hot — the crowd has to care about both
            // corners. Within a side the reading is top-weighted (see Ctx.DragWeight), so
            // one nobody on a two-man team dilutes that side rather than erasing it.
            double bothOverBonus = Math.Min(
                ctx.SideAvg(plan.SideA, p => p.Disposition),
                ctx.SideAvg(plan.SideB, p => p.Disposition)) * 8.0; // up to +8

            // Connection also moves the opening bell: a building that came to see these two
            // specific people starts louder than one that recognises neither.
            double connectionLift = (ctx.Pair(p => p.Connection) - 1.0) * 14.0;

            double baseEnergy = (avgPop / 100.0) * 60.0 + bothOverBonus + connectionLift;
            double feudBonus  = plan.Feud?.StartingEnergyBonus ?? 0;

            // A championship creates stakes the crowd brings with it — doc 21 §1.1 — and
            // how much of that arrives is exactly the belt's prestige. A devalued title
            // adds almost nothing, which is the whole point of tracking prestige at all.
            double titleBonus = plan.TitleAtStake?.StakesBonus ?? 0;

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

            // Two rules meet here, and they compose rather than compete.
            //
            // Stakes lift the roof: a room that believes the belt matters will go further
            // for it than the same room would for the same two people in a meaningless
            // match (docs/wrestling-reference/21-championships.md §1).
            //
            // Familiarity then scales the whole room down — on the appetite, not the work
            // (docs/wrestling-reference/20-storylines-and-feuds.md §9.1). Two good hands
            // having their fifth match still wrestle it well: the holds are as clean, the
            // timing as sharp. What has gone is the appetite. So this leaves the technical
            // and storytelling accumulators completely alone. A stale rematch is a good
            // match in a flat room, which is what it looks like in life.
            //
            // Familiarity is applied last, and to the total, so a title cannot buy a room
            // out of being sick of a pairing — it can only make the flat version of it
            // slightly less flat.
            //
            // The floor drops below the usual 35 because a pairing the crowd is sick of
            // can be deader than one they simply do not know.
            double roomForThisPairing =
                40.0 + pairConnection * 46.0 + (pairCraft - 1.0) * 20.0
                     - staminaPenalty + titleBonus * 0.45;

            state.CrowdCeiling = Math.Clamp(roomForThisPairing * ctx.Familiarity, 22, 100);

            state.CrowdEnergy = Math.Clamp(
                (baseEnergy + feudBonus + titleBonus) * ctx.Familiarity,
                8, Math.Min(90, state.CrowdCeiling));
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
            Wrestler? control = ctx.ControlLegal(beat);
            Wrestler other    = control != null ? ctx.Opponent(control) : ctx.LegalB;

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

                case BeatType.Shine:
                    ApplyShine(result, beat, ctx, control, other, iMod, dMod);
                    break;

                case BeatType.Cutoff:
                    ApplyCutoff(result, beat, ctx, control, other, iMod, dMod);
                    break;

                case BeatType.Isolation:
                    ApplyIsolation(result, beat, ctx, control, other, iMod, dMod);
                    break;

                case BeatType.NearTag:
                    ApplyNearTag(result, ctx, control, other, iMod, feudMult);
                    break;

                case BeatType.HotTag:
                    ApplyHotTag(result, beat, ctx, control, other, iMod);
                    break;

                case BeatType.Tag:
                case BeatType.BlindTag:
                    ApplyTag(result, beat, ctx, control, other, iMod);
                    break;

                case BeatType.DoubleTeam:
                    ApplyDoubleTeam(result, beat, ctx, control, other, iMod, dMod);
                    break;

                case BeatType.Miscommunication:
                    ApplyMiscommunication(result, ctx, control, other, iMod);
                    break;

                case BeatType.SaveBreakup:
                    ApplySaveBreakup(result, ctx, control, other, iMod, timesUsed);
                    break;

                case BeatType.AllFourBrawl:
                    ApplyAllFourBrawl(result, ctx, iMod, dMod);
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
            // ── What kind of reaction was that? ──────────────────────────────
            // Classified here rather than in twenty handlers, because for most beats the
            // answer follows from things the engine already knows: which way the delta
            // went, and how the crowd feels about whoever drew it. The handlers that
            // override it are the ones where the sign lies.
            result.ResolvedReaction = RecordReaction(result, ctx, control);

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

        /// <summary>
        /// Works out what kind of noise — or silence — a beat drew, and records it.
        ///
        /// The rule, from docs/wrestling-reference/16-crowd-psychology.md §2:
        ///
        ///   • Noise for somebody the crowd likes is a **pop**; noise around somebody they
        ///     want beaten is **heat**, which is engagement and good. A booed heel is the
        ///     fuel the whole face-in-peril structure runs on.
        ///   • Quiet where the crowd is invested is **tension** — the held breath. Quiet
        ///     where they are not is **silence**, and silence is the failure state (§2.1).
        ///   • Quiet in a room that has stopped caring, from people it was never given a
        ///     reason to care about, is **go-away heat**.
        ///
        /// A room is never doing exactly one of these, so the weight is *split* rather than
        /// assigned. That matters: a binary threshold made the whole feature almost inert,
        /// because it put the median performer exactly on the line and every match either
        /// side of it read as fully invested or fully absent. A share makes two nobodies
        /// accumulate real silence over a long match, which is the outcome A5 exists to
        /// produce.
        /// </summary>
        private ReactionKind RecordReaction(BeatResult r, Ctx ctx, Wrestler? control)
        {
            var state = ctx.State;
            double weight = Math.Max(1.5, Math.Abs(r.CrowdEnergyDelta));

            // A handler that knows better than the sign — the denied tag, the overworked
            // isolation — takes the whole weight and says so.
            if (r.Reaction is { } declared)
            {
                state.RecordReaction(declared, weight);
                return declared;
            }

            // Booking, not just casting. A crowd asked to watch the same thing over and
            // over starts entertaining itself — the sarcastic chants and the counting-along
            // of doc 16 §2, which it flags as a red alert. RepetitionFactor is already
            // computed per beat; below about half its original value the beat is being
            // repeated past the point anybody is still watching it.
            if (r.RepetitionFactor < 0.5)
            {
                state.RecordReaction(ReactionKind.GoAwayHeat, weight);
                return ReactionKind.GoAwayHeat;
            }

            // How much this room cares about the people in front of it.
            //
            // The window is set against the values Connection actually takes on the shipped
            // roster, not its theoretical range: a nobody lands near 0.30, a midcard hand
            // near 0.70, a genuine draw near 1.17. A first attempt used the theoretical
            // 0.72–1.30 and read a *midcarder* as completely uninvested, which is plainly
            // wrong — a midcard match still has a crowd.
            double connection = control is not null
                ? ctx.For(control).Connection
                : ctx.Pair(p => p.Connection);
            double invested = Math.Clamp((connection - 0.32) / 0.80, 0, 1);

            if (r.CrowdEnergyDelta > 0.5)
            {
                // Whose noise is it? A crowd that dislikes the man on top is booing him,
                // and booing him is engagement.
                double disposition = control is not null
                    ? ctx.For(control).Disposition
                    : ctx.Pair(p => p.Disposition);
                double liked = Math.Clamp((disposition - 0.30) / 0.40, 0, 1);

                // The share the room does not care about at all still goes nowhere.
                double engaged = weight * invested;
                state.RecordReaction(ReactionKind.Pop,  engaged * liked);
                state.RecordReaction(ReactionKind.Heat, engaged * (1 - liked));
                state.RecordReaction(ReactionKind.Silence, weight * (1 - invested));

                return liked >= 0.5 ? ReactionKind.Pop : ReactionKind.Heat;
            }

            if (r.CrowdEnergyDelta < -0.5)
            {
                // The distinction the scalar could never make. A room that cares is holding
                // its breath; a room that does not has started entertaining itself.
                state.RecordReaction(ReactionKind.Tension,    weight * invested);
                state.RecordReaction(ReactionKind.GoAwayHeat, weight * (1 - invested));

                return invested >= 0.5 ? ReactionKind.Tension : ReactionKind.GoAwayHeat;
            }

            // Nothing happened either way — a rest hold, a feeling-out. Whether that is
            // attentive or absent is again entirely a question of who is in the ring.
            state.RecordReaction(ReactionKind.Tension, weight * invested);
            state.RecordReaction(ReactionKind.Silence, weight * (1 - invested));

            return invested >= 0.5 ? ReactionKind.Tension : ReactionKind.Silence;
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

            // The isolation is meant to be repeated — that is how the charge is built —
            // so it decays gently. Everything else in the tag formula is a moment.
            BeatType.Isolation            => 0.86,
            BeatType.NearTag              => 0.80,
            BeatType.Shine                => 0.70,
            BeatType.DoubleTeam           => 0.74,

            // A second hot tag in one match is a different match. A second miscommunication
            // is a comedy spot. A third save is the referee being ignored.
            BeatType.HotTag               => 0.45,
            BeatType.Miscommunication     => 0.50,
            BeatType.SaveBreakup          => 0.55,
            BeatType.AllFourBrawl         => 0.60,

            // A tag itself is not a moment and does not wear out.
            BeatType.Tag                  => 1.00,
            BeatType.BlindTag             => 0.75,
            BeatType.Cutoff               => 0.72,

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

            // Deliberately the whole side rather than the legal performers, even though
            // this scales craft output: tagging out is literally how a team resists
            // fatigue, so a fresh partner should hold the match up late.
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
        // Advantage is a side-level axis, so this asks which side the controlling
        // performer is on. Reference equality against a single named wrestler gave the
        // wrong sign the moment a side had a second member who could take control.
        private static int ControlSign(Ctx ctx, Wrestler control) =>
            ctx.IsSideA(control) ? 1 : -1;

        // ── Individual beat handlers ─────────────────────────────────────────

        private void ApplyHotOpening(BeatResult r, Ctx ctx, double iMod, double dMod)
        {
            var plan = ctx.Plan;
            double avgRing     = AvgRingSkill(ctx);          // → Technical: legal men
            double avgCharisma = ctx.PairStat(w => w.Charisma); // → crowd: whole side

            double connection = ctx.Pair(p => p.Connection);

            // A hot opening is a sprint — it only reads as frantic if they can move.
            double pace = PerformerProfile.Blend(ctx.LegalPair(p => p.Athleticism), 0.50); // → Technical

            r.CrowdEnergyDelta      = Rng(8, 14) * iMod * (0.7 + avgCharisma / 5.0 * 0.6) * connection * pace;
            r.AdvantageDelta         = Rng(-5, 5);
            r.TechnicalContribution = 4.0 * (avgRing / 5.0) * iMod * ctx.LegalPair(p => p.Workrate) * pace;
            r.StorytellingContribution = 2.5 * iMod * PerformerProfile.Blend(connection, 0.6);

            r.Commentary.Add(Pick(
                $"{ctx.LegalA.RingName} and {ctx.LegalB.RingName} immediately go at each other before the bell finishes ringing!",
                $"No feeling-out process — the crowd erupts as these two collide from the first second!",
                $"{ctx.LegalA.RingName} and {ctx.LegalB.RingName} are at each other's throats right away!",
                $"The bell barely sounds before {ctx.LegalA.RingName} and {ctx.LegalB.RingName} are trading shots!",
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
            double avgTech = ctx.LegalPairStat(w => w.RingSkills.Technical); // → Technical

            // A slow start only works if the crowd trusts these two to go somewhere with it.
            double patience = PerformerProfile.Blend(ctx.Pair(p => p.Connection), 0.7);

            r.CrowdEnergyDelta      = Rng(-2, 4) * iMod * patience;
            r.AdvantageDelta         = Rng(-3, 3);
            r.TechnicalContribution = 5.5 * (avgTech / 5.0) * dMod
                                      * ctx.LegalPair(p => p.WorkrateFor(WrestlingStyle.Technical))
                                      * PerformerProfile.Blend(ctx.LegalPair(p => p.RingPsych), 0.7);
            r.StorytellingContribution = 3.0 * dMod * PerformerProfile.Blend(ctx.LegalPair(p => p.RingPsych), 0.6);

            r.Commentary.Add(Pick(
                $"{ctx.LegalA.RingName} and {ctx.LegalB.RingName} circle each other, measuring the distance carefully.",
                $"A deliberate, methodical start as both wrestlers respect each other's ability.",
                $"The feeling-out process begins — neither willing to show their hand too soon.",
                $"{ctx.LegalA.RingName} and {ctx.LegalB.RingName} are in no rush — this is going to be a war of attrition.",
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
            double avgRing = AvgRingSkill(ctx);

            r.CrowdEnergyDelta      = Rng(3, 8) * iMod * ctx.Pair(p => p.Connection);
            r.AdvantageDelta         = Rng(-4, 4);
            r.TechnicalContribution = 4.5 * (avgRing / 5.0) * dMod * ctx.LegalPair(p => p.Workrate);
            r.StorytellingContribution = 2.0 * dMod * PerformerProfile.Blend(ctx.LegalPair(p => p.RingPsych), 0.5);

            r.Commentary.Add(Pick(
                $"{ctx.LegalA.RingName} and {ctx.LegalB.RingName} lock up.",
                $"The match gets under way with both wrestlers testing each other.",
                $"An even start as {ctx.LegalA.RingName} and {ctx.LegalB.RingName} feel each other out.",
                $"A collar-and-elbow tie-up to open — both wrestlers gauging what they're dealing with.",
                $"Standard opening exchanges, but the undercurrent of tension is already obvious."
            ));
        }

        private void ApplyHeatSegment(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double dMod)
        {
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

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
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

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

            r.TechnicalContribution    = 4.5 * (AvgRingSkill(ctx) / 5.0) * iMod * pControl.Workrate
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
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

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
            double avgPsych = ctx.LegalPairStat(w => w.Mental.Psychology); // → Storytelling
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
            control ??= ctx.LegalA;
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

        /// <summary>
        /// A rest hold is where a match either breathes or dies, and which one depends
        /// entirely on whether the room is invested. Handled by the default classification:
        /// its delta is small, so it reads as tension in an invested room and silence in an
        /// empty one — which is exactly the distinction doc 16 §2.1 says matters most.
        /// </summary>
        private void ApplyRestHold(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, double iMod, double dMod)
        {
            control ??= ctx.LegalA;
            Wrestler other = ctx.Opponent(control);
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
                : ctx.LegalPairStat(w => w.RingSkills.Brawler); // → Technical
            double brawlFactor = 0.5 + brawlerSkill / 5.0 * 0.8; // 0.66–1.30

            double connection = control != null ? ctx.For(control).Connection : ctx.Pair(p => p.Connection);
            double workrate   = control != null
                ? ctx.For(control).WorkrateFor(WrestlingStyle.Brawler)
                : ctx.LegalPair(p => p.WorkrateFor(WrestlingStyle.Brawler)); // → Technical

            r.CrowdEnergyDelta      = Rng(6, 12) * iMod * dMod * brawlFactor
                                      * PerformerProfile.Blend(connection, 0.6);
            r.AdvantageDelta         = Rng(-8, 8);
            r.TechnicalContribution = 3.0 * (brawlerSkill / 5.0) * iMod * workrate;
            r.StorytellingContribution = 4.5 * iMod * dMod * PerformerProfile.Blend(connection, 0.5);

            r.Commentary.Add(Pick(
                $"This match spills out to the floor! The crowd parts as the brawl comes to them!",
                $"{ctx.LegalA.RingName} and {ctx.LegalB.RingName} are fighting into the crowd!",
                $"Chaos! These two are taking this war everywhere!",
                $"We have completely lost control — they're brawling through the entire arena!",
                $"The guardrail is not going to contain this one — they're spilling out into the audience!"
            ));
        }

        private void ApplyPsychologicalWarfare(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double feudMult)
        {
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

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
            // Feeds a crowd field and a craft field, so it is read twice: the room sees
            // everyone in the feud, but only the people working carry the story.
            double roomConnection  = ctx.Pair(p => p.Connection);
            double workedConnection = ctx.LegalPair(p => p.Connection);

            r.CrowdEnergyDelta = Rng(14, 24) * iMod * feudMult * roomConnection;
            r.AdvantageDelta    = Rng(-5, 5); // contested — both wrestlers go at it
            r.TechnicalContribution    = 2.0 * iMod;
            r.StorytellingContribution = 14.0 * iMod * feudMult
                                         * PerformerProfile.Blend(workedConnection, 0.5);

            r.Commentary.Add(Pick(
                $"This feud reaches a boiling point! {ctx.LegalA.RingName} and {ctx.LegalB.RingName} can no longer contain their hatred!",
                $"Everything this feud has been building toward is pouring out right now!",
                $"The bad blood between these two erupts — the crowd is absolutely unhinged!",
                $"The gloves are off! The real hatred between {ctx.LegalA.RingName} and {ctx.LegalB.RingName} is on full display!",
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
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

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
            double roomConnection   = ctx.Pair(p => p.Connection);
            double workedConnection = ctx.LegalPair(p => p.Connection);

            r.CrowdEnergyDelta = Rng(10, 16) * iMod * feudMult * PerformerProfile.Blend(roomConnection, 0.7);
            r.AdvantageDelta    = Rng(-10, 10);
            r.TechnicalContribution    = 1.0;
            r.StorytellingContribution = 9.0 * iMod * feudMult * PerformerProfile.Blend(workedConnection, 0.5);

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
            control ??= ctx.LegalA;
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

        // ── The tag formula ──────────────────────────────────────────────────
        //
        // Nine handlers, one rule between them: the hot tag is worth what the isolation
        // paid for. Everything else here either builds that charge or spends it.
        //
        // docs/wrestling-reference/18-match-craft.md §3 and
        // docs/wrestling-reference/16-crowd-psychology.md §2.

        /// <summary>
        /// The shine: the face team on top before the heat, establishing them as worth
        /// caring about. A crowd that never saw the team look good has no reason to want
        /// them saved later.
        /// </summary>
        private void ApplyShine(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double dMod)
        {
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

            var pControl = ctx.For(control);
            var side     = ctx.SideOf(control);

            // Reads off the whole side rather than off whoever is legal — a shine is the
            // team looking sharp together. For a side of one that is exactly that man,
            // which is why this beat is bookable in a singles match too.
            double teamwork = ctx.SideAvg(side, p => (p.Athleticism + p.Workrate) / 2.0);

            r.CrowdEnergyDelta = Rng(6, 12) * iMod * dMod
                                 * ctx.SideAvg(side, p => p.Connection);

            r.AdvantageDelta = ControlSign(ctx, control) * Rng(10, 20) * iMod * dMod;

            r.TechnicalContribution = 5.5 * (AvgRingSkill(ctx) / 5.0) * iMod * dMod * teamwork;
            r.StorytellingContribution = 4.0 * iMod * dMod
                                         * PerformerProfile.Blend(pControl.RingPsych, 0.45);

            // Four options either way, so the RNG is drawn exactly once whichever branch
            // runs and a tag match's output is unchanged by the singles lines existing.
            r.Commentary.Add(side.IsTag
                ? Pick(
                    $"{side.Name} are firing on all cylinders early — quick tags, and {other.RingName} cannot get a foothold.",
                    $"A brilliant opening stretch from {side.Name}! They are running rings around {other.RingName}.",
                    $"{side.Name} in complete control, and the crowd is loving every second of it.",
                    $"Textbook teamwork from {side.Name} — in and out of the corner before the referee can blink.")
                : Pick(
                    $"{control.RingName} is firing on all cylinders early, and {other.RingName} cannot get a foothold.",
                    $"A brilliant opening stretch from {control.RingName}! They are running rings around {other.RingName}.",
                    $"{control.RingName} in complete control, and the crowd is loving every second of it.",
                    $"Every time {other.RingName} tries to settle, {control.RingName} has an answer for them."));
        }

        /// <summary>
        /// The cut-off: the heels take over and the heat begins. Usually off a distraction,
        /// which is why it does not need to be clean to work.
        /// </summary>
        private void ApplyCutoff(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double dMod)
        {
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

            var pControl = ctx.For(control);
            var pOther   = ctx.For(other);

            // The room quietens because the team it was enjoying just stopped being on top.
            // A cut-off the crowd cares about costs more energy, not less — that is the
            // point of it.
            r.CrowdEnergyDelta = -Rng(2, 6) * iMod * PerformerProfile.Blend(pOther.Connection, 0.6);

            r.AdvantageDelta = ControlSign(ctx, control) * Rng(20, 34) * iMod * dMod;

            r.TechnicalContribution = 3.5 * iMod * pControl.Workrate;
            r.StorytellingContribution = 6.0 * iMod
                                         * PerformerProfile.Blend(pControl.RingPsych, 0.55)
                                         * PerformerProfile.Blend(pOther.Selling, 0.45);

            r.Commentary.Add(Pick(
                $"{control.RingName} cuts {other.RingName} off at the knees — and just like that, the complexion of this match has changed.",
                $"One moment of distraction and {control.RingName} takes over. {other.RingName} is a long way from his corner.",
                $"There it is — {control.RingName} shuts the door, and {other.RingName} is in trouble.",
                $"{control.RingName} catches {other.RingName} coming in and the heat is on."
            ));
        }

        /// <summary>
        /// The face in peril. A heat segment with a corner to be kept away from — which is
        /// the whole difference between a tag match and a singles match with four names.
        ///
        /// Charges the hot tag. Control is the side doing the isolating; the man being
        /// worked over is their opponent's legal performer.
        /// </summary>
        private void ApplyIsolation(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double dMod)
        {
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

            var pControl    = ctx.For(control);
            var pIsolated   = ctx.For(other);
            var isolatedSide = ctx.SideOf(other);

            // Same cool-heel rule the singles heat segment uses: a crowd that likes the
            // man on top is not generating "save him" tension.
            double tensionFactor = 1.0 - pControl.Disposition * 0.6;

            // But the tension is about the corner, not just the victim. The crowd is
            // watching a man who cannot reach help — so how much they want the *partner*
            // in matters as much as how much they like the man taking the beating.
            double sympathy = PerformerProfile.Blend(pIsolated.Connection, 0.60);
            double cornerPull = PerformerProfile.Blend(
                isolatedSide.Members.Count > 1
                    ? isolatedSide.PartnersOf(other).Average(m => ctx.For(m).Connection)
                    : pIsolated.Connection,
                0.40);

            // How far past the room's patience this beat is. The run — not the count —
            // because a long heat punctuated by hope spots is what a big match looks like
            // (docs/wrestling-reference/18-match-craft.md §3.1), while four in a row with
            // no near tag is a crowd being asked to wait with nothing to hold on to.
            bool isolatedIsSideA = ctx.IsSideA(other);
            int overPatience = ctx.State.IsolationRun(isolatedIsSideA) + 1 - IsolationPatience;

            // Past that point the beat stops adding to the room and starts draining it,
            // and the drain accelerates. Doc 16 §2: a crowd that has given up does not go
            // politely quiet, it finds something else to do.
            r.CrowdEnergyDelta = overPatience <= 0
                ? Rng(2, 7) * tensionFactor * iMod * dMod * sympathy * cornerPull
                : -Rng(3, 8) * overPatience * iMod;

            r.AdvantageDelta   = ControlSign(ctx, control) * Rng(10, 22) * iMod * dMod;

            WrestlingStyle beatStyle = beat.StyleHint ?? control.Style;
            r.TechnicalContribution = 6.0 * (control.RingSkills.GetStyleProficiency(beatStyle) / 5.0)
                                      * iMod * dMod * pControl.WorkrateFor(beatStyle)
                                      * PerformerProfile.Blend(pIsolated.Selling, 0.60);

            // Being kept from your corner is the story — right up until the room stops
            // believing there is a story left. Past the patience point there is no story
            // being told, only a match being padded.
            //
            // TechnicalContribution above is deliberately NOT punished: an overlong heat
            // segment is badly *paced*, not badly *wrestled*, and the distinction is why
            // the crowd axis is the right place for this.
            r.StorytellingContribution = overPatience <= 0
                ? 7.5 * iMod * dMod
                    * PerformerProfile.Blend(pIsolated.Selling, 0.65)
                    * PerformerProfile.Blend(pControl.RingPsych, 0.40)
                : 0.0;

            // Past the room's patience this is not tension, it is the crowd entertaining
            // itself — duelling chants, a beach ball, doc 16 §2's red alert. Before the
            // patience point an isolation is heat if the man on top is disliked, which the
            // default classification already gets right.
            if (overPatience > 0) r.Reaction = ReactionKind.GoAwayHeat;

            ctx.State.RecordIsolation(isolatedIsSideA);

            r.Commentary.Add(overPatience <= 0
                ? Pick(
                    $"{control.RingName} keeps {other.RingName} grounded in the wrong corner — miles from help.",
                    $"{other.RingName} is cut off and being taken apart. That corner might as well be a mile away.",
                    $"Every time {other.RingName} gets to his feet, {control.RingName} drags him back. Textbook isolation.",
                    $"{control.RingName} is working {other.RingName} over methodically, and the crowd is getting restless.")
                : Pick(
                    $"{control.RingName} is still grinding away on {other.RingName}, and you can hear duelling chants starting up in the lower bowl.",
                    $"This has gone on a long time now. The crowd has started talking amongst themselves.",
                    $"Another rest hold on {other.RingName}. Somebody in the front row has produced a beach ball.",
                    $"{control.RingName} keeps working, but the room has stopped waiting for the tag and started entertaining itself."));
        }

        /// <summary>
        /// The tag reached for and denied.
        ///
        /// This is the one beat in the engine that is *supposed* to take energy out of the
        /// building. The room going quiet and frustrated is stored energy, not lost energy
        /// — it comes back at the hot tag, and it comes back bigger than another isolation
        /// would have made it (docs/wrestling-reference/16-crowd-psychology.md §2).
        /// </summary>
        private void ApplyNearTag(BeatResult r, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double feudMult)
        {
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

            var pDenied  = ctx.For(other);
            var deniedSide = ctx.SideOf(other);

            // The groan is proportional to how much they wanted it.
            double investment = PerformerProfile.Blend(pDenied.Connection, 0.70);

            r.CrowdEnergyDelta = -Rng(3, 8) * iMod * investment;
            r.AdvantageDelta   = ControlSign(ctx, control) * Rng(4, 10) * iMod;

            r.TechnicalContribution    = 1.5 * iMod;
            r.StorytellingContribution = 9.0 * iMod * feudMult
                                         * PerformerProfile.Blend(pDenied.Selling, 0.55)
                                         * investment;

            // The beat the scalar could never read correctly. A denied tag takes energy
            // out of the building, and that quiet is the whole point of it — the room is
            // holding its breath, not leaving. Phase 2's adjudication had to describe this
            // in a comment as "stored energy" precisely because the engine had no way to
            // say it. Now it does.
            r.Reaction = ReactionKind.Tension;

            ctx.State.RecordNearTag(ctx.IsSideA(other));

            string partner = deniedSide.Members.Count > 1
                ? deniedSide.PartnersOf(other).First().RingName
                : "the corner";

            r.Commentary.Add(Pick(
                $"{other.RingName} reaches — and {control.RingName} drags him back! {partner} is beside himself on the apron!",
                $"SO CLOSE! {other.RingName} was inches from {partner} and {control.RingName} pulled him away!",
                $"The referee is distracted, {other.RingName} makes the tag — and it does not count! The crowd is furious!",
                $"{other.RingName} lunges for {partner}... and comes up empty. You can hear the air go out of this building."
            ));
        }

        /// <summary>
        /// The hot tag — the loudest planned moment in professional wrestling, and worth
        /// almost nothing if nothing was spent buying it.
        ///
        /// Built as a direct sibling of the unearned-finish rule in <see cref="ApplyFinish"/>:
        /// a payoff the booking did not earn takes a hard multiplier. Here the charge comes
        /// from isolation beats and denied tags since this side last got someone fresh in.
        /// </summary>
        private void ApplyHotTag(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod)
        {
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

            var state = ctx.State;
            bool sideA = ctx.IsSideA(control);
            var side   = ctx.SideOf(control);

            int isolations = state.IsolationsSuffered(sideA);
            int nearTags   = state.NearTagsDenied(sideA);

            // The man who has been taking the beating, before the tag brings in his partner.
            var beaten = control;

            double charge = HotTagCharge(isolations, nearTags);

            // Bring the fresh man in. Everything after this beat is worked by him.
            int incomingIndex = state.Tag(sideA, side.Members.Count, beat.IncomingIndex);
            var fresh = side.Members[incomingIndex];
            var pFresh = ctx.For(fresh);

            // The pop belongs to the man coming in, and to how badly the crowd wanted him.
            // Deliberately the largest single crowd delta the engine can produce. A
            // comeback is Rng(12,20); this has to dwarf it, because the format spends
            // several minutes of deliberately quiet television buying it. Undersized, the
            // isolation reads as a cost with no payoff and the tag formula grades below a
            // sprint that never put anyone in peril.
            r.CrowdEnergyDelta = Rng(26, 40) * iMod * charge
                                 * PerformerProfile.Blend(pFresh.Connection, 0.70)
                                 * PerformerProfile.Blend(pFresh.Athleticism, 0.40);

            // A hot tag wipes out the heat that came before it, the same way a comeback does.
            double deficit = sideA ? Math.Max(0, -state.Advantage) : Math.Max(0, state.Advantage);
            r.AdvantageDelta = (sideA ? 1 : -1) * (Rng(28, 46) * iMod + deficit * 0.65);

            r.TechnicalContribution = 5.0 * (AvgRingSkill(ctx) / 5.0) * iMod * pFresh.Workrate
                                      * PerformerProfile.Blend(pFresh.Athleticism, 0.45);
            r.StorytellingContribution = 11.0 * iMod * charge
                                         * PerformerProfile.Blend(pFresh.Connection, 0.55);

            r.Commentary.Add(charge >= 1.3
                ? Pick(
                    $"HOT TAG! {fresh.RingName} is in and this place has come UNGLUED!",
                    $"{beaten.RingName} makes it — {fresh.RingName} comes in like a house on fire and the roof comes off!",
                    $"THERE IT IS! After all that punishment, {beaten.RingName} finally reaches {fresh.RingName} — and the building erupts!")
                : charge >= 0.85
                    ? Pick(
                        $"{beaten.RingName} gets the tag! {fresh.RingName} is in, and the crowd is up.",
                        $"The tag is made — {fresh.RingName} comes in fresh and goes straight after {other.RingName}.")
                    : Pick(
                        $"{beaten.RingName} tags {fresh.RingName} in. The crowd is not quite sure why that was the moment.",
                        $"{fresh.RingName} comes in. He had not been out there long enough for anyone to miss him."));

            if (isolations == 0)
                r.Commentary.Add(
                    "Nobody was ever in trouble, so there is nothing for that tag to release.");
        }

        /// <summary>
        /// How much the isolation bought. 1.0 is the neutral reading; a fully-built tag is
        /// worth roughly twice that, and one nobody paid for is worth about half.
        ///
        /// The isolation term saturates at three beats and the near-tag term at two: a
        /// fourth isolation is a crowd getting bored rather than a crowd getting desperate,
        /// which is the same diminishing-returns shape the rest of the engine uses.
        /// </summary>
        private static double HotTagCharge(int isolations, int nearTags)
        {
            double isolationTerm = 0.55 * Math.Min(isolations, IsolationPatience) / (double)IsolationPatience;

            // Worth more per beat than another isolation. A denied tag costs the room real
            // energy in the moment, so if it did not pay back more than the isolation it
            // replaced, booking one would be a straight loss and nobody would ever do it.
            double nearTagTerm = 0.45 * Math.Min(nearTags, 2) / 2.0;

            if (isolations == 0)
            {
                // The unearned-payoff penalty, deliberately the same 0.55 the engine
                // charges for a finish that momentum did not support.
                //
                // Near tags still count here. They used to be discarded outright in this
                // branch, which made a denied tag with no isolation behind it a pure cost
                // — the room lost energy and nothing bought it back — and directly
                // contradicted the reason the beat exists. A near-tag-only heat is still
                // a thin one, so it is scaled against the penalised floor rather than the
                // full one, but it is no longer worthless.
                return 0.55 + nearTagTerm * 0.55;
            }

            return 1.0 + isolationTerm + nearTagTerm;
        }

        /// <summary>A routine or blind tag. Changes who is legal without being a moment.</summary>
        private void ApplyTag(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod)
        {
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

            bool sideA = ctx.IsSideA(control);
            var side   = ctx.SideOf(control);
            bool blind = beat.Type == BeatType.BlindTag;

            int incomingIndex = ctx.State.Tag(sideA, side.Members.Count, beat.IncomingIndex);
            var fresh = side.Members[incomingIndex];

            r.CrowdEnergyDelta = blind
                ? Rng(2, 6) * iMod * PerformerProfile.Blend(ctx.For(fresh).Connection, 0.5)
                : Rng(0, 2) * iMod;

            r.AdvantageDelta = (sideA ? 1 : -1) * Rng(4, 12) * iMod;
            r.TechnicalContribution = 1.5 * iMod * ctx.For(fresh).Workrate;
            r.StorytellingContribution = blind ? 4.0 * iMod : 1.5 * iMod;

            r.Commentary.Add(blind
                ? Pick(
                    $"A blind tag! {fresh.RingName} came in without {other.RingName} seeing a thing!",
                    $"{fresh.RingName} tags himself in behind {other.RingName}'s back — nobody saw that but the referee!")
                : Pick(
                    $"{fresh.RingName} tags in.",
                    $"A quick tag brings {fresh.RingName} into the match."));
        }

        /// <summary>
        /// Team offence. The one beat that reads off how well the pair work *together*
        /// rather than off either of them individually — which is what phase 4's team
        /// chemistry will attach to.
        /// </summary>
        private void ApplyDoubleTeam(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double dMod)
        {
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

            var side   = ctx.SideOf(control);
            var pOther = ctx.For(other);

            // Averaged across the team, not taken from the legal man: a double team is only
            // as good as the worse half of it.
            double combined = ctx.SideAvg(side, p => (p.Workrate + p.Athleticism) / 2.0);
            double weakestLink = side.Members.Min(m => ctx.For(m).Workrate);

            // The one beat that is genuinely about the pair rather than about either of
            // them. Two good singles wrestlers hit a double team competently; a team that
            // has done it two hundred times hits it in stereo. Spans 0.78–1.22, so it is
            // worth roughly half a skill grade in either direction.
            double chemistry = 0.78 + 0.44 * Math.Clamp(side.Chemistry, 0, 1);

            r.CrowdEnergyDelta = Rng(7, 14) * iMod * dMod
                                 * ctx.SideAvg(side, p => p.Connection) * chemistry;
            r.AdvantageDelta   = ControlSign(ctx, control) * Rng(14, 26) * iMod * dMod;

            r.TechnicalContribution = 7.0 * (AvgRingSkill(ctx) / 5.0) * iMod * dMod
                                      * combined * chemistry
                                      * PerformerProfile.Blend(weakestLink, 0.35)
                                      * PerformerProfile.Blend(pOther.Selling, 0.45);
            r.StorytellingContribution = 3.5 * iMod * dMod;

            r.Commentary.Add(side.Chemistry >= 0.6
                ? Pick(
                    $"Beautiful double-team move from {side.Name} — they have done that a thousand times.",
                    $"{side.Name} hit it in perfect stereo! {other.RingName} never had a chance.",
                    $"Textbook tandem offence from {side.Name}. That is what a team looks like.")
                : Pick(
                    $"{side.Name} go for a double team — it lands, but it is not pretty.",
                    $"A double team from {side.Name}. They got there in the end.",
                    $"{side.Name} try some tandem offence. You can see them thinking about it."));
        }

        /// <summary>
        /// The partners collide.
        ///
        /// Control is the side that makes the mistake, and the advantage moves *against*
        /// them — the only beat in the engine where that is true, which is why it is worth
        /// saying out loud rather than leaving to a sign convention.
        /// </summary>
        private void ApplyMiscommunication(BeatResult r, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod)
        {
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

            var side = ctx.SideOf(control);
            var pControl = ctx.For(control);

            // A drilled team colliding is a bigger story than two strangers doing it,
            // because it is a departure. It is also rarer, which is the booker's problem
            // rather than the engine's — nothing stops it being booked, it just costs the
            // side less when they were never in sync to begin with.
            double surprise = 0.75 + 0.50 * Math.Clamp(side.Chemistry, 0, 1);

            // Every other crowd delta in the engine scales by somebody's connection, and
            // this one did not — a mix-up between two nobodies popped exactly as hard as
            // one between two main-eventers.
            r.CrowdEnergyDelta = Rng(3, 8) * iMod * ctx.SideAvg(side, p => p.Connection);

            // Against the side that blundered, and harder the better drilled they were.
            r.AdvantageDelta = -ControlSign(ctx, control) * Rng(12, 24) * iMod * surprise;

            r.TechnicalContribution = 1.0 * iMod;
            r.StorytellingContribution = 6.5 * iMod * surprise
                                         * PerformerProfile.Blend(pControl.RingPsych, 0.40);

            string partner = side.Members.Count > 1
                ? side.PartnersOf(control).First().RingName
                : other.RingName;

            r.Commentary.Add(Pick(
                $"Disaster! {control.RingName} takes out {partner} by mistake — and the two of them are jawing at each other!",
                $"{control.RingName} and {partner} collide! There is trouble in that corner.",
                $"Miscommunication! {partner} is furious with {control.RingName}, and {other.RingName} is more than happy to watch.",
                $"That is not how they drew it up — {control.RingName} just wiped out his own partner."
            ));
        }

        /// <summary>
        /// The partner breaks up the pin. Extends a near-fall sequence, and wears out
        /// faster than almost anything else in the engine — by the third one the referee
        /// is being openly ignored.
        /// </summary>
        private void ApplySaveBreakup(BeatResult r, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, int timesUsed)
        {
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

            var side = ctx.SideOf(control);
            var saver = side.Members.Count > 1
                ? side.PartnersOf(control).First()
                : control;

            r.CrowdEnergyDelta = Rng(8, 15) * iMod
                                 * PerformerProfile.Blend(ctx.For(saver).Connection, 0.55);

            // A save stops the fall but does not put anyone on top.
            r.AdvantageDelta = ControlSign(ctx, control) * Rng(2, 8) * iMod;

            r.TechnicalContribution = 1.5 * iMod;
            r.StorytellingContribution = 5.5 * iMod;

            r.Commentary.Add(timesUsed >= 3
                ? Pick(
                    $"{saver.RingName} in AGAIN to break it up. The referee has lost control of this.",
                    $"Another save from {saver.RingName} — at this point nobody is even pretending there are rules.")
                : Pick(
                    $"{saver.RingName} dives in to break up the count at the last possible moment!",
                    $"THE SAVE! {saver.RingName} gets there just in time — {other.RingName} cannot believe it!",
                    $"{other.RingName} had it won — but {saver.RingName} was there to break it up!"));
        }

        /// <summary>
        /// Everyone in, referee has lost it. Resets the room before the finish rather than
        /// advancing anybody's position, so it swings no advantage at all.
        /// </summary>
        private void ApplyAllFourBrawl(BeatResult r, Ctx ctx, double iMod, double dMod)
        {
            r.CrowdEnergyDelta = Rng(6, 13) * iMod * dMod * ctx.Pair(p => p.Connection);
            r.AdvantageDelta   = 0;

            // The one beat where the whole side is genuinely working: validation requires
            // both sides to be teams and the beat's premise is that all four are in, so
            // the "man on the apron is not performing" rule has nothing to exclude.
            r.TechnicalContribution    = 2.5 * iMod * dMod * ctx.Pair(p => p.Workrate);
            r.StorytellingContribution = 4.0 * iMod * dMod;

            r.Commentary.Add(Pick(
                $"All four of them are in the ring now and the referee has completely lost control!",
                $"It has broken down! {ctx.Plan.SideA.Name} and {ctx.Plan.SideB.Name} are swinging at each other everywhere!",
                $"Bodies everywhere — the referee is just counting and hoping at this point!"
            ));
        }

        private void ApplyFinish(BeatResult r, MatchBeat beat, Ctx ctx,
            Wrestler? control, Wrestler other, double iMod, double feudMult)
        {
            control ??= ctx.LegalA;
            other = ctx.Opponent(control);

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

            // Volume is not the whole of it. A room can be loud and gone — a hijacked crowd
            // is very loud indeed — and it can be quiet and completely present, which is
            // what the near-tag and the count before a kick-out are for.
            //
            // Investment scales the crowd component between InvestmentFloor and 1. It never
            // reaches zero, because a match worked in front of a dead room is still worth
            // its technical and storytelling scores; what it loses is the third of the
            // grade that was supposed to be about the audience.
            //
            // docs/wrestling-reference/16-crowd-psychology.md §2.1: promotions consistently
            // over-fear boos and under-fear silence. This is where the engine stops doing
            // the same thing — heat counts as engagement, and silence is what costs.
            // Centred, not a penalty. CrowdCeiling already scales the whole crowd axis by
            // how much the audience cares about this pairing, so applying investment as a
            // straight multiplier charged low connection twice and — worse — compressed
            // every difference that lives in the crowd component, which is most of the
            // engine's discrimination. Six tests measuring quite different things all
            // failed at once, which is what double-counting looks like.
            //
            // A typical match sits near TypicalInvestment and comes out at 1.0. What moves
            // is the tails: a room that never turned up loses about a third of its crowd
            // component, and one that was present all night gains about a quarter.
            double investment = state.Reaction.Investment;
            double crowdComponent = crowdNorm * 100 * crowdWeight
                                    * Math.Clamp(
                                        1.0 + (investment - TypicalInvestment) * InvestmentSwing,
                                        0.65, 1.06);

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

            // Who actually scored the fall. In singles this is the same person the plan
            // booked to win; in a tag match it is whoever was legal for the winning side
            // when the finish landed, which depends on the tags booked along the way and
            // so can only be known here.
            var winningSide = plan.BookedWinningSide!;
            var losingSide  = plan.BookedLosingSide!;

            return new MatchEngineResult
            {
                Winner             = ctx.LegalOf(winningSide),
                Loser              = ctx.LegalOf(losingSide),
                WinningSide        = winningSide.Members.ToList(),
                LosingSide         = losingSide.Members.ToList(),
                BeatResults        = beatResults,
                TechnicalScore     = state.TechnicalScore,
                StorytellingScore  = state.StorytellingScore,
                CrowdPeakEnergy    = state.CrowdPeakEnergy,
                CrowdAverageEnergy = state.CrowdAverage,
                FinishQuality      = state.FinishQuality,
                MatchTypeCoherence = coherence,
                Reaction           = state.Reaction,
                Familiarity        = ctx.Familiarity,
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
                or BeatType.FinishSubmission or BeatType.FinishClean
                // Quick tags and a methodical isolation are technical tag work.
                or BeatType.Tag or BeatType.Isolation,

            // Spectacle: get them early, keep them loud, finish emphatically.
            MatchTypeEnum.Spotfest => t is BeatType.HotOpening or BeatType.StandardOpening
                or BeatType.HighSpot or BeatType.CrowdBrawl or BeatType.NearFall or BeatType.Comeback
                or BeatType.FinishClean or BeatType.FinishSuperFinisher or BeatType.FinishRollup
                // Tandem offence and the breakdown are spectacle; so is the tag itself.
                or BeatType.DoubleTeam or BeatType.AllFourBrawl or BeatType.Shine
                or BeatType.HotTag or BeatType.BlindTag,

            // Character and grudge work, including the finishes that leave a story running.
            MatchTypeEnum.Storytelling => t is BeatType.SlowOpening or BeatType.StandardOpening
                or BeatType.PsychologicalWarfare or BeatType.RevengeSpot
                or BeatType.FeudalEscalation or BeatType.ThirdPartyPullIn or BeatType.AlliesRejected
                or BeatType.Comeback or BeatType.NearFall
                or BeatType.FinishClean or BeatType.FinishInterference or BeatType.FinishDQ
                // The tag formula is the most story-driven structure the engine has: a man
                // kept from his corner, the tag denied, and the release. Leaving these out
                // meant declaring a Southern Tag "Storytelling" was a pure penalty.
                or BeatType.Cutoff or BeatType.Isolation or BeatType.NearTag or BeatType.HotTag
                or BeatType.Miscommunication or BeatType.SaveBreakup,

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

        /// <summary>
        /// Ring skill of the two performers actually working. Only ever feeds Technical,
        /// so it reads the legal men rather than the whole side.
        /// </summary>
        private double AvgRingSkill(Ctx ctx) =>
            ctx.LegalPairStat(w => w.RingSkills.GetOverallSkill());

        private double Rng(double min, double max) =>
            min + _rand.NextDouble() * (max - min);

        private string Pick(params string[] options) =>
            options[_rand.Next(options.Length)];
    }
}
