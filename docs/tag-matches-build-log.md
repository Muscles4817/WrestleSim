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

The hardest review of the build, and it was right about all of it. The reviewer built its own
singles-equivalence harness independently of the author's — 215,089 rows, deliberately aimed
at the space the author's structure filter could have hidden — and confirmed byte-identical
output. It also confirmed re-execution determinism, side-mirror symmetry (3.3073★ vs 3.3073★
with the sides swapped), correct charge crediting on every path it could construct, and that
two hot tags in one match is not exploitable at equal beat count. Then it found six real
defects.

**The two worst were failures of the author's own claims, not of the code.**

*The build log's charge table contained the disconfirming number and it was read as
confirmation.* The table showed 3 isolations → 3.079★ and 5 isolations → 3.105★, and the
prose beneath it said overworking the heat is punished. It is not: at **constant beat count**
the rating climbs monotonically to 8 isolations (1.793★ → 2.471★), because the charge cap
limits only the payoff while each extra `Isolation` keeps adding storytelling at a gentle 0.86
decay. `AFourthAndFifthIsolation_BuyNothingMore` passed only because its longer plan was
faded — it was measuring fade, not saturation. **Referred to adjudication** (how to fix,
below); the claim is withdrawn until it is.

*The adjudicated top-weighting ruling shipped with no test that could fail.* The reviewer
inverted the aggregation to read a side as its **worst** member and all 351 tests stayed
green. The test compared a tag structure against a singles structure, so the structure
difference dominated and the aggregation never entered the comparison. This is the same
defect the phase 1 review caught (`ControlSign`), recurring on the second ruling.

**Accepted and fixed:**

| # | Finding | Fix |
|---|---|---|
| 1 | The top-weighting test could not fail. | Rewritten twice. It now holds the plan, opponents and seeds constant and varies only side A's composition, comparing star+star / star+jobber / jobber+jobber. A second test pins the side read directly via `HeatEconomy.SideStanding`. It also surfaced a real tension — see below. |
| 2 | `HotTagCharge` discarded every near tag when there was no isolation, making a denied tag a pure cost and contradicting the commit's own justification for the beat. | Near tags now count in the unearned branch, scaled against the penalised floor. Test added. |
| 4 | The beat-legality rule checked the **wrong side** for `Isolation` and `NearTag` — control is the side doing the isolating, but the man who needs a corner is their opponent. Masked by the uneven-sides block; with it lifted, a 1v2 validated and leaked a fallback commentary string ("*the corner is beside himself on the apron*"). The adjudication had specified this rule as the *replacement* for the size rule and it did not subsume it. | Directional now. Test asserts the direction on the legality rule itself rather than on the block that masks it. |
| 5 | Narrowing `MatchMatrixTests` to `ForSideSize(1)` dropped six distributional audits, and the comment claimed an equivalent tag sweep existed elsewhere. It did not. | `TagMatrixTests` added — the same sweep shape over tag structures. Southern Tag 4.03★ > Formula Tag 3.69★ > Tag Sprint 2.90★; crowd peak spans 33.5–99.4; matchup spread (1.97) exceeds structure spread (1.13). |
| 6 | None of the eleven new beats appeared in `IsOnType`, so declaring a match type on a tag structure was a pure penalty — a Southern Tag was docked 0.13★ for being called Storytelling. | Tag beats added to the coherence sets. Southern Tag now rates best as Storytelling (4.29 vs 3.83 Technical) and Tag Sprint best as Spotfest, which is what those structures are. |
| 7 | `ApplyAllFourBrawl` read `LegalPair` — but the beat's premise is that nobody is on the apron. | Reads both whole sides. |
| 8 | The `Ctx` aggregation doc block claimed one carve-out; there were four. | Corrected, with the shared rationale named: the carve-outs are the beats whose *subject* is the pair itself. |
| 10a | `MatchEngineState.Tag` silently substituted the next man when `IncomingIndex` named the already-legal performer. | `Validate` now walks the tag sequence statically and rejects it. Test added. |
| 10d | `ApplyMiscommunication`'s crowd delta had no connection factor — a mix-up between two nobodies popped as hard as one between main-eventers. | Scaled by the side's connection. |

**Also recorded, not code:** finding 9 — the charge's effect arrives overwhelmingly through
`StorytellingContribution`, not through the crowd. `BeatResult.CrowdEnergyDelta` is a
*pre-compression* value; `ApplyEnergy` then applies `0.20 + 0.80·√headroom` and clamps at the
ceiling, so a 50% larger pop moves the peak by well under a point. The charge table above is
therefore correct about the pop and misleading about what reaches the room, and the design
line "the largest single crowd delta the engine can produce" is true of the raw number and
largely inert in the score. Left as-is for now — the mechanism works, through storytelling —
but stated plainly here rather than left to imply otherwise.

**Deviation from the plan, previously unrecorded:** the plan's §2 charge formula is
multiplicative (`base × isolationCharge × nearTagCharge × connection × selling`); the
implementation is additive (`1.0 + iso + near`) and drops the isolated man's selling. The
additive form is easier to reason about and to cap, which is why it was written that way, but
the plan was not updated to match.

### Adjudication — phase 2 questions

Three questions went to a third agent. All three rulings went against the author, and the
third overturned the author's own analysis with a measurement the author had not thought to
take.

#### Q1 — How to punish overworking the heat? **Ruled: punish the *run*, not the count.**

The author's instinct was to steepen `RepetitionDecay(Isolation)`. Refuted empirically: at 0.68
the rating still climbs monotonically to eight isolations, and it still climbs at 0.30, because
`Math.Pow(d, n-1)` is strictly positive and can slow a climb but never invert one. The crowd
axis was untouched by that argument entirely — `Isolation` has a *positive* crowd delta, so in
this engine a fifteen-minute heat segment made the building **louder**.

The deeper correction is about what the reference actually says.
[18](wrestling-reference/18-match-craft.md) §2.3 does not say a long heat is bad; it says the
opposite twice — "*the longer and more frustrating the heat, the bigger the comeback pops*" —
and names the condition: "*the hope spots are essential*". §3.1 says 15–25 minutes adds a
second heat/comeback cycle. So the thing to punish is not the fourth isolation, it is the
fourth *consecutive* isolation with nothing to hold on to. The library's own Southern Tag
already agrees: it never books two isolations back to back.

Implemented as `MatchEngineState.IsolationRun`, a second counter beside the charge. The charge
measures what was spent buying the payoff and resets on a tag; the run measures how long the
room has been asked to wait and resets on a tag **or a near tag** — a denied tag is a hope
spot, so it now does two jobs. Past `IsolationPatience` (3, the same constant the charge caps
at, so the two can never drift), the beat drains crowd energy at an accelerating rate and
contributes no storytelling. `AdvantageDelta` and `TechnicalContribution` are deliberately
untouched: an overlong heat is badly *paced*, not badly *wrestled*, and that distinction is
why the crowd axis is the right place for it. The commentary branches too, so the player can
hear it — duelling chants, the crowd talking amongst itself, a beach ball.

| plan (12 beats, constant length) | before | after |
|---|---|---|
| 3 isolations | 2.486★ | 2.487★ |
| 8 isolations in a row | **3.099★** | **2.429★** |
| `I I I N I I I N` (two cycles) | — | 3.204★ |
| crowd average, 3 → 8 isolations | rose | **40.94 → 35.43** |

The 1–3 range is unchanged. A long heat punctuated by hope spots stays viable; an unbroken one
is punished. The ruling explicitly declined to apply the same shape to `NearTag`, which
self-limits — it over-rewards by +0.078★ two beats past its cap and then turns over on its
own, against +0.61★ and no turning point for the isolation.

#### Q2 — Should `Shine` and `Cutoff` be tag-only? **Ruled: no — fix the text, and only `Shine` needs it.**

Settled by the repo's own standing rule, written down in the phase 1 adjudication: *you warn
about a booking the engine handles, you block one the engine mis-handles*. A singles shine is
modelled correctly — `SideAvg` short-circuits to the sole member and every term is coherent.
It just **said** the wrong thing: "*Solo A are firing on all cylinders early — quick tags…*",
reachable from the booking UI today. That is a text defect, not a modelling one.

`Cutoff` needed nothing at all — every one of its four lines uses only the two wrestlers'
names and reads correctly in a singles match, and it fills a real singles gap as the only
one-beat takeover that *costs* crowd energy. Gating it would have deleted a working beat.

