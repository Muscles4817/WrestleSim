using WrestlingSim.Enums;
using WrestlingSim.Models.World;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Models.MatchPlan
{
    public class MatchPlan
    {
        /// <summary>
        /// The sides. A singles match is two sides of one, a tag match two sides of two, a
        /// triple threat three sides of one — which is why there is no separate plan type
        /// for any of them.
        ///
        /// <see cref="SideA"/> and <see cref="SideB"/> are shims over the first two, exactly
        /// as <see cref="WrestlerA"/>/<see cref="WrestlerB"/> are shims over their starters.
        /// The same reasoning applies one level up: almost everything the engine asks is
        /// side-level, and "who is on top" does not become a different question with a third
        /// side in the match — but *some things do*, and those are the whole of what a
        /// multi-man match is. See <see cref="MatchFormat"/>.
        ///
        /// Two sides is the default so every existing plan, save file and test keeps working
        /// unchanged; a plan is only multi-man if somebody adds a third side.
        /// </summary>
        public List<MatchSide> Sides { get; init; } = [new(), new()];

        /// <summary>The first side. Shim over <see cref="Sides"/>.</summary>
        public MatchSide SideA
        {
            get => Sides[0];
            init { while (Sides.Count < 1) Sides.Add(new()); Sides[0] = value; }
        }

        /// <summary>The second side. Shim over <see cref="Sides"/>.</summary>
        public MatchSide SideB
        {
            get => Sides[1];
            init { while (Sides.Count < 2) Sides.Add(new()); Sides[1] = value; }
        }

        /// <summary>
        /// What kind of match this is, structurally — which is not the same question as
        /// <see cref="MatchType"/>, which is how it is *worked* (technical, spotfest…).
        ///
        /// Derived rather than stored, because the format is a fact about the sides and
        /// storing it would let a plan disagree with itself.
        /// </summary>
        public MatchFormat Format => Sides.Count switch
        {
            <= 2 => MatchFormat.TwoSided,
            3    => MatchFormat.TripleThreat,
            _    => MatchFormat.MultiWay
        };

        /// <summary>True when more than two sides can win — the third-man problem applies.</summary>
        public bool IsMultiMan => Sides.Count > 2;

        /// <summary>The wrestler who starts for side A. Shim; see <see cref="SideA"/>.</summary>
        public Wrestler WrestlerA
        {
            get => SideA.Members.Count > 0
                ? SideA.Starter
                : throw new InvalidOperationException("Side A has no members.");
            init => SideA.Members.Add(value);
        }

        /// <summary>The wrestler who starts for side B. Shim; see <see cref="SideB"/>.</summary>
        public Wrestler WrestlerB
        {
            get => SideB.Members.Count > 0
                ? SideB.Starter
                : throw new InvalidOperationException("Side B has no members.");
            init => SideB.Members.Add(value);
        }

        /// <summary>Everyone in the match, in side order.</summary>
        public IEnumerable<Wrestler> AllParticipants => Sides.SelectMany(s => s.Members);

        /// <summary>True when any side has a partner on the apron.</summary>
        public bool IsTagMatch => Sides.Any(s => s.IsTag);

        public List<MatchBeat> Beats { get; set; } = new();

        /// <summary>
        /// Every live story among the people in this match.
        ///
        /// A two-side match has at most one and the singular <see cref="Feud"/> shim below
        /// is all anybody needed. A multi-man match routinely has more than one, and the
        /// story that decides the finish is often between two people who are both about to
        /// lose to the third — which is the entire reason
        /// <see cref="Engine.FeudBook.Among"/> exists.
        /// </summary>
        public List<Feud> Feuds { get; set; } = new();

        /// <summary>The headline feud. Shim over <see cref="Feuds"/>.</summary>
        public Feud? Feud
        {
            get => Feuds.FirstOrDefault();
            set
            {
                Feuds.Clear();
                if (value is not null) Feuds.Add(value);
            }
        }

        /// <summary>
        /// The live story that puts these two on opposite sides in this match, if there is
        /// one. Prefers a story that is actually about the pair over a larger one that
        /// merely contains them.
        /// </summary>
        public Feud? FeudBetween(Wrestler a, Wrestler b) =>
            Feuds.Where(f => f.Opposes(a, b))
                 .OrderBy(f => f.Participants.Count())
                 .ThenByDescending(f => f.Heat)
                 .FirstOrDefault();

        /// <summary>
        /// The booker declaring that this match ends the feud.
        ///
        /// A declaration rather than something derived from the beats, because that is what
        /// it is in real booking: nothing about a match's shape makes it a blow-off, and
        /// the same beats are a blow-off or another chapter depending on whether anybody
        /// decided the story was over. Doc 20 §6 is the standard — a blow-off has to
        /// *resolve*, has to be *proportional* to what was built, and is the point at which
        /// the debt is settled.
        ///
        /// The cost of declaring one is that it is spent: the feud ends, its heat goes to
        /// zero, and the pairing has to be built again from nothing. The cost of never
        /// declaring one is <see cref="Models.MatchPlan.Feud.Distrust"/>.
        /// </summary>
        public bool IsBlowOff { get; set; }

        public MatchType MatchType { get; set; } = MatchType.Standard;

        /// <summary>
        /// The championship on the line, or null for a non-title match.
        ///
        /// A title creates automatic stakes for any match involving it
        /// (docs/wrestling-reference/21-championships.md §1.1), so this changes what the
        /// crowd brings to the opening bell, what the winner takes away, and — via
        /// <see cref="Engine.TitleEconomy"/> — what the belt itself is worth afterwards.
        /// </summary>
        public Title? TitleAtStake { get; set; }

        /// <summary>
        /// True when the belt is on the line and the champion is one of the people in it.
        /// A vacant title is contested by both, which is also a title match.
        /// </summary>
        public bool IsTitleMatch => TitleAtStake != null;

        // ── Derived / validation ─────────────────────────────────────────────

        /// <summary>The side the finish beat is booked for, or null if there is no finish.</summary>
        public MatchSide? BookedWinningSide
        {
            get
            {
                var finish = Beats.LastOrDefault(b => b.IsFinish);
                if (finish == null) return null;
                return ControlSide(finish);
            }
        }

        /// <summary>
        /// The side that takes the fall.
        ///
        /// With two sides, whoever did not win. With more, the finish has to say — see
        /// <see cref="MatchBeat.Against"/>. Null when it cannot be determined, which
        /// <see cref="Validate"/> refuses to let a bookable plan reach.
        /// </summary>
        public MatchSide? BookedLosingSide
        {
            get
            {
                var winning = BookedWinningSide;
                if (winning is null) return null;

                if (Beats.LastOrDefault(b => b.IsFinish)?.Against is { } pinned
                    && SideIndex(pinned) is { } pi && pi < Sides.Count)
                    return Sides[pi];

                var others = Sides.Where(s => s != winning).ToList();
                return others.Count == 1 ? others[0] : null;
            }
        }

        /// <summary>Everyone who did not win — which in a multi-man match is more than one side.</summary>
        public IEnumerable<MatchSide> BookedNonWinningSides =>
            BookedWinningSide is { } w ? Sides.Where(s => s != w) : [];

        /// <summary>
        /// Winner inferred from the finish beat's Control.
        ///
        /// For a tag match this reports the side's *starter*, because a plan on its own
        /// does not know who will be legal when the finish lands — that depends on the
        /// tags booked in between, which only the engine resolves. The engine reports the
        /// person who actually scored the fall as
        /// <see cref="MatchEngineResult.Pinner"/>.
        /// </summary>
        public Wrestler? BookedWinner => BookedWinningSide?.Starter;

        public Wrestler? BookedLoser => BookedLosingSide?.Starter;

        /// <summary>
        /// Returns validation errors. Empty list = plan is valid to execute.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            // ── Sides ────────────────────────────────────────────────────────
            // Checked first and returned on, because everything below reads people out
            // of the sides and would throw rather than report.
            if (Sides.Count < 2) errors.Add("A match needs at least two sides.");
            if (Sides.Count > 4)
                errors.Add($"A match has at most four sides; this one has {Sides.Count}.");
            for (int i = 0; i < Sides.Count; i++)
                if (Sides[i].Members.Count == 0)
                    errors.Add($"Side {(char)('A' + i)} has nobody in it.");
            if (errors.Count > 0) return errors;

            // The engine indexes Members with the raw StartingIndex, so an out-of-range
            // value has to be caught here rather than quietly clamped — otherwise this
            // method reports a plan as valid that Execute then throws on.
            foreach (var (side, label) in new[] { (SideA, "A"), (SideB, "B") })
                if (side.StartingIndex < 0 || side.StartingIndex >= side.Members.Count)
                    errors.Add(
                        $"Side {label} starts with member {side.StartingIndex}, but it has " +
                        $"{side.Members.Count}.");

            if (errors.Count > 0) return errors;

            // Handicap is a real booking, and this is not a rule about taste. The engine
            // has no term for numbers at all: advantage never moves for being outnumbered,
            // and a 1v4 grades identically to a 1v1. Meanwhile side-weighting means the
            // side with the *extra* man scores slightly worse. So a handicap match would
            // not be graded generously or harshly — it would be graded as something else
            // entirely. A 2v1 also cannot be saved, because SaveSerializer refuses any
            // plan where IsTagMatch is true.
            //
            // The exit condition for this rule is a numbers term in the engine, not a
            // decision that handicap is allowed.
            if (Sides.Select(s => s.Size).Distinct().Count() > 1)
                errors.Add(
                    $"Sides are uneven ({string.Join(" v ", Sides.Select(s => s.Size))}). The " +
                    "engine has no model for a numbers advantage yet, so a handicap match " +
                    "would be graded as a normal one.");

            // And the mirror of it for the other direction. A pin break with nobody left to
            // break the pin is not a beat with a missing target, it is a beat about a
            // situation the match cannot be in.
            foreach (var beat in Beats.Where(b => b.IsMultiManBeat))
                if (!IsMultiMan)
                    errors.Add($"{beat.Type} needs a third party in the match — there is " +
                               "nobody to dispose of or steal from in a two-sided one.");

            // Every beat has to be workable by the side it is booked for. This is the rule
            // that actually protects the tag formula — a hot tag needs someone to tag, and
            // an isolation needs a corner to be kept away from. It subsumes the size check
            // above and will outlive it.
            foreach (var beat in Beats.Where(b => b.IsTagBeat))
            {
                // The one tag beat that belongs to nobody: everybody is in the ring, so
                // Even is the right reading of it and both sides have to be teams.
                if (beat.Type == BeatType.AllFourBrawl)
                {
                    if (!SideA.IsTag || !SideB.IsTag)
                        errors.Add("Everybody In needs a partner on both sides.");
                    continue;
                }

                var side = ControlSide(beat);
                if (side is null)
                {
                    errors.Add($"{beat.Type} has to be booked for one side or the other.");
                    continue;
                }

                // Which side actually needs a partner depends on the beat, and getting this
                // backwards is easy: for an isolation or a denied tag, control is the side
                // doing the isolating, but the man who needs a corner to be kept away from
                // is their *opponent*. Everything else needs the controlling side to be a
                // team, because they are the ones tagging or double-teaming.
                var needsPartner = beat.Type is BeatType.Isolation or BeatType.NearTag
                    ? Opposing(side)
                    : side;

                if (!needsPartner.IsTag)
                    errors.Add(
                        $"{beat.Type} needs a partner on {needsPartner.Name}'s side, and " +
                        "there is nobody on the apron.");
                else if (beat.IncomingIndex is { } incoming
                         && (incoming < 0 || incoming >= side.Size))
                    errors.Add(
                        $"{beat.Type} tags in member {incoming}, but {side.Name} has {side.Size}.");
            }

            // Walking the tags. Who is legal at any point is fully determined by the
            // starting indices and the tag beats before it, so a tag that brings in the man
            // who is already in the ring can be caught here rather than silently
            // substituting the next man round — invisible on a two-man side, a different
            // match on a trio.
            {
                int legalA = SideA.StartingIndex, legalB = SideB.StartingIndex;

                foreach (var beat in Beats.Where(b => b.IsTagChange))
                {
                    var side = ControlSide(beat);
                    if (side is null || !side.IsTag) continue;

                    bool isA = side == SideA;
                    int current = isA ? legalA : legalB;
                    int next = beat.IncomingIndex is { } i && i >= 0 && i < side.Size
                        ? i
                        : (current + 1) % side.Size;

                    if (next == current)
                        errors.Add(
                            $"{beat.Type} tags in {side.Members[next].RingName}, who is already " +
                            "the legal man.");

                    if (isA) legalA = next; else legalB = next;
                }
            }

            // A wrestler on both sides breaks every "which side is this person on" lookup
            // in the engine, and is not a booking anybody meant to make.
            foreach (var w in SideA.Members.Where(SideB.Contains))
                errors.Add($"{w.RingName} is booked on both sides of the match.");

            foreach (var side in new[] { SideA, SideB })
                if (side.Members.Count != side.Members.Distinct().Count())
                    errors.Add("A wrestler is booked twice on the same side.");

            // A blow-off ends a story. Declaring one where there is no story is not a
            // booking the engine can price, and silently treating it as an ordinary match
            // would hide the mistake rather than report it.
            if (IsBlowOff)
            {
                if (Feud is null)
                    errors.Add("A blow-off has to end a feud, and no feud is attached to this match.");
                else if (Feud.Concluded)
                    errors.Add($"{Feud.SideAName} vs {Feud.SideBName} has already been blown off — that story is over.");
            }

            if (!Beats.Any())
                errors.Add("Plan has no beats.");

            if (!Beats.Any(b => b.IsOpening))
                errors.Add("Plan has no opening beat.");

            var finishBeats = Beats.Where(b => b.IsFinish).ToList();
            if (finishBeats.Count == 0)
                errors.Add("Plan has no finish beat.");
            else if (finishBeats.Count > 1)
                errors.Add("Plan has more than one finish beat.");
            else if (!Beats.Last().IsFinish)
                errors.Add("Finish beat must be the last beat.");
            else if (SideIndex(finishBeats[0].Control) is not { } finishSide
                     || finishSide >= Sides.Count)
                // BookedWinner reads the finish's Control, so Even/Contested silently resolved
                // to WrestlerB while the engine's commentary credited WrestlerA. A finish has
                // to say who won — and in a multi-man match it has to name a side that is
                // actually in the match.
                errors.Add($"Finish beat must be controlled by one of the {Sides.Count} sides " +
                           "— a finish decides who wins.");

            // ── Title ────────────────────────────────────────────────────────
            if (TitleAtStake is { } title)
            {
                if (title.Retired)
                    errors.Add($"{title.Name} has been retired and cannot be defended.");

                // A champion who is not in the match cannot lose the belt in it, so this
                // is a non-title match with a misleading label rather than a title match.
                else if (title.Champions.FirstOrDefault(c => !AllParticipants.Contains(c)) is { } absent)
                    errors.Add(
                        $"{title.Name} cannot be on the line here — {absent.RingName} holds it " +
                        "and is not in this match.");

                // A tag belt is held and lost jointly, so it needs teams to contest it.
                else if (title.IsTagTitle && (SideA.Size < title.SideSize || SideB.Size < title.SideSize))
                    errors.Add(
                        $"{title.Name} is a {title.SideSize}-person title and cannot be " +
                        "defended in a singles match.");

                else if (!title.IsTagTitle && IsTagMatch)
                    errors.Add(
                        $"{title.Name} is a singles title and cannot be defended in a tag match.");
            }

            // Feud-gated beats require an active feud
            foreach (var beat in Beats)
            {
                if (beat.Type == BeatType.FeudalEscalation && (Feud == null || Feud.Intensity < FeudIntensity.Building))
                    errors.Add($"FeudalEscalation requires a feud of at least Building intensity.");

                if (beat.Type == BeatType.ThirdPartyPullIn && (Feud == null || (!Feud.HasTag(FeudHistoryTag.FamilyInvolved) && !Feud.HasTag(FeudHistoryTag.ManagerConflict))))
                    errors.Add($"ThirdPartyPullIn requires feud history tag FamilyInvolved or ManagerConflict.");

                if (beat.Type == BeatType.AlliesRejected)
                {
                    var beatIndex = Beats.IndexOf(beat);
                    bool hasPriorPullIn = Beats.Take(beatIndex).Any(b => b.Type == BeatType.ThirdPartyPullIn);
                    if (!hasPriorPullIn)
                        errors.Add("AlliesRejected requires a ThirdPartyPullIn earlier in the plan.");
                }
            }

            // ── Multi-man rules (doc 18 §2.5) ────────────────────────────────
            if (IsMultiMan)
            {
                // "No disqualification, no count-out, first fall wins." Not a house rule —
                // it is what makes the format work: with three people there is no way to
                // enforce a count on two of them at once, so the format drops the rule
                // rather than pretending.
                var finish = Beats.LastOrDefault(b => b.IsFinish);
                if (finish is not null &&
                    finish.Type is BeatType.FinishDQ or BeatType.FinishCountout)
                    errors.Add($"A {Sides.Count}-way has no disqualification or count-out — " +
                               "first fall wins. Book a clean finish, a submission, or " +
                               "interference.");

                // The fall has to name who took it. Without this the format's one real
                // booking tool — beating a champion without beating the champion — cannot
                // be expressed, and the loss would land on whoever happened to be listed.
                if (finish is not null)
                {
                    if (finish.Against is null)
                        errors.Add($"A {Sides.Count}-way finish has to say who takes the fall, " +
                                   "not just who wins — that is the whole point of the format.");
                    else if (SideIndex(finish.Against.Value) is not { } pi || pi >= Sides.Count)
                        errors.Add("The side booked to take the fall is not in this match.");
                    else if (SideIndex(finish.Control) == pi)
                        errors.Add("The winner cannot also be the one pinned.");
                }

                // Every other beat may name a target too, and now that the builder can set
                // one, the two ways of naming an impossible one have to be caught here
                // rather than resolved into a name. A beat aimed at a side that is not in
                // the match falls through to the engine's rotation and quietly narrates
                // somebody else; a beat aimed at the side working it is a wrestler doing
                // something to themselves, which is the exact sentence this work has spent
                // two review rounds removing.
                foreach (var (beat, i) in Beats.Select((b, i) => (b, i)))
                {
                    if (beat.IsFinish || beat.Against is not { } against) continue;

                    if (SideIndex(against) is not { } ai || ai >= Sides.Count)
                        errors.Add($"Beat {i + 1} is aimed at somebody who is not in this match.");
                    else if (SideIndex(beat.Control) == ai)
                        errors.Add($"Beat {i + 1} is aimed at the side working it — nobody " +
                                   "runs a spot on themselves.");
                }

                if (IsTagMatch)
                    errors.Add("Multi-man matches are one wrestler a side for now. Trios are " +
                               "booked as two sides of three, which is a different match.");
            }

            return errors;
        }

        /// <summary>
        /// The side index a control value names, or null for Even / Contested.
        ///
        /// **The only place this mapping exists.** Everything that needs to turn a
        /// <see cref="BeatControl"/> into a side goes through here, so adding a fifth side
        /// later is one edit rather than a hunt through a hundred `== WrestlerA` comparisons.
        /// </summary>
        public static int? SideIndex(BeatControl control) => control switch
        {
            BeatControl.WrestlerA => 0,
            BeatControl.WrestlerB => 1,
            BeatControl.SideC     => 2,
            BeatControl.SideD     => 3,
            _                     => null
        };

        /// <summary>The control value naming a given side index.</summary>
        public static BeatControl ControlFor(int sideIndex) => sideIndex switch
        {
            0 => BeatControl.WrestlerA,
            1 => BeatControl.WrestlerB,
            2 => BeatControl.SideC,
            3 => BeatControl.SideD,
            _ => throw new ArgumentOutOfRangeException(nameof(sideIndex),
                     $"No control value for side {sideIndex}; a match has at most four sides.")
        };

        /// <summary>The side a beat's control refers to, or null for Even / Contested.</summary>
        public MatchSide? ControlSide(MatchBeat beat) =>
            SideIndex(beat.Control) is { } i && i < Sides.Count ? Sides[i] : null;

        /// <summary>Which side this wrestler is on, or null if they are not in the match.</summary>
        public MatchSide? SideOf(Wrestler w) => Sides.FirstOrDefault(s => s.Contains(w));

        /// <summary>The side opposing the one given.</summary>
        public MatchSide Opposing(MatchSide side) => side == SideA ? SideB : SideA;

        /// <summary>
        /// Resolves which wrestler is "Control" for a given beat.
        ///
        /// Reports the controlling side's starter. During execution the engine resolves
        /// this against whoever is currently legal instead — see
        /// <see cref="Engine.MatchEngineState.LegalIndexFor"/>.
        /// </summary>
        public Wrestler? ControlWrestler(MatchBeat beat) => ControlSide(beat)?.Starter;

        public Wrestler OtherWrestler(Wrestler w) =>
            SideA.Contains(w) ? SideB.Starter : SideA.Starter;
    }
}
