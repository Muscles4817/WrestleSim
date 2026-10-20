# 31 — Mapping to WrestleSim

*What the engine models today, what the reference says is missing, and a prioritised view of
what to build next.*

This document is **opinion about design**, unlike the rest of the reference which is
description of the industry.

**Audited against `ece8cd9`.** Every status claim below was checked by reading the code at
that commit. Where a figure is an engine *output* rather than a constant it carries the commit
it was measured at, because those two go stale at different rates and only one is checkable by
reading.

This replaces a standing "check the code before trusting the current state column", which is
what let the board drift: a caveat that applies to everything excuses keeping nothing current.
If you are reading this a long way past `ece8cd9`, the honest move is to re-audit and move the
hash, not to re-add the hedge. §2.1 records what drifting cost last time.

---

## 1. What the engine already gets right

Worth stating clearly, because several of these are subtle and correct:

| Model | Where | Why it's right |
|---|---|---|
| **Beat-based matches rather than a single roll** | `MatchEngine`, `MatchBeat` | Matches are structures, not outcomes. This is the correct primitive. |
| **Three-component rating** (technical / storytelling / crowd) | `MatchEngineResult` | Separates the axes that a single star rating conflates. See [18](18-match-craft.md) §8. |
| **Per-pairing crowd ceiling** | `MatchEngineState` | The most important truth in the business: craft cannot substitute for the audience caring who you are. See [16](16-crowd-psychology.md) §1. |
| **Crowd gains compress near the ceiling** | `MatchEngineState` | Correct — the last 20 points of a reaction are much harder than the first 20. |
| **Diminishing returns on repeated beat types** | `MatchEngine` | Reproduces the real near-fall/run-in/heat-segment decay. |
| **Unearned finish penalty** | `MatchEngine` | Directly implements [18](18-match-craft.md) §5.2 rule 1. |
| **Crowd disposition from fan-group appeal, not alignment** | `Gimmick.AppealRatings` | Correct and unusual — alignment is a booking label, disposition is what the audience actually feels. |
| **The cool-heel `tensionFactor`** | `MatchEngine` | A genuine structural insight, correctly modelled. See [16](16-crowd-psychology.md) §2.2. |
| **`CardPosition` derived from overness** | `Wrestler.CardPosition` | Position as an output of audience perception, not an input. (Reads `EffectiveOverness` since A1; the row said "popularity", which is the pre-split name.) |
| **Feud heat accumulated by booking, intensity derived** | `Feud.Heat` | "You don't declare a feud, you book one" — exactly right. |
| **Feud history tags gating beats** | `FeudHistoryTag` | Reproduces how real callbacks work: you can't do the revenge spot without the betrayal. |
| **Show position multipliers and same-kind fatigue** | `ShowSimulator` | Card composition as a real constraint. See [04](04-booking-philosophy.md) §3.8. |
| **Crowd mood carrying between items** | `ShowSimulator` | The crowd is a continuous state across a show, not per-match. |
| **Runtime budget with an overrun penalty** | `Show.OverrunFraction` | A real television constraint. |
| **Gimmick freshness that decays** | `Gimmick.Freshness` | The single most important hidden variable in wrestling, already present. |
| **Roster attributes spanning most of their legal range** | `RosterAttributes_UseMostOfTheirLegalRange` test | Enforcing spread so archetypes are distinguishable — a genuinely good engineering decision. |

Added by work after this list was written, and right for the same kind of reason:

| Model | Where | Why it's right |
|---|---|---|
| **Expectation derived, not declared** | `MatchExpectation` | A booker never announces "this will be a technical classic". The promise comes from who is in it and what is at stake; the delivery is the beat sheet. Replaced `MatchType` as an input, which had asked the player to choose their own grading criteria. |
| **Fatigue and sharpness as opposed meters** | `RingCondition` | Rest fixes one and breaks the other, so there is no correct amount of time off — only a trade. A single health number cannot express that. See B7. |
| **Exertion kept separate from danger** | `RingCondition`, `InjuryRisk` | A sprint can cost more than a long mat classic; a ladder match is dangerous without being tiring. Modelling them as one axis would get both wrong. |
| **Injury return as a date, not a countdown** | `Models.Person.Injury` | The clock jumps when a show is run weeks out, so anything decremented per `AdvanceOneDay` would silently skip those weeks. |
| **A stipulation defined by what it removes** | `StipulationRules` | A cage match is "nobody escapes, nobody interferes" — the finishes it forbids, not a cosmetic label. Makes the ladder of stipulations mechanical. |
| **A battle royal priced as elevation, not as 29 losses** | `HeatEconomy.ForRumble` | Nobody in it was beaten. The cheapest way to make somebody without spending anybody — and the iron-man run pays even without the win. |
| **Touring held as a standing instruction** | `Wrestler.TouringUntil` | A promotion puts people out for a stretch; it does not re-decide every Saturday. Dropping somebody from one night does not end their tour. |