`ApplyShine` now branches on `side.IsTag`, with four options either way so the RNG is drawn
exactly once and tag output is unchanged. The `BeatType` header comment, which claimed every
tag beat needs somebody on the apron, is corrected — as is the `BeatLibrary` copy the player
actually reads, which described a "face team".

#### Q3 — Does the star carry the craft, or only the crowd? **Ruled: the author's premise was false.**

The author reported a tension between the two phase 1 rulings: top-weighted side reads make
the star carry the room, but legal-only craft reads meant the overall rating landed *below*
the midpoint, so the conservation law looked like it had survived.

It had not. The measurement held the **worst available booking** of the mixed side fixed.
`Formula Tag` starts `Members[0]`, so `Rating(Star(), Jobber())` books the *jobber* to take the
hot tag, work the double team and score the fall. Averaged over both member orders:

| | star+star | jobber in | star in | job+job | midpoint | **averaged** |
|---|---|---|---|---|---|---|
| Formula Tag | 3.924 | 3.030 | 3.577 | 2.249 | 3.086 | **3.304** |
| Southern Tag | 4.150 | 3.449 | 3.830 | 2.714 | 3.432 | **3.639** |
| Tag Sprint | 3.140 | 2.589 | 2.739 | 1.689 | 2.415 | **2.664** |

The mixed side rates **+0.22★ above the midpoint** across the booking space. And the
falsification the original test never had: at `DragWeight = 1.0` the conservation law
reproduces to three decimals (3.081 vs a 3.086 midpoint) and vanishes at 0.5. Ruling B works
on the rating, not only on the crowd.

The ruling also declined the author's proposed fix — blending craft reads toward the side's
top-weighted value — as an *active regression*. The 0.55★ gap between putting the star in on
the hot tag and putting the jobber in is the most legible booking lever the tag engine has, it
falls straight out of `ApplyHotTag` scaling the pop by the incoming man's connection, and
blending would flatten exactly that. The star carries **by being legal for the beats that
matter**, which makes carrying a decision the player makes rather than a stat they possess.

`AStarCarriesAWeakPartner_...` now averages both orders, asserts above the midpoint with
headroom (and fails at `DragWeight = 1.0`, which is what makes it a test), and separately
asserts the hot-tag lever. **The open question recorded here previously is withdrawn.**

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

---

## Phase 4 — Teams and chemistry

**Goal:** a standing team is a different act from two singles wrestlers on the same side.

### What changed

| File | Change |
|---|---|
| `Models/World/TagTeam.cs` | New. Members, tenure, `MatchesTogether`, `LastTeamed`, and `Chemistry` with saturating growth and time-based decay. |
| `Models/MatchPlan/MatchSide.cs` | `Team` and a derived `Chemistry` — 1.0 for a side of one, 0.0 for a pair with no standing team. |
| `Engine/MatchEngine.cs` | `ChemistryLift` — chemistry reduces `DragWeight` per side. Chemistry scales `DoubleTeam` and `Miscommunication`. |
| `Models/World/Career.cs` | `Teams`, `TeamFor(members)`, and per-day chemistry decay on the world clock. |
| `Persistence/*` | `TagTeamDto`; card items carry `TeamAId`/`TeamBId` so a reloaded side points at the *same* team instance. |
| `Engine/ShowSimulator.cs` | Records a match against each side's team. |
| `Tests/TagTeamTests.cs` | New, 13 cases. |

### Chemistry does two opposite jobs

That is the design, and it is worth stating because a single "team quality" number would have
been the obvious and wrong shape:

1. **It makes tandem offence work.** `DoubleTeam` scales 0.78–1.22 with chemistry — worth
   about half a skill grade either way. Two good singles wrestlers hit a double team
   competently; a team that has done it two hundred times hits it in stereo. The commentary
   forks on this too, so a scratch pairing reads as one.
2. **It makes the pair read as one act.** Chemistry lowers that side's drag toward its
   average (`ChemistryLift = 0.7`), so an established team reads much closer to its best
   member. This is the mechanical form of "the tag division is where you elevate somebody"
   — [12](wrestling-reference/12-pushes-and-positioning.md) §2.2.1. Measured: a star and a
   rookie thrown together rate meaningfully below the same two as a real team.

`Miscommunication` scales the *other* way for the same reason — a drilled team colliding is a
departure and therefore a bigger story; two strangers colliding is Tuesday.

### Decay is on the clock, not the match

`Career.AdvanceOneDay` decays every active team, alongside momentum and title drift. Chemistry
is a property of the team's history, not of any one match, and a team that stops teaming has
to stop being a team even if the player never books them again. Sixty days of grace, then a
just-under-two-year half-life: a reunion five years later is not the act that split up.

### Result

**369 tests passing.** Singles equivalence re-verified — the 48,720-match harness is still
byte-identical, because a side of one returns its only member outright and never reaches the
drag term.

### Review — round 4

*(pending)*

---

## Phase 5 — Consequences

**Goal:** a tag match costs and pays something a singles match does not.

### What changed

| File | Change |
|---|---|
| `Engine/HeatEconomy.cs` | `ForSides` — the fall priced against the two *sides'* standing, then the pinner and the pinned take it in full while partners take a share. `SideStanding` reads a side top-weighted, mirroring the engine. `MatchStatusOutcome` gains `Partners`. |
| `Models/MatchPlan/Feud.cs` | `SideA`/`SideB` with `WrestlerA`/`WrestlerB` as shims — the same pattern `MatchPlan` uses. `IsTeamFeud`, `SideAName`/`SideBName`. |
| `Engine/FeudBook.cs` | Side-aware `Key`, `Find`, `GetOrCreate` and `Record`. Each side's names sort internally so billing order does not matter, then the two sides sort against each other so home advantage does not either. |
| `Engine/ShowSimulator.cs` | Familiarity and feud heat keyed on sides; `ForSides` for tag results; cross-pair heat at 25%. |
| `Models/World/TitleReign.cs` | `Champions` with `Champion` as a shim. `ChampionName`, `HeldBy`. |
| `Models/World/Title.cs` | `SideSize`, `IsTagTitle`, `Champions`, `IsHeldBy`. |
| `Engine/TitleEconomy.cs` | `ResolveTitleMatch` over sides; retention asks whether the *team* kept it; `PartnerBonuses` so both holders are paid. |
| `Persistence/*` | Feud sides, reign co-champions and title `SideSize`, all reading v2 forms. |
| `Tests/TagConsequenceTests.cs` | New, 13 cases. |

### The asymmetry is the whole point

`PartnerLossShare = 0.35`, `PartnerWinShare = 0.50`. That gap is what makes "have the other
guy take the fall" a real booking lever rather than a free pass — the most common use a tag
match is put to ([12](wrestling-reference/12-pushes-and-positioning.md) §6.1). Measured: a
90-overness star loses meaningfully less when his partner eats the pin, and still loses
something, so it remains a decision.

The transfer pool is set by `SideStanding`, which is top-weighted for the same reason
`Ctx.SideAvg` is — beating a team reads as beating the team, and a team is mostly its best
man. A flat mean would have made adding a jobber to a main-eventer's side a way of quietly
halving what beating them is worth.

### A team rivalry is its own feud

Keyed separately from the singles feuds inside it, because the crowd's appetite for two teams
is genuinely not the crowd's appetite for any pair of men in them, and it has to wear out
separately ([20](wrestling-reference/20-storylines-and-feuds.md) §9.1). A tag match records
full heat against the team feud and 25% against each of the four cross-pairings — small on
purpose, because four cross-pairs at a meaningful share would mean one tag match builds more
singles heat than a singles match does. That is how a tag programme pays off in a singles
blow-off.

### A tag belt is held and lost jointly

`SideSize` on `Title`, so a tag belt competes for the same finite pool of audience attention
as every other title ([21](wrestling-reference/21-championships.md) §2.1) — adding one dilutes
the singles belts exactly as another singles belt would, which is why it is a property of the
title rather than a separate kind of thing. Retention asks whether the belt stayed on the side
that came in with it, not who was legal; both holders get the status bonus.

### Result

**392 tests passing.** Singles equivalence holds — still byte-identical across the
48,720-match harness.

### Review — round 5 (phases 3, 4 and 5 together)

Sixteen findings, and the hardest round of the build. The reviewer again verified the
singles-equivalence contract independently — its own 29,376-row harness across a synthetic
roster spanning overness 10–96, byte-identical — and then found a crash path, a decay curve
wrong by a factor of about 24, and two features with no way into the game.

