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

## A5 — the crowd reaction vector

The first item from the original gap analysis rather than the tag-match plan, and the one
[31](wrestling-reference/31-sim-mapping.md) ranked highest of what was left. Doc 18 §2.5 also
makes it a **prerequisite** for real multi-man matches: `Advantage` is a scalar running −100
to +100 and therefore two-poled, and the quantity that matters in a three-way is not who is
winning but who the room is watching.

### The problem with one number

`CrowdEnergy` could not answer *is anybody invested?*, and it conflated the two quiet rooms
that mean opposite things. Phase 2 hit this directly: the denied tag takes energy **out** of
the building, and the adjudication had to describe that in a comment as "stored energy"
because the engine had no way of saying it. An overworked isolation also takes energy out of
the building, and means the opposite. One number cannot hold both.

### What was built

`CrowdReaction` accumulates five components — pop, heat, tension, silence, go-away heat —
following the taxonomy in [16](wrestling-reference/16-crowd-psychology.md) §2. The readings
that matter are `Engagement` (everything except the two kinds of not-caring), `Investment`
(the share of the night the audience was present for) and `Dominant`.

Two design points worth stating:

**Heat is engagement.** A crowd booing somebody it wants beaten is present, and the
face-in-peril structure runs on exactly that. Treating boos as a bad outcome would have been
the engine making the mistake §2.1 says the business makes.

**Reaction is a distribution, not a label.** The first implementation classified each beat as
exactly one kind, and the feature was almost inert — 84% of matches unchanged, biggest move
0.086★ — because a binary threshold put the median performer exactly on the line. Splitting
the weight by connection is what made it bite.

### Two mistakes worth recording

**Double-counting.** Investment was first applied as a straight multiplier on the crowd
component. But `CrowdCeiling` *already* scales the whole crowd axis by how much the audience
cares about the pairing, so this charged low connection twice — and, worse, compressed every
difference that lives in the crowd component, which is most of the engine's discrimination.
Six unrelated tests failed at once: the WM20 and WM34 recreations, feud payoff, staleness
decay, conditioning, and the tag carry test. That is what double-counting looks like from the
outside. The multiplier is centred now, so a typical match scores where it always did.

**A symmetric swing pushed 2.15% of all matches to a flat 5.00.** The pairings that draw the
most investment are already near the ceiling and had nowhere to go. The clamp is asymmetric —
0.65 down, 1.06 up — and that asymmetry is the design rather than a tuning convenience: doc 16
§2.1 is about the *cost of silence*, not a bonus for engagement.

### Result

**429 tests passing**, with every pre-existing threshold intact — including the real-match
recreations, which is the actual guard on engine behaviour.

Singles ratings **do** change here, deliberately and for the first time in this body of work.
The byte-identical contract existed so that tag matches would not disturb singles; A5 is a
change to how crowd reaction is modelled, and it is supposed to move singles. The guard is
the recreations and the distribution tests, not equality.

Measured: the same plan worked to the same standard rates **2.17★ in front of a room that
never turned up and 3.99★ in front of one that did.**