---

## 2. The gap analysis

The Tier A/B/C entries were ordered by **value-per-unit-of-work** when they were written.
They are no longer in build order and §4 explains why; read the tiers as a grouping by size,
not as a queue.

Entries this analysis never listed — tag matches among them — are in §2.2 rather than below.

### 2.1 What the gap analysis got wrong about itself

This board was written as a list of things to build, and for a while it was read as one. It
is now mostly a record of things built, and twice it has been found describing shipped work
in the future tense — once for A1/A2/A4, and once more seriously below. Both times the cause
was the same: **an entry states a gap, the gap gets filled by work that never looks at the
entry, and nothing closes the loop.**

The largest instance: **B7 Injuries** sat open while a fatigue, ring-rust and injury system
was built, tested across ~1,300 lines, and merged. A plan was then written proposing to build
it, which is the failure in its most embarrassing form. That plan was discarded and this
section exists so the next one is not written.

A binary open/closed flag cannot express the other two shapes this audit found, so both are
spelled out: **partial**, where some of a brief shipped and the rest did not (B4, B5), and
**past its brief**, where what shipped answered more than the entry asked (B7). Both are more
common than a clean open-to-closed transition, which is most of why this board drifted.

| Board entry | Was | Is now |
|---|---|---|
| A1, A2, A4 | open | **implemented** — corrected above |
| B7 Injuries | open | **implemented**, and past its brief |
| C2 Brands | open | **implemented** |
| B5 Markets | open | **partial** — the touring loop shipped, markets did not |
| B4 Promotion layer | open | **partial** — tier and calendar shipped, revenue and the TV deal did not |
| B3 Segments, C3 Regional modes | open | **open**, but some ingredients arrived by other routes |
| A6, A7, A8, B2, B6, C1, C4, C5 | open | **still open**, each verified by reading |

A3, A5 and B1 were already marked implemented and were re-checked rather than changed.

### 2.2 Shipped, and never listed here at all

The analysis below has no entry for any of these. That is not a backlog — it is work that is
merged and reachable, and the board's silence about it was the real staleness.

| Shipped | Where | Reference |
|---|---|---|
| **Tag, trios and multi-man** — sides, legal-man tracking, the hot tag, blame for the fall | `MatchSide`, `MatchEngineState`, `PhaseGrammar` | [18](18-match-craft.md) §2.5; [../tag-matches-plan.md](../tag-matches-plan.md) |
| **Stipulations** — five rungs, each defined by the finishes it removes, with validation | `Stipulation`, `StipulationRules`, `StipulationBook` | [20](20-storylines-and-feuds.md) §6.2 |
| **Battle royals and the Rumble** — judged on moments rather than on work, with a draw order that means opposite things to opposite people | `RumbleEngine`, `RumbleScoring`, `RumbleDrawEngine` | [18](18-match-craft.md) |
| **Elimination formats** — falls remove people, handicap, Survivor Series | `MatchEngine`, `MatchPlan` | [18](18-match-craft.md) §2.5 |
| **House-show loops and touring** — a cast and a number of towns, as a standing instruction rather than a booking | `HouseShowLoop`, `LoopSimulator`, `Wrestler.TouringUntil` | [07](07-live-events-and-touring.md) |
| **Match briefs and the phase grammar** — the booker states the booking and the game writes the match | `MatchBrief`, `BriefDirector`, `PhaseGrammar`, `BriefCritique` | [18](18-match-craft.md) §2.3 |
| **Derived expectation** — what a match promises, read off who is in it and what is at stake, replacing `MatchType` as an input | `MatchExpectation` | [18](18-match-craft.md) §3.2 |

The brief-and-phase-grammar layer deserves singling out because it is an **architectural**
change this board never anticipated: `MatchType` asked the booker to declare how their match
should be graded, which is not a decision a booker has. Expectation is now derived and
delivery is the beat sheet, and the interesting quantity is the distance between them. Nothing
in the gap analysis below is written with that in mind, and some of it would be framed
differently if it were.

### Tier A — high value, contained work

#### A1. Split `Popularity` into `Overness` (stock) and `Momentum` (flow) — **implemented**
**Reference:** [17](17-heat-and-getting-over.md) §1.1
`Popularity` did both jobs. Splitting them enables hot/cold dynamics, the peak-and-cash-in
decision, cooling from absence, and the entire heat lifecycle.
- `Overness` — slow-moving accumulated audience relationship; drives crowd ceiling
- `Momentum` — fast-moving current trend; drives reaction growth week over week and
  decays without exposure
- `CardPosition` should read `Overness`; crowd energy should read both.