**A correction to a correction:** the phase 5 "What changed" table above credited
`HeatEconomy.cs`, `Feud.cs` and `FeudBook.cs` to commit `8324f02`. They landed in `d7da2ba`,
whose message describes only phase 2 review fixes. The table is right about what changed and
wrong about where; noted rather than rewritten, because the history is the history.

**Accepted and fixed:**

| # | Finding | Fix |
|---|---|---|
| 1 | **A reloaded tag card lost its feud, and crashed the show if a feud-gated beat was booked.** `SaveSerializer` rebound the feud with `Find(a, b)` — the two *starters* — but phase 5 had re-keyed feuds on sides. Silently −0.15★; with a `FeudalEscalation` beat, an uncatchable `InvalidOperationException` and an unrunnable card. | Rebinds on sides. |
| 2 | **Chemistry decay compounded quadratically.** `Decay` recomputed the whole idle stretch from `LastTeamed` on every call and the world clock calls it daily, giving `0.9985^(N(N+1)/2)` — a ~30-day half-life against the documented two years, and effectively zero within five months. | A `DecayedTo` marker; decay is now idempotent per day and charges each day once. Two tests now assert the curve rather than printing it. |
| 3 | **Partner heat was dampened twice**, so 50%/35% came out as 24%/30% — and a rookie beside a 95-overness star got about a sixth of his share, because the star's own ceiling compression was charged to him. The elevation payout was ~6× too small. | Partners take their share of the *undampened* swing, then dampen once against their own ceiling. |
| 4 | **Teams and tag titles were unreachable from the game.** `Career.TeamFor` had no production call site, nothing ever set `MatchSide.Team`, and `TitleRegistry.Create` took no `SideSize` — so chemistry was pinned at zero for every side a player could book, and a tag belt could not exist in a played career. | The builder attaches a standing team and offers to form one; the Championships screen has a "Contested by" control. |
| 5 | **Cross-pair feuds burned the blow-off they were meant to build.** Heat was discounted to 25% but staleness was recorded in full, so after three tag matches each singles pairing sat below the cold threshold *and* carried a 35% familiarity penalty. The justification was also self-defeating: 4 × 25% is a whole singles match's worth. | A sixth, and no `RecordMatch` at all — seeing two men on opposite sides of a tag match is what makes people want the singles match, not a substitute for having seen it. |
| 6 | **`championWon` froze a tag belt whenever a champion switched sides**, and named the man who had just been pinned as the defending champion. `Any(winningSide.Contains)` is true whichever side wins. | A defence is the champions holding it *together*: `All`, over the champions actually in the match. |
| 7 | **The console offered nine tag-only beats in a singles booking** and had no hot-tag warning — the phase 3 message claimed both front ends had the same flow. | Both gated, both warn. |
| 8 | **Three of four feud call sites still keyed on the starters** after phase 5 fixed the fourth, so the same tag match deposited heat into different feuds depending on where it was run. | All four on side keys. |
| 9 | The web builder offered singles belts in a tag match and only rejected them at the final click, three steps later. | `SelectableTitles` filters on `SideSize` and requires *all* holders to be in the match. |
| 10 | The hot-tag warning missed the case where a plain `Tag` clears the charge — exactly the "nothing to release" plan it exists for. | Looks only at what is still charged since the last tag change on that side. |
| 11 | `SideStanding` claimed to mirror `Ctx.DragWeight` but ignored the chemistry lift that phase 4 had added — the engine read a drilled star-and-rookie side at 84.75 and the heat economy at 72.50. It was already untrue when written. | Takes chemistry, threaded through from `ShowSimulator`. |
| 12 | A v3 save wrote the v2 fields documented as never-written, as `""`. | Made nullable. |
| 14 | The hand-written v2 fixture had empty `Wrestlers`, `Feuds` and `Titles`, so it exercised none of the v2 read paths phase 5 added. (The reviewer generated a real v2 save from a `53e0233` build and confirmed the behaviour is correct — but nothing in the suite would have caught it otherwise.) | A realistic v2 fixture covering feuds, two reigns and an absent `SideSize`. |
| 15 | `ForSides` is public and had no input guards: an empty winning side read as a maximum upset and paid ~5× a normal win. | Guarded — and the guards immediately caught two genuinely wrong calls in the phase 5 tests themselves. |
| 16 | `FeudBook.Key` could collide a singles feud with a team one if a `RealName` contained the separator. | Length-prefixed segments. |

**Declined, with reasons.** Finding 4's fix initially seeded a tag title by default. That broke
three existing tests asserting the shipped slate is exactly three belts and "exactly fits the
audience's attention" — and those tests are right. A tag belt claims the same finite attention
as any other ([21](wrestling-reference/21-championships.md) §2.1), so shipping one by default
would spend the player's first real title decision for them. Reachability is solved by the
create UI; the seeding was reverted.

### Result

**403 tests passing.** Singles equivalence still byte-identical.

---

## Follow-up — tag titles finished

Phase 5 introduced joint reigns and taught `TitleEconomy.ResolveTitleMatch` about sides. What
it did not do was revisit every *other* place the title economy reads "the champion". On a
singles belt `Champions[0]` and "the champion" are the same thing, so nothing failed; on a tag
belt they are the same thing half the time, which is worse than failing.

An audit of every `.Champion` read outside the shim itself found five:

| Site | Was | Now |
|---|---|---|
| `TitleEconomy.ApplyDailyDrift` | Pulled the belt's standing toward `reign.Champion.EffectiveOverness` — the first-listed holder. Swapping the billing order of the same two champions changed what the belt was worth over time. | Reads the reign through `HeatEconomy.SideStanding`, top-weighted exactly as the crowd and the status economy read a side. |
| `TitleEconomy.ApplyNonTitleLoss` | Named and priced `Champions[0]` whoever had really been beaten, producing results reading "Ricky lost to X" when Robert took the fall. | Takes the man who lost. Measured: the 80-overness half losing to a nobody costs the belt 2.50, the 40-overness half 1.50. |
| `TitleEconomy.Vacate` | Reported one name when stripping a belt from two people. | Names both — and only when a reign was actually closed by that call. |
| `Title.ReignsOf` | Matched on `r.Champion == w`, so a second-listed champion's reigns were not found by it. | Matches on `r.HeldBy(w)`. |
| `DashboardScreen`, `MatchBuilder` | Rendered one holder of a tag belt. | Render `ChampionName`. The belt list also marks which shape a title is, since both now appear together. |

Also: the Championships screen's "Champions" stat showed the *reign count*, which reads as a
number of people the moment a belt can be held by two. Renamed to "Reigns".

**Still not seeded by default**, and the reasoning has not changed: a tag belt claims the same
finite attention as any other ([21](wrestling-reference/21-championships.md) §2.1), so shipping
one would spend the player's first real title decision for them. A test now pins that —
introducing a tag belt measurably dilutes every belt already on the books, which is the cost
that makes the decision a decision.

**414 tests passing.** Singles equivalence still byte-identical.

### Review — round 6

Reviewed independently. It built a differential harness of its own — 625 rows across drift,
non-title loss, title-match resolution, `Vacate` and `ReignsOf`, run against this commit and
its parent — and ran five mutations of the production code. Six of the eleven new tests
discriminate; none passes with the code it protects inverted, so the failure mode of rounds 1
and 2 has not recurred.

It found that the sweep was **not** complete, and that three claims above were overstated.

**Defects fixed:**

| # | Finding | Fix |
|---|---|---|
| 1 | **`Vacate` named people who no longer held the belt.** The suffix searched the whole lineage for the last vacated reign rather than reading the reign it had just closed, so vacating an already-vacant belt reported the *previous* holders as the people being stripped. Latent — the UI guards on `IsVacant` — but a bug introduced by this commit. | Reads the reign in hand. |
| 2 | **Singles `Vacate` output was not byte-identical**, which this commit asserted it was. The `Champions.Count > 1` guard chose *which name* to use; the suffix itself was appended unconditionally, so a singles vacancy read "Stripped by the promotion — stripped from Ricky Morton." The reviewer's harness caught it; mine would not have, because it does not cover the title economy. | Only a tag reign earns the suffix. Test pins the exact singles string. |
| 3 | **Two `.Champion` reads survived**, in `ResolveTitleMatch` and `Vacate`, feeding `TitleUpdate.OutgoingChampion`/`Champion` — the very pattern the commit is named after. The audit table above undercounted by two. | `TitleUpdate` gains `OutgoingChampions`/`Champions`. The singular forms stay for singles readers. |
| 4 | **The `ReignCount`-as-champions mislabel was only half fixed** — the retired-belts panel still read "3 champions" for a belt six people had held. | Fixed, along with the doc comment on `Title.ReignCount`. |
| 5 | **`SideStanding` was called with chemistry defaulted to 0**, so the title economy read every team as two strangers while the crowd read a drilled one as nearly a single act — 76.50 against 87.35 for the same 92/30 pair. `SideStanding`'s own documentation is a post-mortem of that exact divergence happening once already. | `ApplyDailyDrift` takes chemistry; `Career.AdvanceOneDay` passes the team's. |
| 7 | **The top-weighting claim was unpinned.** Replacing `SideStanding` with a plain mean left all 414 tests green — the two drift tests only required order-independence and monotonicity, which a mean satisfies. | A test that fails on a mean. Verified by mutation. |

