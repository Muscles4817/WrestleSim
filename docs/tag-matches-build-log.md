# Tag matches — build log

Running record of the build described in [tag-matches-plan.md](tag-matches-plan.md): what each
phase actually changed, what an independent review found, and how disagreements were settled.

Each phase is built, reviewed by a second agent that did not write the code, and — where the
review and the author disagree — adjudicated by a third. Contentions and their rulings are
recorded here rather than resolved silently, so the reasoning survives the session.

**Baseline before phase 1:** `53e0233`, 321 tests passing, CI green.

> A note on the numbers: the plan originally said "285 tests". That was a count of `[Fact]` /
> `[Theory]` *attributes*; the suite actually runs 321 cases because `[Theory]` expands. The
> plan has been corrected. All figures below are actual test cases.

---

## Phase 1 — Sides abstraction

**Goal:** the engine reasons about sides rather than two named people, with singles behaviour
unchanged and no new beats, UI or persistence.

### What changed

| File | Change |
|---|---|
| `Models/MatchPlan/MatchSide.cs` | New. `Members`, `StartingIndex`, `Starter`, `Name`, `PartnersOf`, `Of(...)`. |
| `Models/MatchPlan/MatchPlan.cs` | `SideA`/`SideB` as the real storage; `WrestlerA`/`WrestlerB` become init shims over them. Adds `AllParticipants`, `IsTagMatch`, `BookedWinningSide`/`BookedLosingSide`, `SideOf`, `Opposing`, `ControlSide`. Validation gains empty-side, both-sides and duplicate-member checks. |
| `Engine/MatchEngineState.cs` | Live legal-performer indices (`LegalA`/`LegalB`, `InitialiseLegal`). Deliberately here and not on `MatchSide`, because a plan is a booking that can be executed more than once. |
| `Engine/MatchEngine.cs` | `Ctx` holds a `Wrestler → PerformerProfile` dictionary instead of two fields; `A`/`B` become the *legal* performers' profiles. Aggregates (`Pair`, `PairStat`, `SideAvg`, `AvgRingSkill`, opening-bell overness and disposition) are side-averaged. `ControlSign` asks which side a performer is on. `BuildResult` reports the legal performer at the finish plus both full sides. |
| `Models/MatchPlan/MatchEngineResult.cs` | Adds `WinningSide`, `LosingSide`, `Pinner`, `Pinned`, `WasTagMatch`. `Winner`/`Loser` retained and now documented as "who took the fall". |
| `Models/BookedMatch.cs` | `Name` uses side names; `Wrestlers` returns everyone. |
| `Tests/MatchSideTests.cs` | New. 12 cases covering shims, validation, 2v2 execution, side aggregation, and singles equivalence. |

### Two hazards in the old two-field context

Recording them because neither was in the plan. **Corrected after review** — the original
version of this section called both "latent bugs the refactor surfaced", which overstates the
second one:

1. **`Ctx.For` returned the wrong profile silently.** It was
   `w == Plan.WrestlerA ? A : B` — any unrecognised wrestler got side B's profile rather than
   an error. Now a dictionary lookup that throws. This one *was* reachable: the degenerate
   same-instance booking below hit it.
2. **`ControlSign` was `ReferenceEquals(control, Plan.WrestlerA)`.** For a side-A *partner*
   this is false, so a beat booked for side A would swing advantage to side B. This is
   **preparatory work, not a fix**: in phase 1 the legal performer is always the side's
   starter and `Plan.WrestlerA` returns exactly `SideA.Starter`, so the old and new
   expressions are provably equivalent. Review confirmed it empirically — reverting
   `ControlSign` leaves all tests green. It becomes reachable in phase 2, when a hot tag
   first makes a partner legal, and that is when a test can pin it.

### Deviation from the plan: one test was modified

The plan committed to all 321 tests passing **unmodified**. One did not, and it is worth being
precise about why rather than quietly relaxing it.

`RosterDifferentiationTests.Charisma_MattersOnEveryStructure_NotJustOnesWithTalkingBeats`
called `Mean(dull, dull, ...)` — *the same `Wrestler` object on both sides* — as a way of
isolating one attribute. The new validation rejects that booking, and correctly: with one
instance in both corners, `Ctx.For` cannot say which profile to use and `IsSideA` is true for
everyone, so `ControlSign` returned `+1` for **every** beat — the advantage axis was pinned in
one direction and saturated, so the face-in-peril structure never put anyone in peril.

The fix builds two distinct instances with identical stats (`Dull A` / `Dull B`). It still
passes, with the same threshold.