Shipped as `Wrestler.Overness` and `Wrestler.Momentum`, combined by `EffectiveOverness`
(`Overness + Momentum × 0.15`) which is what `PerformerProfile` and the opening bell read —
being hot lets someone punch above their standing without a mid-carder reading as a main
eventer. Decay is `HeatEconomy.ApplyDailyDecay`, charged by `Career.AdvanceOneDay`: momentum
retains 0.967/day (a three-week half-life), and overness itself slips 0.06/day once someone
has been off screen past a 21-day grace, damped so a cold performer has little further to
fall.

Two things worth recording. **`Overness` is a `double`, not an `int`** — as a whole number
every change below half a point rounded to nothing and small transfers could never
accumulate, which surfaced as a botched segment costing a wrestler exactly zero.
And `MatchEngineState.Momentum` was renamed `Advantage` in the same change: it means
in-match advantage and resets every match, and leaving it sharing a name with career
momentum was a trap.

#### A2. Heat transfer on match results (status economics) — **implemented**
**Reference:** [17](17-heat-and-getting-over.md) §6, [12](12-pushes-and-positioning.md) §6.1
The engine scored match *quality* and the result had no status consequence, so booking a
squash and booking the upset were mechanically identical.
- Beating a higher-`Overness` opponent transfers a meaningful amount
- Beating a lower-`Overness` opponent transfers almost nothing and costs the loser
- Losing without a story (clean, no interference, no stipulation) costs more than losing
  with one

Shipped as `HeatEconomy.ForMatch`. Transfer scales with `prize` (you can only take status
from someone who has it), a `gap` term measured from the winner's side, match quality, and
`FinishWeight` — `Decisive` 1.00, `Fluke` 0.50, `Protected` 0.35, so the audience forgives a
loss it understands. Gains compress near the ceiling and losses near the floor.

Measured on the shipped roster at `3da5c40`, both in a good match:

```
  Roman Reigns (96) beats Von Wagner (23)      Von Wagner (23) beats Roman Reigns (96)
    Roman      overness  +0.00  momentum  +1     Von Wagner  overness  +5.12  momentum  +48
    Von Wagner overness  −0.59  momentum −10     Roman       overness  −0.21  momentum   −3
    → net status destroyed                       → a star is made, at almost no cost
```

The squash gains the star **literally nothing** while costing the roster real value, which
is §6's central claim made mechanical.

#### A3. Feud heat decay and blow-off as a terminal event — **implemented**
**Reference:** [20](20-storylines-and-feuds.md) §9, §6, [04](04-booking-philosophy.md) §1.1
- Feud heat should **decay** when the feud isn't advanced (currently it only accumulates)
- A **blow-off** should pay out accumulated heat as a large one-time result and *end* the
  feud
- Continuing past the blow-off should be penalised
- Never blowing off should eventually convert heat into audience distrust