**Claims corrected:**

- *"`ShowSimulator` passes the one who was pinned."* `Pinner` and `Pinned` are expression-bodied aliases for `Winner` and `Loser`, so that edit is a readability rename and nothing more — reverting it leaves the suite green. The whole fix lives inside `ApplyNonTitleLoss`. The commit message read as though the wrong person had been passed, which was never true.
- *"a second-listed champion had no title history as far as the game was concerned."* `Title.ReignsOf` has no production callers. The fix is right; nothing reads it.
- *"Vacate reported one name when stripping a belt from two people."* True of the string, but `GameState.VacateTitleAsync` discards the returned update entirely, so no player has seen either version.

**Noted, not actioned:** `ShowSimulator` charges a non-title loss only against belts held by
the *pinned* man, so a singles champion on a losing tag team whose partner ate the fall pays
nothing. That is consistent with doc 21 §4.1 and defensible — the champion did not lose — but
it is now a deliberate decision rather than an accident of there being no tag matches.

**418 tests passing.**

---

## Follow-up — trios

Three a side. The striking thing is how little of this was engine work: **a 3v3 plan validated
and executed correctly before a line of trios code was written**, because three a side is
still two sides and the sides abstraction never had a cap in it. A probe on the phase 5 tree:

```
PROBE validate errors: none
PROBE ran: ★★★ (2.97 / 5.00), pinner=A3, side=3
```

The tag rotation was already `(current + 1) % memberCount`, `IncomingIndex` already let a
booker name which of two partners comes in, `SideAvg` and `TopWeighted` were already
count-agnostic, `ForSides` already distributed to N partners, `FeudBook.Key` already keyed N
members, `Title.SideSize` already generalised, and save v3 already stored side member lists.

So this is not "add trios". It is **finding everything that had quietly assumed a side has at
most two people** — which is the same shape of job as the tag-titles follow-up, and was found
the same way.

| What assumed two | Fix |
|---|---|
| `ApplyAllFourBrawl`'s commentary said "All four of them are in the ring" — wrong the moment six are. | Counts the people actually in there. The `BeatType` member keeps the name `AllFourBrawl`, because that string is what gets written into save files and renaming it would orphan every card already on disk; the display name and the commentary are what changed. |
| `ApplyNearTag` named `PartnersOf(other).First()` — silently one of two, which reads as a mistake rather than as shorthand. | `NameCorner` — a name when there is one partner, "his corner" when there are more. |
| The web builder had a singles/tag **boolean**, two hardcoded partner panels and two hardcoded team panels. | A side-size selector and a generated slot loop. Adding a fourth man later is now a number, not more markup. |
| The console flow asked `AskTag()` and picked partners with three positional exclusions. | `AskSideSize()` and `PickSide`, which picks a whole side of any size and excludes everybody already booked. |
| No structures to book. | `Lucha Trios` (the Arena México default — fast, tandem, a fall out of nowhere) and `Six-Man War` (the full formula, which a third man makes *longer* rather than different: two corners to keep him from, two tags to deny). |

Doc 18 §2.5 was written first, and it is what settles the design question this raises. A long
heat is good and the hope spots are what make it bearable, so a third man is not a reason to
change the formula — it is a reason the formula can run longer. `Six-Man War` books two
isolations and two denied tags where `Southern Tag` books two and two, and rates above the
lucha sprint for the same reason the Southern Tag rates above the tag sprint.

**432 tests passing** (14 new). Singles equivalence still byte-identical.

---

## Trios — review round 1, and a paragraph I have to withdraw

The reviewer's verdict was **not safe to merge**, and the substantive half of it was right in
a way the paragraph immediately above this one is a good example of. That paragraph says a
third man "is a reason the formula can run longer" and then, in the very next sentence,
concedes that `Six-Man War` "books two isolations and two denied tags where `Southern Tag`
books two and two" — the same numbers. It asserts a mechanic and then prints the measurement
denying it. That is the fourth time in this build log I have done that, and the second time
the contradiction was inside a single paragraph.

Worse than the prose: **both structures left three of their six people on the apron for the
entire match.** Each contained exactly one tag change with no `IncomingIndex`, so the tag fell
through to "next man round" — side A went 0→1 and side B never tagged at all. Members `A3`,
`B2` and `B3` were never legal, never worked, and were never named. Doc 18 §9 lists *"the
unexplained third man — somebody is on the floor for minutes with no reason given"* as a named
failure mode and §2.5 calls it the format's characteristic one. I shipped it as a preset.
(Round 2 is right that this citation is loose: §9's line puts the unexplained man *on the
floor*, and §2.5's "characteristic failure" sentence is about a triple threat — indeed §2.5
as rewritten in this same PR says trios are the exception, because everyone not legal is on
the apron by rule. The defect was real on its own terms and did not need the borrowed
authority.)

### The blocking two

| # | Finding | Fix |
|---|---|---|
| 1 | `SidesComplete` was `partnersA.Take(sideSize - 1).All(w => w is not null)`. `partnersA` starts **empty**, and `All` on an empty sequence is `true` — so the gate passed with no partners picked. `Finish()` then built `MatchSide.Of(a)` vs `MatchSide.Of(b)`: a perfectly valid *singles* match, booked silently for a player who asked for a trios one. A **regression on shipped tag booking**, not a trios gap. | The gate now reads `MembersA.Count() == sideSize && MembersB.Count() == sideSize` — the same `MembersA` expression `Finish()` builds the side from. The point is not that the new expression is correct; it is that the gate and the builder can no longer disagree, because there is only one of them. Same fix at `:166`, where the Side B panel appeared before Side A was filled. |
| 6 | Side B's team-formation panel was titled "Side A". | One word. |

### The substantive two

Findings 2 and 3 were the real problem, and the reviewer offered a fair choice: make the
third man work, or stop claiming he does. I did the first, because the second would have
shipped two presets that commit the failure their own documentation names.

**`Six-Man War` — the American six-man.** Three tag changes added, all naming their incoming
member explicitly:

```
Cut-Off (B) → Face in Peril → Near Tag
            → Quick Tag B→1 → Face in Peril → Near Tag     three fresh heels,
            → Quick Tag B→2 → Face in Peril                one isolation each
            → Hot Tag A→1 → Double Team
            → Quick Tag A→2                                the third face, fresh
            → Everybody In → Shock Kickout (A) → Save (B) → Clean Victory (A)
```

Three isolations against three fresh opponents is the "deeper heat than a tag can carry" the
Description claims, and it is the one thing two a side genuinely cannot book. A near tag
between each keeps `IsolationRun` at 1 throughout, so it costs no patience — doc 18 §2.3 is
explicit that a long heat is good and the hope spots are what make it bearable. The hot-tag
charge is credited to the side being worked over, so the heels tagging costs the faces
nothing. And the third face taking the fall off a tag he has not worked yet is a beat that
cannot exist at two a side.

Finding 9 was also right: dropping the `Shock Kickout` left `Save` breaking up a pin the match
had never shown anybody attempt. Restored as `Shock Kickout (A) → Save (B) → Clean Victory
(A)`, which reads as the faces nearly winning, the heels breaking it up, and the faces winning
anyway.

**`Lucha Trios` — actually lucha now.** The old version was a nine-beat sprint with one tag,
named for Arena México. Doc 25 §3.3 says three-a-side in lucha means *"rapid tag rules that
allow constant motion"*, so it now has four tag changes in thirteen beats, two of them on
the rudo side, no isolation and no hot tag at all — and `Roll-Up Steal` for the "fall out of
nowhere" its Description had always promised while ending on `Clean Victory` (finding 12).