**Corrected after review.** This section originally claimed the old test "was measuring a
match in which no structure worked as booked". That is true of the *advantage axis* and false
of the *star rating the test actually asserts on*. Review ran both idioms against the old
engine and the measured charisma deltas are identical to three decimals:

| structure | same-instance delta | two-instance delta |
|---|---|---|
| TV Formula | 0.6386 | 0.6386 |
| Face-in-Peril | 0.7769 | 0.7768 |
| Technical Showcase | 0.8159 | 0.8160 |
| Big Match Epic | 0.8842 | 0.8836 |

So the change was *forced* (the booking is invalid under any sides model) and correct, but it
did not rescue a broken measurement. The threshold is `> 0.15`, so there was 4–6× headroom
either way. Justifying the deviation with the stronger claim was wrong and is withdrawn.

### Review — round 1

Reviewed by an independent agent that did not write the code. It verified the
singles-unchanged claim far more strongly than the test suite does: it built the same harness
against the parent commit and the phase 1 commit and diffed raw output from **10,246 singles
matches** — every roster pair × every structure × 4 match types × varied familiarity, plus 966
hand-built plans covering every `BeatType`, every `BeatControl` including Even/Contested, and
every finish type. Each row carried star rating, component scores, per-beat deltas in
round-trip format **and the full commentary text**, which is the RNG-draw-order canary since
`Pick` consumes the same `Random`. Output was byte-identical.

That is the phase's central contract, and it is met — not "the tests still pass" but "no
singles match anywhere produces a different number or a different word".

**Accepted and fixed:**

| # | Finding | Fix |
|---|---|---|
| 1 | `StartingIndex` out of range passed `Validate()` then crashed `Execute` — `MatchSide.Starter`'s `Math.Clamp` was masking the disagreement. | Clamp removed; `Starter` throws a described error; `Validate()` bounds-checks `StartingIndex` on both sides. Three regression tests, including one asserting the general property that anything which validates also executes. |
| 2 | The `ControlSign` test could not fail — reverting the fix left all 333 tests green. | Test renamed to `EveryBeatBookedForOneSide_LeavesThatSideAhead` and now states what it actually pins, with the scope limit written down. The real test arrives in phase 2. Build log corrected above. |
| 3 | `SaveSerializer` silently dropped every partner — a booked 2v2 came back off disk as a singles match, no error. | `ToDto` now refuses a tag plan with a `NotSupportedException` naming save v3. Test added. |
| 4 | `MatchPlan` lost its `required` members, so a half-built plan threw `ArgumentException: '0' cannot be greater than -1` from `Math.Clamp` rather than anything meaningful. | Fixed by the same change as finding 1 — `Starter` now reports empty sides properly. |

**Referred to adjudication** (findings 5 and 7) — see below.

**Noted, not actioned:** `LegalIndexFor`, `SideOf` and `Opposing` have no call sites yet
(phase 2 scaffolding); `MatchScreen.razor` and `MatchBookingFlow.cs` still render
`WrestlerA vs WrestlerB` directly instead of `BookedMatch.Name` (phase 3 territory, but a good
illustration of the risk of a compatibility shim — those sites *look* fine rather than failing
to compile); `Pinner`/`Pinned` read oddly for DQ and count-out finishes.

### Result

**337 tests passing** (321 existing + 16 new), full suite, Release.

### Adjudication — phase 1 contentions

Two review findings were design disagreements rather than defects, and went to a third agent.
Both rulings went against the author, and the second found something neither of the first two
agents had seen.

#### Q1 — Should uneven sides be blocked? **Ruled: block.**

The author argued for allowing it, citing the repo's own precedent: `Division.cs` documents
that a cross-division match is *warned about, not blocked*, "because intergender matches are
a legitimate booking decision, just an unusual one."

The ruling rejected that precedent for a specific reason. A cross-division match is scored
**correctly** — the engine has no gender term, so an intergender match genuinely is just a
match, and the notice is about booking taste. A handicap match is not unmodelled, it is
**mis-modelled with the sign inverted**. Measured:

| booking | stars |
|---|---|
| Star vs B1 (1v1) | 3.184 |
| Star & Weak vs B1 (2v1) | 2.753 |
| Star vs Weak (1v1) | 2.478 |
| Star vs Weak & Weak2 (1v2) | 2.478 |
| Star vs Weak ×4 (1v4) | 2.478 |