Shipped as `Feud.ApplyDailyDecay` (charged by `Career.AdvanceOneDay`, 14 days of grace then
0.955/day — three weeks idle costs 27.6%, a month 52%, two months 88%), `MatchPlan.IsBlowOff`
(a booker declaration, priced by `Feud.PayoffFor` at ×1.45 Nuclear down to ×0.72 for one
nobody was told mattered), and `Feud.Distrust` — accrued by `RecordUnresolved` past the third
match, at 1.67× that rate by `RecordBrokenPromise` when a declared blow-off is booked to a
disqualification or a count-out and therefore does not *resolve* (§6.1's first requirement),
and once more for restarting a settled programme inside six months, which is this entry's
"continuing past the blow-off should be penalised".

What it is worth, measured on a `Big Match Epic` between two eighty-overness workers: having
a Nuclear feud at all is +0.19★ over none; settling it is a further +0.05★; ten matches that
settle nothing is −0.05★. **The rating movement is small and that is not the point** — it is
a quarter of what the feud itself is worth, and full distrust saturation costs about 0.03★
because `Credibility` feeds one consumer. What this change actually does is put a *clock* on
heat: a programme has to be maintained, an ending has to be paid for, and neither was true
before. Calling distrust "the teeth" of it overstates a ≤0.05★ nudge, which the first draft
of this entry did.

One thing A3's brief asked for and this does not do: a blow-off does not pay out as a large
one-time **business** result, because there is no business axis to pay into yet. §7 of doc
18 ("rate well vs draw well as two separate outputs") is the prerequisite, and it is not
built. What ships is the quality and story half.

#### A4. Match-count decay per pairing — **implemented**
**Reference:** [20](20-storylines-and-feuds.md) §9.1, [29](29-benchmarks-and-numbers.md) §10.1
`Feud.MatchCount` existed, was incremented in three places, displayed in four, and **read by
zero engine code** — its doc-comment claimed it fed fatigue decay and it did not. A promotion
could run the same main event fifty-two weeks running at full value.

Shipped as `Feud.FamiliarityFor`: 1st 100%, 2nd 90%, 3rd 85% (100% when the feud reads as a
blow-off), 4th 65%, 5th 57%, 6th 50%, 7th+ 45%, interpolated so a part-forgotten series lands
between rows. Freshness recovers — 60 days of grace, then one meeting forgotten per 120 days
idle — because doc 17 §4.2 says absence restores it and a rematch eight months later must not
be graded as the fourth.

It damps the **crowd ceiling and opening-bell energy** and the status transfer, and leaves
the technical and storytelling accumulators alone: two good hands still wrestle their fifth
match well, what has gone is the appetite. There is a test asserting those two scores are
bit-identical across a stale rematch. `ShowSimulator` reads familiarity from the feud book
rather than `plan.Feud`, so declining to attach a feud does not dodge the rule.

Measured across five consecutive weekly shows with an identical beat sheet, at `3da5c40`:
3.45★ → 3.21★ → 3.14★ → **2.73★** → 2.52★, with the winner's overness gain falling
+0.18 → 0.00. Rating falls 21% by the fourth while the *status* gain falls to nothing, which
is the right shape — the work still gets credited, the audience stops caring who wins.

> Both figure sets above are stamped with the commit they were measured at because they are
> **engine outputs, not constants**. The constants they came from — `Feud`'s curve, its 60/120
> day freshness window, `HeatEconomy`'s scales and `FinishWeight`'s weights — are unchanged as
> of `f38c999` and the curve itself is test-pinned by `MatchCountDecayTests`. The star
> *ratings* are not pinned anywhere, and `MatchEngine` has gained the phase grammar, match
> briefs and a dozen structures since they were taken, so treat the absolute numbers as a
> reading from that commit and the 21%-and-nothing shape as the claim.

Note the third-match bump keys off **feud intensity** rather than a stipulation
(`Feud.ReadsAsBlowOff` is `Intensity >= Hot`). That was the only signal available when A4
shipped, and it no longer is: `MatchPlan.Stipulation` now exists with five rungs and its own
validation (`StipulationRules`). So both `ReadsAsBlowOff` and its doc-comment — which still
reads "there are no stipulations in the game yet" — are **stale as of `f38c999`**, and wiring
the real signal in is a live follow-up rather than a thing to wait for. Heat is not a bad
stand-in, so this is a small improvement rather than a defect: a declared stipulation is a
promise to the audience that this is the end, which is exactly what the bump is pricing, and
reading one should outrank inferring it from intensity.

#### A5. Reaction *type*, not just magnitude — **implemented**
**Reference:** [16](16-crowd-psychology.md) §2
Built as `CrowdReaction` (pop / heat / tension / silence / go-away heat) plus `ReactionKind`.
What it covers:
- **Heat counts as engagement.** A booed heel is the fuel the face-in-peril structure runs
  on, and the engine now says so rather than reading boos as things going badly.
- **Tension and silence are separated.** The two quiet rooms that mean opposite things — an
  audience holding its breath and one that has stopped caring — were a single negative
  number. The denied tag reads as tension; an overworked heat reads as go-away heat.
- **Silence is the failure state**, per §2.1. The crowd component is scaled by `Investment`
  on an asymmetric curve — a dead room keeps 70.2% of it, a room present all night gains
  6.1%. An invested crowd is the baseline a match is supposed to earn; being ignored is what
  costs.
- **Booking, not just casting.** Repetition feeds investment continuously, so a beat the
  crowd has already seen three times costs their attention however over the wrestlers are.
- **Heat means alignment, not unpopularity.** A heel draws heat and a babyface draws pop at
  the same overness. Of §2's two important crossovers, one is live and one is not: **8 of 37
  shipped heels** read as cheered, and **0 of 35 babyfaces** read as booed. The babyface
  crossover needs `Disposition` below **0.270** and the lowest face on the roster is **0.285**
  (Maeve Torrance) — so it is reachable in the model by a margin of 0.015, and unreached by
  anybody actually shipped. Three faces sit within 0.035 of it.

  It is also the *wrong* route to it. What the model can express is an unpopular babyface,
  which is close to the defect this replaced; what §2 means is a babyface the crowd has
  **turned on despite a push**, and there is no signal for that yet. Overness-versus-appeal
  is the obvious candidate and does diverge — up to **0.18**, with **15 of 76** past the
  0.08 I wrongly gave as its ceiling — so the
  ingredient exists even though nothing reads it. Not measurable today; not unmeasurable in
  principle.

Measured on the **shipped 76-wrestler roster** (n = 34,200 — every non-feud-gated singles
structure × every ordered pair): investment moves the score in **96.7%** of matches, by up to
**4.04 points (0.202★)**. The median match comes out at ×0.9998, a dead room keeps 70.2% of
its crowd component and a room present all night gains 6.1%, and only 0.33% sit at the
maximum. The effect really is in the tails.

> These figures replace an earlier set (96.6%, 3.99 points, "exactly ×1.000") which was not
> reproducible: the corpus test seeded each cell with `HashCode.Combine`, which .NET randomises
> per process, so every run measured a different sample. The numbers above are from the fixed
> corpus and reproduce exactly. See the build log for the whole of it.

Still open from the original entry: `nuclear heat` as distinct from ordinary heat, and
reaction as an input to the *business* result rather than only the quality rating (C5).

#### A6. Persistent limb damage across beats
**Reference:** [18](18-match-craft.md) §2.2
Beats that target a body part should modify later beats, and failing to sell the damage
should cost storytelling score. This turns limb work from a beat type into actual ring
psychology and would make `Selling` a much more interesting attribute.

Still open. `MatchEngine` does have a `BodyPart` now, but it serves the **injury roll** —
which part got hurt — and `MatchEngineState` carries no limb damage between beats. Worth
noting that it is cheaper than it was: the phase grammar gives a match named stages to hang
persistent state on, and `MatchEngineState` already tracks isolations, hope spots and denied
tags across beats, so the machinery for "something that happened earlier changes this beat"
exists and limb work simply does not use it.

#### A7. Finisher credibility as a promotion-level variable
**Reference:** [18](18-match-craft.md) §4.2, §5.3
Every kicked-out finisher should slightly reduce the credibility of *all* finishers,
decaying over time. Reproduces finisher inflation and makes near-fall spam self-limiting
for the right reason.

#### A8. Interference credibility as a promotion-level variable
**Reference:** [04](04-booking-philosophy.md) §5.1
Every run-in and non-finish should reduce the credibility of near-falls company-wide for a
decay period. The cost of interference is invisible per-instance and enormous in aggregate,
which is exactly the shape of a good game mechanic.

### Tier B — high value, larger work

#### B1. Championships — **implemented**
**Reference:** [21](21-championships.md)
Built as `Title` / `TitleReign` / `TitleRegistry` plus `TitleEconomy`. What it covers:
- `Title` with lineage, prestige, and a current holder
- Prestige rising with reign length and credible defences; falling with frequent changes,
  non-title losses, absence, and dilution
- **Total prestige roughly fixed and divided among titles**, so adding a belt weakens the
  others
- Title matches inheriting a stakes bonus from prestige

#### B2. Booked position vs perceived position
**Reference:** [12](12-pushes-and-positioning.md) §1
The player books a position; the audience assigns one. The gap between them is where the
game lives — and it produces the coronation/rejection dynamic that is the single most
recognisable modern wrestling story.

Still open, but the **pattern** now exists one layer down and is worth copying rather than
reinventing. `MatchExpectation` does exactly this shape for a single match: the promise is
derived from who is in it and what is at stake, the delivery is the beat sheet, and the
distance between them is the interesting quantity — with `CanCarryIt` deciding whether going
against the promise lands, read off psychology and ring IQ rather than off the plan.

B2 is that mechanism applied to a *career* instead of a match. What is missing is only the
booked half: `Wrestler.CardPosition` derives from `EffectiveOverness`, so the model holds the
audience's verdict and nothing holds the player's intent to compare it against.

#### B3. Audience segments
**Reference:** [23](23-fanbase-segments.md)
Casual / regular / hardcore / family, each with separate satisfaction, each driving a
different revenue line, each wanting incompatible things. Turns booking from optimisation
into strategy. Probably the highest-ceiling addition in the whole reference.

Still open, and **half-blocked**. The *taxonomy* exists — `FanGroup` has six members and
`Gimmick.AppealRatings` holds a per-group appeal that the crowd model already reads, which is
the §1 row about disposition coming from appeal rather than alignment. What is missing is
per-segment **satisfaction** carried across shows, and the revenue lines that are the point of
splitting them: "each driving a different revenue line" cannot be built before C5.

#### B4. The promotion layer
**Reference:** [02](02-promotion-anatomy.md), [06](06-schedule-and-cadence.md), [09](09-revenue-and-costs.md)
The sim currently books shows in a vacuum. A promotion entity with tier, calendar, roster,
finances, media rights, and markets converts a match simulator into a management game.
Start with: tier, revenue lines, a TV contract with an expiry, and a show calendar.

**Two of the four starting items shipped.** `Promotion` carries a `PromotionTier`, and the
calendar is real — `ShowDefinition` with recurrence, a rolling materialised schedule, and
`Career.AdvanceOneDay` as the clock that every decay in the game is charged to.

Not shipped: **revenue lines** and **the TV contract**. There is no money in the model at all.
The closest thing is `Promotion.TypicalAttendanceFor(ShowType)`, which returns a plausible
number for flavour and is read by nothing that could be called a business. So the half of this
entry that "converts a match simulator into a management game" is the half still missing, and
it is C5's dependency again.

#### B5. Markets — **partial**
**Reference:** [07](07-live-events-and-touring.md)
Persistent markets with heat that depletes when run and recovers over time. Produces the
touring loop as an emergent strategy and gives `Show.Location` real meaning.

**The touring loop shipped; markets did not.** `HouseShowLoop` books a cast and a number of
towns for an untelevised date, `Wrestler.TouringUntil` holds it as a standing instruction
rather than a week-by-week booking, and `LoopSimulator` runs it — so the loop exists and costs
the roster real fatigue. What does not exist is the thing this entry is actually about: a town
is an `int` count, not a place. There are no named markets, no per-market heat, and `Venue` is
still a string that nothing reads.

So the loop is here but it is **not emergent** — the player books it directly instead of being
drawn into it by a market that has gone cold. That inversion matters: this entry's claim was
that markets *produce* touring as a strategy, and shipping touring first means the strategy
arrived without the thing that was supposed to motivate it.

#### B6. Age and the aging curve
**Reference:** [15](15-injuries-and-attrition.md) §4
`Wrestler` has no age. Adding one plus per-stat curves — athleticism declining from ~28,
psychology rising to ~40, connection rising with tenure — reproduces the real career arc
with the stats the model already has.

Still true, and now the **single highest-leverage missing field in the model**, because other
systems have been built up to its edge and stopped there. `InjuryRisk` implements every §3.1
multiplier except age-over-35 (1.5–2×, "rising sharply"); `RingCondition`'s recovery rates
have no age term; and `Stipulation` leaves out Career, Mask and Loser Leaves Town on the
stated grounds that this codebase has no way to write somebody off — which is an age-and-
retirement gap, not a stipulation gap.

It is also a **data** task, not only a code one: all 76 entries in
`WrestlingSim.Core/JSON/Wrestlers.json` carry `Physical`, `Mental`, `RingSkills`, `Style` and
`Overness`, and nothing temporal at all. A `DebutedOn` is arguably worth more than a `BornOn`,
since §4.1's point is that drawing power tracks accumulated audience relationship rather than
athletic peak — but the injury multiplier wants calendar age, so probably both.

#### B7. Injuries — **implemented**, and exceeded
**Reference:** [15](15-injuries-and-attrition.md) §3
Probabilistic, driven by volume, style, age, injury history, opponent safety, and
conditioning. `Toughness` and `Stamina` already exist as inputs. Injury history as a
permanent compounding attribute is the important detail.

Shipped as `InjuryRisk` (rolled **per beat**, so a long match at Extreme intensity carries
real exposure rather than one roll at the bell), `Models.Person.Injury`, and
`Wrestler.Injury`/`.InjuryHistory`. §3.1's multipliers are present as named functions —
`StyleRisk`, `IntensityRisk`, `FatigueRisk`, `SizeRisk`, `HistoryRisk`, `RingRisk` — and
`HistoryRisk` is keyed to the **body part**, which is §3.1's strongest-single-predictor point
made mechanical rather than gestured at.

Two of the brief's inputs were solved sideways and one is still missing:

- **Opponent safety** has no attribute. `OpponentRisk` derives it from `Psychology` and
  `RingIQ`, which were already authored for all 76 wrestlers. That is the better answer — a
  wrestler the player already reads as sloppy becoming mechanically dangerous is a consequence
  rather than a new number to learn — and it quietly closes a line in §3's attribute table.
- **Ring quality**, which this entry never asked for, is in as `RingRisk(PromotionTier)`.
  An indie ring really is a multiplier, per §3.1.
- **Age** is absent, because there is still no age field. See B6.

**And it went past the brief.** This entry asked only for injuries; what shipped is the whole
attrition picture, including the one thing nothing in the reference's sim implications
proposed:

> `Wrestler.Fatigue` and `Wrestler.Sharpness` are **two meters that pull against each other**
> — working a match raises both, resting lowers both. So rest fixes one thing and breaks
> another, and there is no correct amount of time off, only a trade about a particular
> wrestler. Rest the main eventer two months and they come back fresh and rusty; work them
> every week and they are sharp and cooked.
>
> That is a better mechanic than a single recovering health number, and it is not in doc 15.
> It is doc 15 §7's return problem and §4.2's longevity problem read together and solved once.
> `RingCondition` holds both, and keeps exertion separate from danger on the stated grounds
> that a twelve-minute sprint can cost more than a twenty-six-minute mat classic while a
> ladder match is dangerous without being especially tiring.

What is still open from §15 and would be the natural next slice: the **narrative** half.
An injury does not yet interrupt a feud (`Feud.RecordUnresolved` would charge the player
distrust for a programme a dice roll ended), injury absence decays the same as being left off
television when the audience in fact knows why someone is gone, and there is no return pop —
doc 15 §7's loudest moment, and the only thing that makes injury an upside rather than a tax.

### Tier C — distinctive, ambitious

#### C1. Merit vs Inertia
**Reference:** [28](28-cultural-inertia.md) §Sim implications
Split promotion drawing power into fast-moving `Merit` and slow-moving `Inertia`. Nostalgia
acts give an immediate boost while consuming `Inertia` and blocking a star-creation slot.
A competitor's existence lowers the `Inertia` floor. **Show the player good revenue numbers
while the warning signs accumulate.** This would be genuinely novel among wrestling games.

#### C2. Brands and split integrity — **implemented**
**Reference:** [22](22-brand-splits.md)
Brands with exclusivity, and a `SplitIntegrity` value that every crossover reduces. The
player gets an immediate ratings boost for putting their star on the other show, and pays
for it structurally. The entire real-world dynamic in one loop.

Shipped as `Brand`, `BrandSplit` and `BrandIntegrity`. `Integrity` starts at 100 and falls by
a cost per crossover that scales with how invested the audience is in the performer being
moved, and an exclusive show earns a stakes bonus that **disappears as integrity does** — so
the loop closes: the bonus you are borrowing against is the belief you are spending.

`ShowSimulator` settles the cost *after* scoring the card, deliberately, so the player sees
the rating the crossover bought before they see what it cost. That is the order in which the
decision looks correct, which is the point.

#### C3. Regional rule sets
**Reference:** [25](25-international-models.md)
Japanese mode (tours, tournaments, units, dojo, excursions), lucha mode (trios, masks,
apuestas), British mode (rounds and points). Each is a genuinely different rule set, not a
reskin.

Still open as a *mode*, but some of the ingredients have arrived through the front door rather
than this entry: **trios** is a real format with its own structures (`Lucha Trios`, `Six-Man
War`), and **tours** exist as `HouseShowLoop` runs of towns. Neither carries the rule set
around it — no units or excursions, no masks or apuestas, no rounds — and a mode is the part
that would make them mean something different rather than merely be available.

#### C4. Eras
**Reference:** [26](26-eras-and-history.md) §4
A parameter set per era — revenue mix, schedule, contract type, content rating, match
length, medical policy, title reign norms — turns history into configuration rather than
special-case code.

#### C5. Rate well vs draw well as separate outputs
**Reference:** [18](18-match-craft.md) §7
The sim reports a star rating. It should also report a **business result**, and the two
should be able to diverge sharply. That divergence *is* professional wrestling.

Still open, and it is now the **most-referenced missing piece on this board** — A3 cannot pay
a blow-off out as a business result without it, B3's segments have no revenue line to drive,
B4 has no P&L to report into, and C1's whole point is showing the player good revenue numbers
while the warning signs accumulate. `MatchEngineResult` carries `TechnicalScore`,
`StorytellingScore`, crowd energy and `FinishQuality`; every one of them is a quality axis.
`Grade` bands that quality for the UI and is not a second output.

Four entries are each individually blocked on this, which probably makes it the wrong thing to
have left in Tier C.

---

## 3. Attribute gaps on `Wrestler`

Attributes the reference says matter. Status as of `ece8cd9`.

| Attribute | Reference | Status |
|---|---|---|
| **Overness / Momentum** | [17](17-heat-and-getting-over.md) §1.1 | **Present** — `Overness`, `Momentum`; see A1 |
| **Injury history** | [15](15-injuries-and-attrition.md) §3.1 | **Present** — `InjuryHistory`, read per body part by `InjuryRisk.HistoryRisk` |
| **Safety** | [03](03-roles-and-competence.md) §5.4 | **Derived, not stored** — `InjuryRisk.OpponentRisk` reads `Psychology` + `RingIQ`. Enough for injury risk; not enough for the social/reputation half of §5.4 |
| **Durability** | [15](15-injuries-and-attrition.md) | **Superseded** — the entry wanted schedule tolerance as distinct from bump tolerance; `Fatigue`/`Sharpness` with per-wrestler recovery express it as a pair of meters instead of a stat |
| **Age** | [15](15-injuries-and-attrition.md) §4 | **Missing.** Gates the entire career arc, and now blocks B6, the age term in `InjuryRisk`, and the write-somebody-off stipulations |
| **Reliability** | [03](03-roles-and-competence.md) §5.4 | Missing. Ends more careers than anything else |
| **Adaptability** | [03](03-roles-and-competence.md) §5.1 | Missing. Mitigates style clash; the Flair attribute |
| **Promo (scripted) / Promo (improv)** | [19](19-characters-and-promos.md) §3.5 | Missing as a split. `SegmentSimulator` has an improv *risk* keyed off `Psychology`, which is the consequence without the two skills behind it |
| **Locker-room standing** | [14](14-locker-room.md) §2 | Missing. Invisible to fans, decisive to careers |
| **Merch draw** | [09](09-revenue-and-costs.md) §3.4 | Missing, and blocked behind C5 — there is no revenue for it to be a line of |

Two attributes now on `Wrestler` that this table never asked for, both from the attrition
work: **`Fatigue`** and **`Sharpness`** (0–100, moving together on work and together on rest),
plus **`TouringUntil`** as a standing road assignment. See B7.

And on `Gimmick`:

| Attribute | Reference | Why |
|---|---|---|
| **Motivation** | [19](19-characters-and-promos.md) §2.3 | A gimmick without one is the identified failure mode |
| **Participation hook** | [16](16-crowd-psychology.md) §3.8 | Crowd bonus scaling with audience tenure |
| **Fit** (gimmick↔performer compatibility) | [19](19-characters-and-promos.md) §2.2 | The "80% the performer" rule; gates how much of the gimmick's potential is realised |
| **Comedy flag** | [19](19-characters-and-promos.md) §5.3 | The one-way door |

---

## 4. Build order

The original four-phase order is kept below for the record, struck through where it has been
overtaken. **It was not followed, and mostly should not have been.** Phase 1 and 2 were
ordered by how contained the work was; what actually got built was ordered by what made the
match itself interesting, which produced the formats, the phase grammar and the attrition
system well ahead of the "promotion" phase they were supposed to wait for. That was the right
call and this board should stop implying otherwise.

### What is actually left

**One field, which several things are queued behind:**
- **Age** (B6, §3) — blocks the aging curve, the age term in `InjuryRisk`, and any stipulation
  that writes somebody off. A data pass over 76 roster entries plus the curves.

**One output, which four entries are each separately blocked on:**
- **C5 rate well vs draw well.** A3's blow-off payout, B3's revenue lines, B4's P&L and C1's
  whole premise all need a business result to exist. Leaving this in Tier C has turned out to
  be the single most consequential ordering mistake on this board.

**Then, roughly in order of value per unit of work:**
1. **The narrative half of injuries** — `Feud.Interrupt`, slower injury-absence decay, the
   return pop. Small, and it converts attrition from a tax into a story. See B7.
2. **A6 limb damage** — the one Tier A entry that is both open and cheap now that the phase
   grammar gives beats somewhere to put persistent state.
3. **Real markets** (B5) — a town is currently an `int`. Named markets with depleting heat
   would make the touring loop that already exists *mean* something.
4. **A7/A8 credibility** — finisher and interference inflation, both still unbuilt.
5. **B2 perceived vs booked position** — copy `MatchExpectation`'s shape up a layer.
6. **Wire `Feud.ReadsAsBlowOff` to `MatchPlan.Stipulation`** — a small correction, now that
   stipulations exist. See A4.

### The original order, for the record

**Phase 1 — make results matter**
1. ~~A1 Overness/Momentum split~~ — done
2. ~~A2 Heat transfer on results~~ — done
3. ~~A3 Feud decay + terminal blow-off~~ — done
4. ~~A4 Match-count decay per pairing~~ — done

**Phase 2 — deepen the match**
5. ~~A5 Reaction type vector~~ — done
6. A6 Persistent limb damage — open
7. A7/A8 Finisher and interference credibility — open

**Phase 3 — the promotion**
8. B4 Promotion layer with a calendar and a P&L — calendar done, no P&L
9. ~~B1 Championships~~ — done
10. B5 Markets — touring done, markets not
11. B2 Booked vs perceived position — open

**Phase 4 — the world**
12. B6/B7 Age and injuries — ~~injuries done~~, age not
13. B3 Audience segments — open
14. C1 Merit vs Inertia — open
15. ~~C2 Brands~~ — done; C3 regional rule sets, C4 eras, C5 dual outputs — open

Not in any phase, and built anyway: tag and multi-man, stipulations, battle royals, the
elimination formats, house-show loops, match briefs and the phase grammar. See §2.2.

---

## 5. Design principles drawn from the reference

Things worth holding onto whatever gets built:

1. **The audience decides, not the player.** The most authentic wrestling mechanic is the
   crowd adopting someone you weren't pushing, and rejecting someone you were.
2. **Scarcity is the engine.** Every good thing in wrestling — a stipulation, a turn, a
   title change, a return — is valuable because it is rare. Any system that lets the player
   do a thing unlimited times has modelled it wrong.
3. **Nothing stays hot.** Decay should be pervasive: gimmicks, pairings, stipulations,
   factions, moves. Managing decay is the job.
4. **Quality and drawing are different.** Report both; let them diverge.
5. **Silence is the failure state, not booing.**
6. **The cost of a shortcut should be invisible per-use and large in aggregate.**
   Interference, non-finishes, nostalgia acts, crossovers — all the same shape.
7. **Imperfect information about people.** The player should be able to be wrong about a
   performer's connection until a crowd tells them.
8. **The player should be able to run a healthy business into the ground with correct-looking
   decisions.** That is what actually happens.