### Measured, on the same six people, 300 seeds each

```
                     UNNAMED (of 6)   stars    tech   story   peak   avgCrowd
Six-Man War   before     3           3.5884
Six-Man War   after      0           4.4403    56.7   109.0   97.7     73.0
Lucha Trios   before     3           3.2249
Lucha Trios   after      0           3.6926    41.9    42.6   89.7     79.5
Southern Tag  at 3v3     2           4.4893    50.7   105.9   96.9     76.8
```

Two things to state plainly rather than let the star column carry.

**Side size still has no term in the engine, and should not.** The reviewer's headline
measurement — identical clones at 2v2 and 3v3 rating 3.6793 to four decimal places — is
correct, and it is not the bug it looks like: every aggregate over three identical people is
the same number as over two, so three identical men *are* the same act. A side is read from
its members, so a third man is worth exactly what the booking gives him to do. What was
broken was the booking, not the reading.

**`Six-Man War` rated 0.05 below `Southern Tag` at 3v3, and the reason I gave was wrong.**
See the round 2 section at the end of this file: I attributed it to average crowd energy and
deferred it, and round 2 measured that the whole of the gap and more is `VarietyNudge`. Fixed
there rather than left standing here.

### The rest

| # | Finding | Fix |
|---|---|---|
| 4 | §2.5 cited doc 25 §3.3 as authority for "a novelty elsewhere / the face-in-peril structure works unchanged". §3.3 says three-a-side is the *default* and *"this changes everything"*. | Rewritten to separate the two formats that share the name: the American six-man (face-in-peril applies; not a novelty — NJPW and WWE run them routinely) and the lucha trios (rapid tags, constant motion, group dynamic, three falls; the opposite shape). The presets now match the distinction. |
| 5 | `NameCorner` returned `"his corner"`, so one near-tag template read *"…drags him back! his corner is beside himself on the apron!"* — a lowercase sentence start, and gendered for a phrase that has to work in a women's trios. | Returns `"the corner"`; the template puts it mid-sentence (*"You can see the corner pleading on the apron!"*), which also drops the "beside himself". |
| 7 | `TitlesScreen` offered SideSize 1 and 2 only, so a trios belt could never be created and `MatchBuilder`'s exact `SideSize` filter meant no belt was ever selectable at three a side. `TriosTests` constructed `new Title { SideSize = 3 }` directly and asserted it worked. | Third option added. This is exactly the screen the paragraph above claimed to have found, and did not. |
| 8 | `EveryTriosStructure_IsBookableForThreeASideAndNotForTwo` never tested two. Both structures **validated cleanly at 2v2** — the name asserted a false property. | Naming `IncomingIndex: 2` makes the property true (`incoming >= side.Size` is rejected), so the test now checks it instead of being renamed. |
| 10 | `ApplyMiscommunication` and `ApplySaveBreakup` took `PartnersOf(x).First()`, so the same partner made every save and every mistake all match. Unlike `NameCorner` these beats need a *person*, so "the corner" is not available. | New `SpotPartner` rotates on `State.BeatIndex`. Deliberately not an RNG draw: an extra draw would shift every subsequent number and break the byte-identity the singles and 2v2 suites check for. At two members it selects the same person `.First()` did. |
| 13 | Stray double blank line; a comment saying "the beat's premise is that all four are in" in the commit that made that untrue; console prompt reading bare `"SIDE A"` at side size 1; `MatchBookingFlow.ResolveFeud` keying feuds on the **starters**, so the console booked a trios match against a singles feud while the web builder correctly used the side-to-side key. | All four. The feud one was pre-existing and carried forward — now `Find(sideA.Members, sideB.Members)` and `GetOrCreate` likewise. |

Finding 11 was fair and is fixed in the doc: the per-side advantage array is a genuine
prerequisite for a triple threat and is a cheap data-shape change; the reaction vector is what
makes such a match *worth booking* and is not a prerequisite. The two were run together as one
numbered point, and the attention argument was doing the presence argument's work. Now four
numbered points, correctly separated.

### Two new guards, from the finding that mattered

`EveryTriosStructure_PutsEveryMemberOfBothSidesInTheRing` walks the tag changes statically —
it cannot be fooled by a seed that happened to name somebody — and fails if any member of
either side is never legal. `EveryTriosStructure_NamesAllSixInTheCommentary` measures the same
thing from the other end over 60 seeds, stripping the whole-side renderings first so that
"Ann & Bob & Cal" is not counted as evidence that Cal did anything. Both fail on the structures
as originally shipped.

### One defect found and deliberately not fixed here

The commentary calls every wrestler "him". The shipped roster is half women, the builder
explicitly supports intergender matches, and a near-tag in a women's tag currently reads
*"Bianca Belair pulled **him** away"*. Eight templates in `MatchEngine`, two of them reachable
from singles matches. Fixing them would change commentary **text** for singles, which is the
canary three reviewers have used to prove the engine's numbers were untouched — so it goes in
the UX pass, where user-visible text is the subject, rather than being smuggled through here.

**434 tests passing** (2 new). Singles remain byte-identical, numerically and textually. Two-a-side
commentary **text** changes at one template — the near-tag line rewritten for finding 5 — while
its numbers are unchanged; `SpotPartner` and `NameCorner` both reduce to the previous
expressions at two members.

---

## Trios — review round 2

Verdict: **not safe to merge, narrowly, and for text only.**

The engineering survived everything the reviewer could throw at it. Singles byte-identity
re-proved with two independent harnesses over ~264k output lines covering **every** beat
template in the library; 2v2 numerically identical with exactly one commentary template
changed; all six people proved in the ring using the *production* `Tag()` rule rather than
the test's copy of it; the side-size defence proved analytically from `TopWeighted` and
empirically identical to full double precision at n = 2, 3, 4 and 5; 14,353 fuzzed plans
with no throws and no illegal ratings; and both new guards confirmed to fail on the
structures they were written against.

What blocked it was that a round whose entire subject is *"I asserted a mechanic and then
printed the measurement denying it"* shipped three more of them.

### The one that mattered: I named the wrong cause, and used it to defer the work

`Six-Man War` rated 0.05 below `Southern Tag` at 3v3. I attributed that to average crowd
energy and wrote that fixing it "is a rating-formula change with nothing to do with trios,
so it is not in this PR". Round 2 decomposed the composite over the same 300 seeds:

```
                star    techC   storyC  crowdC  finish  variety   distinct/beats
Six-Man War    4.4403  24.257  24.831  28.660   9.788   1.271     12/17
Southern Tag   4.4893  22.820  24.565  29.689   9.758   2.954     11/13
delta                  +1.438  +0.265  −1.029  +0.031  −1.683
```

The crowd deficit is real (−1.029) and is *more than cancelled* by technical and
storytelling (+1.703). The whole of the gap, and more, is `VarietyNudge` —
`(distinct/beats − 0.6) × 12`, reading 0.846 for Southern Tag and 0.706 for Six-Man War.
And the beats dragging that ratio down are the three `Quick Tag`s: **the exact beats that
put the third man in the ring.**

So the deferral does not survive its own correction. The engine was not expressing a view
about long heats. It was taxing a structure for its connective tissue, which is a perverse
incentive to leave the third man on the apron all night — the failure this whole PR exists
to fix, re-entering through the rating formula.

**I tried to fix it at the source and it was a bad trade.** `BeatType.Tag` was excluded from
both halves of the variety fraction, on the reasoning that a routine tag is not a spot:
nobody watching a six-man thinks *"that is the third tag, I have seen this"* — the tags are
how the match moves, the way a rope-running exchange is. That reasoning is still sound.

The implementation was not, and round 3 measured why. **The variety nudge is not only
measuring variety — it is also the brake on padding**, and exempting anything from the
*denominator* makes that thing free length. With the exemption in, appending `Quick Tag`s to
a `Southern Tag` raised its rating monotonically:

```
Southern Tag 2v2, appending N Quick Tags before the finish (400 seeds a cell)

                             +0      +4      +8     +16     +24     +32
Standard,  Tag counted    4.5042  4.5053  4.4621  4.3634  4.2578  4.1620   turns down, keeps falling
Technical, Tag counted    4.2699  4.3885  4.4234  4.4274  4.3901  4.3436   peaks at +16, then falls
Technical, Tag exempted   4.3232  4.5241  4.6367  4.7168      —       —    climbs, monotonically
```

