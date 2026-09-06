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