Adding a man to your side makes your match monotonically *worse*; being outnumbered four to
one moves the rating by 0.000. You warn about a booking the engine handles. You block one the
engine mis-handles. A second, independent reason settles it anyway: a 2v1 satisfies
`IsTagMatch`, so it validates clean, executes, and then destroys the player's save on the
serializer guard added above.

Implemented, with the error text naming the missing *mechanic* rather than the size rule, so
it reads as a testable exit condition rather than "handicap is not a feature yet". The ruling
also directed that phase 2 must **not** simply delete it, but supersede it with the rule that
actually protects the formula — *every beat must be workable by the side it is booked for* —
which subsumes it and catches a case the blunt size rule would miss.

#### Q2 — Whole-side or legal-performer aggregates? **Ruled: both agents were asking the wrong question.**

On the axis itself the author was upheld, but told to restate the rule one level higher.
"Crowd-level vs execution-level" is ambiguous the moment a craft stat feeds a crowd number.
The rule is now stated on **the field being assigned** — see the plan §1, and the comment
block on `Ctx`. Applying it exposed that the author had side-averaged *uniformly*: every
crowd-facing site was correct and roughly a dozen craft sites were not. A jobber added to the
star's side was moving the technical accumulator from 24.6 to 22.1 despite never touching
anybody. Those are now on `LegalPair`/`LegalPairStat`, and the four variables that feed both a
crowd and a craft field are computed twice.

The finding neither earlier agent made is about the aggregation *function*, and it invalidated
both proposed answers equally:

| aggregation | star legal | jobber legal | phase-2 proxy |
|---|---|---|---|
| side mean (author's) | 2.753 | 1.905 | **2.329** |
| legal only (plan's) | 3.184 | 1.455 | **2.320** |
| flat mean of their two singles matches | — | — | 2.320 |
| top-weighted (implemented) | 2.973 | 2.128 | **2.550** |

Under either proposal, a star-and-jobber tag match grades as **exactly the average of the
star's match and the jobber's match** — the star losing precisely what the jobber gains,
symmetric to within noise. That is a conservation law, not a wrestling model, and it directly
contradicts the standing reference: doc 12 §3.2 calls a top star working with a weaker man
"the single most effective star-making tool that exists", and doc 17 §2.8 says proximity
transfers heat *to* the weaker party.

Fixed with `Ctx.DragWeight` (0.5): a side reads toward its strongest member, so a partner
nobody knows dilutes the team without halving it, and the star carries. Named as a constant
because phase 4's team chemistry should lower it for an established team — a veteran team
reads to a crowd as one act rather than two people. Test: `AStarCarriesAWeakPartner_...`.

Also fixed: a comment on `bothOverBonus` that still described behaviour the code had stopped
having, and `FadeFactor`'s deliberate whole-side read is now documented where it lives.

---

## Phase 2 — The narrative machine

**Goal:** a tag match that is not a singles match with four names on it.

### What changed

| File | Change |
|---|---|
| `Enums/BeatEnums.cs` | Eleven tag beat types: `Shine`, `Cutoff`, `Isolation`, `NearTag`, `HotTag`, `Tag`, `BlindTag`, `DoubleTeam`, `Miscommunication`, `SaveBreakup`, `AllFourBrawl`. |
| `Engine/MatchEngineState.cs` | `Tag()` swaps the legal performer and clears that side's charge; `RecordIsolation`/`RecordNearTag` build it. Charge is credited to the side being *worked over*, not the side working. |
| `Models/MatchPlan/MatchBeat.cs` | `IncomingIndex` (who comes in), `IsTagBeat`, `IsTagChange`. |
| `Engine/MatchEngine.cs` | Nine handlers plus `HotTagCharge`. The aggregation rework from adjudication. |
| `Models/MatchPlan/MatchPlan.cs` | Beat-legality validation; the `AllFourBrawl` exception; uneven-sides block. |
| `Engine/BeatLibrary.cs` | Eleven templates under a new `Tag` category, each with a booker tip naming the mechanic. |
| `Engine/MatchStructureLibrary.cs` | `Southern Tag`, `Formula Tag`, `Tag Sprint`; `MatchStructure.SideSize` and `ForSideSize()`. |
| `Tests/TagMatchTests.cs` | New, 18 cases. |

### The hot-tag charge, measured

`HotTagCharge(isolations, nearTags)` returns 0.55 with no isolation — deliberately the same
unearned-payoff penalty `ApplyFinish` charges for a finish momentum did not support — then
`1.0 + 0.55·min(iso,3)/3 + 0.45·min(near,2)/2`.

| spent | hot-tag pop | storytelling | rating |
|---|---|---|---|
| nothing | 20.75 | 16.40 | 2.301★ |
| 1 isolation | 44.45 | 38.06 | 2.944★ |
| 3 isolations | 55.76 | 53.39 | 3.079★ |
| 3 isolations + 2 denied tags | 66.61 | 74.33 | 3.209★ |
| 5 isolations | 51.62 | 59.64 | 3.105★ |

The last row is the point of the saturation: a fifth isolation buys a *quieter* tag than the
third did, because the charge has capped and the extra beats cost late-match fade. Overworking
the heat is punished, which is correct.

### Two things the tests got wrong before the code did

Worth recording, because in both cases the first instinct was to adjust the engine:

1. **The charge tests measured `CrowdPeakEnergy`.** That saturates against the crowd ceiling
   at around 78 whatever you spend, so it cannot see the charge at all — it showed 3
   isolations + 2 near tags as *worse* than 3 isolations alone. The charge governs the hot
   tag's own delta and the final rating; those are what the tests measure now.
2. **`AFourthIsolation` asserted equality.** Saturation means "buys nothing more", not
   "produces an identical number" — the extra beats still cost fade. The assertion is now
   `five.pop <= three.pop`.

One thing genuinely was undersized: the hot tag was billed as the loudest planned moment in
wrestling and given `Rng(16,26)` against a comeback's `Rng(12,20)`. At that size the isolation
read as a cost with no payoff and the full formula graded below a sprint that never put anyone
in peril. Now `Rng(26,40)`, and a denied tag is worth more per beat than another isolation
(0.45 vs 0.55 across a cap of 2 rather than 3) so that taking one is not a straight loss.

### One pre-existing test changed

`MatchMatrixTests` sweeps `MatchStructureLibrary.All` into singles matches. Tag structures
cannot be booked that way, so the sweep now enumerates `ForSideSize(1)`. The test's subject is
the singles booking space; this keeps it that.

### Result

**351 tests passing.** Singles equivalence re-verified after the balance changes: the same
48,720-match harness (7 structures × 4 match types × every roster pair × 2 familiarity levels,
carrying per-beat deltas and full commentary text) is **byte-identical** to the phase 1 tree.

### Review — round 2

*(pending)*

---

## Phase 3 — Playable end to end

**Goal:** book, run, save and reload a tag match through both front ends.

### What changed

| File | Change |
|---|---|
| `Persistence/SaveGame.cs` | Save **v3**. `CardItemDto` gains `SideA`/`SideB` (id lists) and `StartingIndexA`/`B`; `BeatDto` gains `IncomingIndex`. `WrestlerA`/`WrestlerB` retained as read-only v2 fields. |
| `Persistence/SaveSerializer.cs` | Writes sides; reads either. A v2 card item becomes two sides of one. The phase 1 `NotSupportedException` guard is removed — v3 can carry the thing it was refusing. |
| `Web/Shared/MatchBuilder.razor` | Singles/tag toggle, partner pickers on both sides, structures filtered by `ForSideSize`, side names throughout the beat editor, tag beats gated out of singles matches, and a warning when a hot tag has no peril before it. |
| `UI/MatchBookingFlow.cs` | The same flow in the terminal build. The beat-editor chain now takes `MatchSide` rather than two wrestlers. |
| `Web/Screens/MatchScreen.razor` | Title reads `BookedMatch.Name` instead of rebuilding "A vs B" by hand — the phase 1 review flagged this exact site. |

### Two deliberate choices worth recording

**A v3 save does not write `WrestlerA`/`WrestlerB` at all**, even for a singles match. Writing
them would let an older build open the save and silently drop every partner. Refusing to load
is a better failure than loading a lie, and `SaveSerializer` already rejects a save whose
version is newer than the build.

**A card naming somebody the roster no longer has is dropped whole**, rather than rebuilt a
man short. Three-quarters of a tag match is not a match, and running one would be worse than
losing the booking.

### Left for phase 5, deliberately

`ShowSimulator.RunMatch` and `MatchScreen` still key familiarity and feud heat on
`WrestlerA`/`WrestlerB` — the two *starters*. For a tag match that records the story against
the men who took the opening bell rather than against the teams. It works and it is wrong, and
side-keyed feuds are exactly what phase 5 is for. Marked here so it is not mistaken for an
oversight.

### Result

**356 tests passing.** An end-to-end test books a Southern Tag onto a card, runs it through
`ShowSimulator`, and asserts the fall is credited to the partner who came in on the hot tag —
which is what the notes say: `Robert def. Bobby — ★★★¾`.

### Review — round 3

*(pending)*