Read that honestly, because the first version of this table annotated the counted row
*"padding punished"* and its own figures do not show punishment at +4 or +8 — round 4 caught
that, which is the same failure this file keeps recording. What the counted row shows is a
curve that **turns**: a couple of extra tags are worth a little (more beats, more crowd), and
past that the variety penalty overtakes them and keeps overtaking them. The exempted row
never turns, because nothing is left to overtake it. It is the *absence of a turning point*
that is the exploit, not the sign at +4.

`BeatType.Tag` is on-type for `Technical`, so the exploit was largest exactly where a player
would declare it — and Technical is also where the counted curve turns latest, which is worth
knowing on its own — and `Validate` permits it at two a side as well, since a `Quick Tag` with
no `IncomingIndex` alternates 0→1→0→1 forever. A hand-built plan of opening + 40 tags +
finish went from the −5 clamp to a nudge of exactly zero, because `VarietyBeatCount` fell to
2 and the "too short to judge" guard silently became "not judged at all".

A 0.05★ cosmetic gap traded for a 0.4★ exploit, shipped with no test. **Reverted.**

`Six-Man War` keeps its 0.049★ deficit, and this time the deferral has the right reason
attached: the engine is correctly observing that seventeen beats using twelve distinct types
repeats more than thirteen using eleven, and three isolations and three tags is more
repetition than two and none. Separating "variety" from "the padding brake" so that
connective beats can be forgiven by one and not the other is a rating-formula change that
genuinely has nothing to do with trios — which is what I claimed the first time, for the
wrong reason.

`PaddingATagMatchWithTags_IsNotFree` is the guard that came out of it, in the shape of the
existing "length is still a substitute for quality" test but aimed at a tag plan. It fails
with the exemption restored.

### The rest

| # | Found | Fix |
|---|---|---|
| N1 | *"Five tag changes in thirteen beats, three of them on the rudo side"* — in a code comment, the build log and the commit message. It is **four**, and **two**. The ten lines underneath say so. | Corrected in all three. |
| R1 | The withdrawn "two corners" claim was still being **shown to the player**, in the side-size explainer and in `SideSizeBlurb(3)`. A trio has one corner — the engine's own `NameCorner` returns the singular for exactly that reason — and the structure books three isolations and two near tags, not two and two. | Both rewritten to say what the structure actually books. |
| N2 | §2.5's sim-implications block says "need **three** things" and then lists four. Introduced by the fix for finding 11, which split point 1 in two. | "four things". |
| N4 | *"Both structures below name every incoming member explicitly"* — false of `Six-Man War`'s `Hot Tag`, which relies on next-man-round. | Reworded. The property that matters — all six legal — was never in doubt. |
| N5 | "rapid tag rules" is doc 25 §3.3's **second** bullet, not its first. | Corrected, and §3.3's dropped first bullet ("more people on every card") restored to the §2.5 summary. |
| R2 | §2.5 attributed "three falls traditionally" to doc 25 §3.3. It is §3.1. | Corrected, with the section named inline. |
| R3 | §9's "unexplained third man" and §2.5's "characteristic failure" cited for a case §2.5 explicitly exempts. | Noted inline in the round 1 section rather than deleted — the defect was real without the borrowed authority, and the overreach is worth keeping visible. |

### Two findings recorded and deferred, with reasons

* **M1.** `Lucha Trios` names all six individually in 300 of 500 matches. Side A's starter is
  only ever named by the `Hot Start` opening, and two of that beat's **five** templates name
  nobody — hence three in five. (I wrote "four templates" in the first draft of this bullet,
  which the 300/500 in the same sentence contradicts. Round 3 caught it. Side B's starter is
  also named by `Shine`, so it is A's starter that binds.) The Description says "all six
  work", which is true of the *booking* in every match and visible in the *commentary* in
  three out of five. The new test measures the aggregate over seeds and is honest about doing
  so, but the gap is real.
* **M2.** Neither front end exposes `IncomingIndex`, so a player hand-building a trios cannot
  name who comes in — everything falls through to next-man-round. Pre-existing, correctly
  persisted, and the presets cover the common case; but the third man only works if you take
  a preset and do not edit it.

Both belong with the UX pass, along with the commentary calling every wrestler "him".

**435 tests passing.** Equivalence across 353,808 dumped lines (every non-feud-gated
structure × every match type × 14 wrestlers × both side sizes × 3 seeds, per-beat deltas at
`"R"` precision plus full commentary): **zero non-commentary differences**, zero differences
of any kind at one a side, and the 2v2 differences are 560 instances of the one near-tag
template rewritten for finding 5. Round 3 reproduced this independently on a 145,200-record
harness of its own and got the same result.

---

## Trios — review round 3

Round 3's verdict: *"not safe to merge. This round it is the engine, not the text."* The
seven text fixes were all made and correct, the byte-identity claim held under an independent
harness, and every number in the round 2 section reproduced exactly — and the substantive
change I made beyond what round 2 asked for opened a rating exploit larger than the gap it
closed. That is written up in the round 2 section above, where the change was made, rather
than only here.

The short version: the variety nudge is doing two jobs, and I only noticed one of them.
Reverted, guarded by `PaddingATagMatchWithTags_IsNotFree`, and the 0.049★ deferred with the
correct reason this time.

Round 3 also caught one more claim my own sentence denied — the M1 bullet said "four
templates" beside a measurement that implies five — corrected above.

Three rounds, and the pattern in every one of them was the same: the engineering held up
under everything three reviewers could throw at it, and what did not hold up was what I said
about it. Worth stating plainly at the end of this file rather than leaving implied.

---

## A3 — feud decay and the terminal blow-off

Doc 31 lists A3 in phase 1, "make results matter". It is the oldest unbuilt item on the
list and the reason is visible in the code it replaces: `Feud.Heat` only ever went *up*.
Every segment ever booked was still paying off months later, a feud left off television for
a year was as hot as the night it started, and there was no cost whatever to starting five
programmes and finishing none. Doc 20 §9 spends a section on exactly that booking.

Three rules, each answering one line of the A3 brief.

### Neglect costs

`Feud.ApplyDailyDecay`, charged by `Career.AdvanceOneDay` alongside the momentum, title and
chemistry decay that were already there. Fourteen days of grace, then 0.955 a day. §9 lists
"a feud left off TV for three weeks loses its heat" among the things that kill one, and
three weeks off television now costs 27.6% of it, a month 52%, two months 88%. (I first
wrote "about a third" for the three-week figure and the code comment said it should cost
"most of it"; neither is what 0.955 with a fortnight of grace actually does. Review measured
it. The constant is the thing to trust, and fourteen days of grace is what makes three weeks
modest — a feud is not punished for missing one week of television, and §9 does not say it
should be.)

Written with `DecayedTo` from the start, because `TagTeam.Decay` shipped without it in phase
4 and compounded quadratically — `0.9985^(N(N+1)/2)` instead of `0.9985^N`, a thirty-day
half-life on a curve written for two years. `DecayIsIdempotentPerDay` fails if the marker is
removed, if it is stamped inside the grace period (the second half of that bug), or if the
grace is dropped; `TheGracePeriod_IsRealAndIsNotCharged` catches the second and the third.
See the round 1 section at the end of this file — the first version of this sentence claimed
both tests caught all three, and review measured that they did not.

### Not paying off costs, durably

`RecordUnresolved` runs after every match that was not declared a blow-off. Three matches
are free — doc 20 §9.1's "three matches is the natural life of a feud" — and the fourth
starts accruing `Distrust`, which suppresses `StartingEnergyBonus` through `Credibility`.
This is §9's interference loop: every match ends in a run-in, nothing resolves, and the
crowd learns not to invest. The lesson is durable: `BlowOff` refunds some of it, and a new
chapter inherits the rest.

Two rules came out of testing rather than out of the brief, both because the first draft was
wrong:

* **A settled feud can be started again.** `FeudBook.GetOrCreate` returns the same object
  for a pairing forever, so `Concluded` as written meant two people who ever finished a
  programme could *never* feud again. The rematch years later is one of the oldest things in
  wrestling. New heat on a settled feud now opens a new chapter and resets the patience
  clock — but not the distrust.
* **A feud that cools below Hot resets the patience clock.** Otherwise a programme that
  quietly died of neglect came back a year later already one match from the limit.

### Paying off pays — and has to actually pay off

`MatchPlan.IsBlowOff` is a booker declaration rather than something derived from the beats,
because that is what it is in real booking: nothing about a match's shape makes it a
blow-off. It multiplies the *finish* beat — the payoff is the payoff, not a blanket bonus,
and `DeclaringABlowOff_MovesTheMatch_AndOnlyTheFinish` asserts every earlier beat is
identical to nine decimal places.

Priced by what was built, §6's second requirement: ×1.45 Nuclear, ×1.28 Hot, ×1.05 Building,
**×0.72** for a story the audience was never told mattered — worse than not declaring one,
the same shape as the unearned finish in `ApplyFinish` and the unearned hot tag.

And §6.1's *first* requirement, which the first draft ignored: a blow-off has to **resolve**.
Booked to a disqualification, a count-out or a run-in it settles nothing, the feud stays
open with its heat intact, and it costs 0.30 distrust against an ordinary unresolved match's
0.18 — 1.67×, not the "double" the code comment first claimed. That is what makes declaring a blow-off a decision with a downside
rather than a free multiplier, and it is checked end to end through `ShowSimulator` rather
than only on the model.

### What it is actually worth

`Big Match Epic`, two 80-overness workers, 300 seeds a cell:

```
no feud                          3.7364
Cold      chapter 3.7621   blow-off 3.7250   Δ −0.0371
Building  chapter 3.7945   blow-off 3.8008   Δ +0.0063
Hot       chapter 3.8628   blow-off 3.8949   Δ +0.0321
Nuclear   chapter 3.9232   blow-off 3.9711   Δ +0.0479
Nuclear, clean 3.9232  vs 10 unresolved 3.8749   Δ −0.0483
```

Stating this plainly rather than letting the multiplier imply more than it does: **the star
rating moves by about 0.05**. That is a quarter of what having a Nuclear feud is worth at
all (+0.19★ over none), which is proportionate — but it is not, on its own, a headline
feature, and a ×1.45 on one beat of nine was never going to be. The teeth of A3 are
elsewhere: heat now has to be maintained, distrust is durable, and a blow-off that does not
resolve is punished.

**What A3 asked for and this does not deliver:** "a blow-off should pay out accumulated heat
as a large one-time result". There is no business axis to pay into — doc 18 §7's
quality-versus-draw split is the prerequisite and it is not built. The quality and story
halves ship; the business half is blocked on work nobody has started.

### Persistence

`LastAdvanced`, `DecayedTo`, `Concluded`, `ConcludedOn`, `MatchesSinceHot`, `Distrust` and
`ChaptersSettled` on the feud; `IsBlowOff` on the card item. A pre-A3 save has no
`LastAdvanced`, and the fallback is `LastMatchDate` rather than null **deliberately**: null
means "never advanced" and therefore never decays, which would quietly exempt every feud in
an existing save from the rule this release adds. Two round-trip tests, one of which checks
the decay *clock* survives — not just the numbers — because a reload that hands every feud a
fresh grace period is the same bug wearing a different hat.

### Reachable in the game

The blow-off is a real choice in both front ends: a toggle in the web builder's feud step
that quotes the multiplier before the choice is made and warns when it would be unearned or
when the pairing has been run into the ground, and a priced prompt in the console flow.
Booking a blow-off with no feud, or a second one on a settled story, fails validation with
an error that says which.

**436 tests passing** (18 new). Every one of the seven new mechanisms was checked by
inverting it and confirming the intended test went red.

---

## A3 — review round 1

Verdict: **safe to merge, with two documentation corrections that should be made first.** The
model was found correct, wired in, and — unusually for this build log — measurably alive in
real play. What the review took apart was the *testing*, and it was right to.

### Four mutations survived, and every one of them was a hookup

The review deleted twenty things and sixteen died. The four survivors were not rules; they
were the four lines that connect the rules to the game.

| Deleted | Suite | Now fails |
|---|---|---|
| `Career.AdvanceOneDay`'s call to `ApplyDailyDecay` | 436/436 green | `TheWorldClock_CoolsAFeudNobodyIsTelling` |
| `ShowSimulator`'s call to `RecordUnresolved` | 436/436 green | `AShowThatSettlesNothing_ChargesThePairingForIt` |
| `Advance()` clearing `DecayedTo` | 436/436 green | `AdvancingAFeud_RestartsTheClockRatherThanResumingIt` |
| the pre-A3 save fallback | 436/436 green | `AFeudFromAPreA3Save_StillCools` |

Every test I wrote drove the model directly. So the headline mechanism's only hookup could be
deleted and CI would say fine — while the *analogous* mechanism, title drift, has had a
150-day `AdvanceOneDay` integration test since phase 5. I tested the rule and not the wire,
four times, and did not notice because the rule tests are the interesting ones to write.

The third of those has a measured cost, which is worth recording because it is invisible: a
feud ignored for forty days and then re-booked keeps 18.12 heat with that line and drops to
9.51 without it — **47.5% of what remained** — and the grace period is silently skipped from
then on. That is one of the two halves of the `TagTeam` bug I said I had guarded against.

### A test documented to catch a bug it could not catch

`TheGracePeriod_IsRealAndIsNotCharged` says in its own comment that it exists to catch the
marker being stamped inside the grace — *"that is the bug `TagTeam.Decay` shipped with and it
is the same shape here."* The review applied exactly that mutation. **It passed.** It also
passed with the marker removed entirely.

Why: it called `ApplyDailyDecay` only on the *last* day of the grace, where `today == from`
and stamping is harmless. It never called mid-grace, which is what the world clock does every
single day. So two claims in the last section were false:

* the build log's *"both fail if the marker is removed, if it is stamped inside the grace
  period, or if the grace is dropped"* — measured, the grace test passes on the first two;
* the PR body's *"the two halves of the `TagTeam.Decay` bug, each of which fails a different
  test"* — measured, both halves fail the *same* test, and neither fails the one written for
  them.

The suite did catch both halves. Just not where I said. Fixed by ticking the grace day by day.

### Two constants documented as something they are not

* `HeatDailyRetention`'s comment said three weeks off television *"should cost most of it"*.
  It costs **27.6%** — a month costs 52%, two months 88%. Fourteen days of grace is what makes
  the three-week figure modest, and doc 20 §9 does not actually ask for more than that; the
  comment was writing a stronger rule than the constant implements. The build log's softer
  "about a third" was a stretch of the same number.
* `RecordBrokenPromise`'s comment said a broken promise *"pays double"* an ordinary unresolved
  match. It is 0.30 against 0.18 — **1.67×**.

Both corrected against the measurement rather than the measurement adjusted to the prose.

Seven of nine constants are unconstrained by any test, which the review is right to flag. The
two decay tests that look like they pin values compute their expectations *from* the
constants, so they pin the shape — geometric, idempotent, graced — and not the numbers. I have
left that as it is deliberately: the shape is the part with a right answer, and pinning
`0.955` to a test would only mean editing two places when the balance changes. It is recorded
here so the next reader knows it is a choice rather than an oversight.

### Two real defects

* **A pre-A3 feud built entirely from segments never decayed at all.** The fallback chain was
  `LastAdvanced ?? LastMatchDate`, and `RecordSegment` took no date before A3 — so a feud
  built out of promos for a month has neither, loads as "never advanced", and
  `ApplyDailyDecay` returns immediately, for ever. The review measured one sitting at 70 heat
  after 400 simulated days. *"Build it with promos, then have the match"* is an ordinary way
  to book. The chain now ends at the save's own clock.
* **The web builder latched `isBlowOff` across a change of pairing.** `Next()` reset
  `existingFeud` and `feudChoice` and not this, so ticking the blow-off for one pair and then
  going Back to pick another whose feud was already settled produced *"has already been blown
  off"* at validation with the toggle hidden and no control on screen to clear it. A dead end.

### The brief had a bullet I marked done and had not built

Doc 31's A3 entry asks for four things and I implemented three, marked the whole entry
**implemented**, and noted only the *other* omission (no business payout). The unbuilt one:
**"continuing past the blow-off should be penalised"**. As shipped it was not merely unbuilt,
it was free — `AddHeat` reopened a settled feud at no cost, which lets a booker take the
payoff and keep the programme, and is worse than having no blow-off at all.

Built now, and deliberately time-sensitive, because the two cases are different bookings.
Restarting a fortnight after the cage match tells the audience the ending they were sold did
not count — doc 20 §6.2's scarcity argument — and costs 0.25 distrust. Reviving the same
rivalry two years later is one of the oldest and best things in wrestling and costs nothing.
Six months is the line. `ChaptersSettled` and `ConcludedOn`, which the review correctly called
write-only state, are what the rule reads.

`Conclude()` — the draft path, which separates two people rather than settling anything — now
resets the patience clock without setting `Concluded`, because a programme taken off the
player is not one the player refused to pay off. Two things were called "conclude" and only
one of them meant it.

### What I am not changing

The review measured `Distrust` saturating in about nine matches past patience, after which it
is a flat −45% on `StartingEnergyBonus` and stops discriminating — worth roughly **−0.03★**.
That is small, and the honest response is to say so rather than inflate the constant: doc 31's
entry and the PR body both now say plainly that the rating movement is not the point and that
calling distrust "the teeth" of A3 overstated a ≤0.05★ nudge. What A3 actually does is put a
clock on heat. Making distrust matter more means giving it a second consumer — a business
axis, doc 18 §7 — which is the same prerequisite the missing blow-off payout is waiting on.

**443 tests passing** (25 new). All four surviving mutations now fail the intended test,
verified one at a time against the full suite.


---

## A3 — review round 2

**Safe to merge**, with three one-liners, all of which are done. Every one of round 1's four
surviving mutations now dies, and both links of the save-fallback chain have their own
killer — dropping only the `?? career.CurrentDate` tail kills exactly the promos-only case
it was added for, which is the right shape.

Round 2 found the round-1 pattern once more, in miniature and twice:

* **`51%` was not any reading of the curve.** A month is 52.13% (28 days is 47.5%, 31 is
  54.3%). Wrong by 1.1 points, in the conservative direction, in two documents. Corrected.
* **The reopen penalty's *wire* was untested**, which is exactly what round 1 spent four
  mutations on, in the one mechanism this branch had just added. The rule only reads today's
  date because `FeudBook.Record` calls `Advance` before `AddHeat`; reverting that reorder
  charges a two-year revival as though it were a fortnight, and left all 443 green.
  `ARevivalBookedThroughTheFeudBook_IsNotChargedAsAContinuation` now books through the book
  rather than the model, and dies to the reorder.
* And the sentence claiming both decay tests caught all three marker bugs was still standing
  in the section where it was written, quoted as false ten lines away in another section but
  not corrected in place. Corrected where it was made.

One thing round 2 measured that is worth keeping visible rather than fixing: `BlowOff`
forgives 0.35 and reopening too soon costs 0.25, so a booker who blows a feud off and
immediately restarts it nets **−0.10 distrust per cycle**, converging on a floor of 0.25.
The payoff-and-keep-the-programme loop is *priced*, not closed. Closing it means making the
refund conditional on the ending being respected, which is a rule about the next chapter
rather than this one — recorded rather than done, so the next reader knows it is a choice.

**444 tests passing.**

---

## The bigger roster — review round 1

Verdict: **safe to merge.** Every headline claim survived measurement, including the one I
flagged in the PR body as most worth scrutinising — the `MatchMatrixTests` caching change.
Worth recording what the reviewer did to it, because it is the shape of scrutiny this change
needed and I had only argued for it in prose:

* Diffed the sweep body and found it byte-identical — no loop bound, seed, threshold or
  assertion changed; the optional `runsPerCell` was never once overridden, so caching could
  not narrow anything.
* Built **uncached twins** of three engine mutants and confirmed cached and uncached produce
  identical pass/fail verdicts.
* Mutation-tested five ways. The base branch killed **0 of 5**; the branch kills **1 of 5** —
  a mutant that makes `BeatControl` a no-op now fails `Ratings_SpanAWideRange`, where on the
  thirty-person roster the same mutant slipped through. Detection got *better*, not worse.
* Checked the memory hypothesis I had not thought of: the cache **saves** 186 MB of peak RSS
  (471 vs 657), because holding one matrix beats churning eight.

Four findings, all minor, all fixed:

| | |
|---|---|
| `SeededTeams_AreSkippedRatherThanBrokenWhenAMemberIsMissing` dropped `Roster.Skip(1)` — Demi Bennett, who is in **no** tag team. Measured `full=9, missing=9`: both assertions passed vacuously and the arm never exercised the path it is named for. | Removes an actual team member now, and asserts the count *falls by exactly one*. |
| "grew from 500k matches to 1.8m" in the test's own comment and the PR body. Measured: **271,440 → 1,778,400**. The second figure is right; the first was overstated ~1.8×. | Corrected, with the real numbers. |
| "`RosterDifferentiationTests` names eight of the original thirty" — it names **eleven**. All eleven are still present, so the substance held. | Noted here rather than restated. |
| `Assert.InRange(mean, 2.8, 4.3)`: the sweep mean fell 3.35 → 2.99 because the additions are weighted to the lower card, so the margin to the floor went 0.55 → **0.19**. | Left as it is — it is currently the assertion doing the mutation-killing — with a comment saying the next tranche of enhancement talent will trip it and that the failure will be a data change wearing an engine regression's clothes. |

### And one real gap the roster made reachable

`Draft.Apply` explicitly ends **feuds** split across brands — *"there is no show left on which
to continue it"* — and had no equivalent for tag teams, while `DraftBoard` picks individuals
with no team awareness. Review measured an AutoPick draft on the shipped roster splitting
**five of the nine seeded teams across brands**, all left `IsActive` and quietly decaying
chemistry for a pairing nobody could ever book again.

This was unreachable before: `career.Teams` was empty at career start, so a day-one draft had
nothing to split. Seeding nine teams ships it reachable. Fixed with the same rule the feuds
get, `TeamsDisbanded` reported alongside `FeudsEnded`, and two tests — one that the draft
breaks up a team it separates, one that it leaves alone a team it keeps together.

### The caveat the review drew out, which the PR body already had right

Distinct rating tiers went 16/30 (53%) → **23/76 (30%)** while the total spread barely moved
(1.342 → 1.356): 53 of the 76 land within 0.02 stars of a neighbour. The additions fill in
*between* existing positions rather than extending the range. That is crowding, not cloning —
no two entries share an attribute vector, and the closest new pair is *less* similar than the
closest pre-existing one — and it is what a bigger roster at a fixed quality ceiling has to
look like. Recorded because the honest reading of "23 tiers" is denser, not wider.

**467 tests passing.**

---

## The commentary called everybody "him"

Flagged during the trios round and deferred twice — once because fixing it would have muddied
that PR's byte-identity claim, once into the UX pass. Both were reasonable at the time and
neither is any more: the roster is now **thirty-eight women and thirty-eight men**, the match
builder supports intergender bookings on purpose and *warns* about them rather than blocking
them, and a near tag in a women's tag match read:

```
Rhea Ripley reaches — and Bianca Belair drags him back!
```

Eight interpolated templates in `MatchEngine`, three beat descriptions, and one line of UI.

Wrestling's own vocabulary is left alone where it is the name of a thing. A six-man tag is a
six-man tag; the face in peril is the face in peril; "the legal man" is what the rule is
called. What changed is the pronouns that attach to a *named performer* — and the referee's,
who has no stated gender either.

Most of them wanted rewriting rather than substituting, because "they" dropped into a sentence
written for "he" usually reads worse than the sentence deserves:

| before | after |
|---|---|
| `{other} is a long way from his corner` | `…a long way from that corner` |
| `Every time {other} gets to his feet, {control} drags him back` | `Every time {other} gets back up, {control} drags them down again` |
| `{fresh} comes in. He had not been out there long enough for anyone to miss him.` | `{fresh} comes in — not that anybody had time to miss them.` |
| `The referee finally reaches his limit` | `The referee has finally had enough` |
| `He reaches for the corner and is dragged back` (Near Tag's description) | `A hand reaches for the corner and is dragged back` |

That last one is there because **the test caught it and I had not**. My grep was
case-sensitive, so a template beginning `"He reaches…"` went straight past it. The guard is
`NoBeatTemplate_AssumesAWrestlersGender` plus `NoCommentaryLine_AssumesAWrestlersGender`, which
runs 6,528 real commentary lines through the engine at one, two and three a side and scans
them **after stripping the ring names** — otherwise a surname containing "her" or "his" would
false-positive for ever and the test would be quietly disabled within a month.

**Every number is unchanged.** `Pick()` selects by index, so rewriting the strings cannot move
an RNG draw. Verified rather than asserted: 131,328 dumped rows — every non-feud-gated
structure × every match type × 12 wrestlers × all three side sizes × 3 seeds, per-beat deltas
at `"R"` round-trip precision — are **md5-identical** to `main`.

**471 tests passing.**
