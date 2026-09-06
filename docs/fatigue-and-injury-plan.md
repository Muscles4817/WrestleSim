# Fatigue and injury — implementation plan

*Written against the codebase at `e56cb1e`. Companion to
[15-injuries-and-attrition.md](wrestling-reference/15-injuries-and-attrition.md), whose §3
and §3.1 coefficients this plan is calibrated to, and to
[31-sim-mapping.md](wrestling-reference/31-sim-mapping.md), which does not list this system
at all.*

---

## 0. The premise

There is no fatigue system and no injury system. What exists is a set of near-misses that
look like one from a distance:

- **`SegmentResult.Injured`** is set by a roll in `SegmentSimulator`, resisted by
  `Mental.Toughness`, stamped as a `FeudHistoryTag.InjuryAngle`, printed as a line of
  commentary, docked 1.0 overness — and then discarded. **Nobody misses a single day.** The
  wrestler is bookable on the same card. The word "injured" appears six times in the whole
  of `Core` and `Web`.
- **`Physical.Stamina`** feeds `PerformerProfile.Conditioning`, which feeds
  `MatchEngine.FadeFactor` and a `staminaPenalty` on long matches. Both are *within* a single
  match and both reset at the bell. Wrestling twenty minutes on Tuesday costs nothing on
  Thursday.
- **The word "fatigue" appears thirteen times across five files in `Core` and means three
  different things:** crowd fatigue from repeated item types (`ShowSimulator`, `ICardItem`,
  `ShowResult`), late-match fade (`MatchEngine`), and per-pairing familiarity (`Draft`). None
  of them is a wrestler being tired.
- **`Wrestler` has no age.** Not a birthdate, not a debut year, not a number. The aging curve
  in doc 15 §4 — the single most useful thing in that chapter — is not merely unbuilt, it is
  currently unbuildable, and the shipped 76-wrestler roster carries no data to build it from.

So the promotion in front of the player is one where a roster of 76 people can each work
every show forever at full output. Doc 15 §1's fundamental fact — that this is a job which
damages the people doing it, and that managing that damage *is* the job — is absent from the
model entirely.

Two things have to be true for this to be worth building:

1. **It must take decisions away from the player in a way they can see coming.** A system
   that removes your main eventer on a random roll two days before the pay-per-view is not a
   feature, it is a punishment. What makes attrition interesting is that the player *chose*
   the schedule, *chose* the spotfest, and *chose* to run the veteran on both brands. The
   information has to be legible before the cost lands.
2. **It must make style and volume into trades.** Doc 15's closing note is the whole design
   goal: style-adjusted longevity "turns *book the spotfest* into a real trade rather than a
   free rating". If the Spotfest and the Technical Showcase cost the same, this system is
   decoration.

---

## 1. The core decision: three stocks, not one health bar

The obvious model is a single 0–100 `Health` that goes down with work and up with rest. It is
wrong, and doc 15 says why in one line: §3.1 names **existing injury history** as "the
strongest single predictor" of future injury. A number that recovers cannot be a permanent
predictor. A wrestler who blew a knee in 2019 and is fully recovered today is *not* the same
as one who never did, and one number cannot hold both facts.

So: three stocks with three different clock speeds.

| Stock | Range | Clock | What it is |
|---|---|---|---|
| **`Condition`** | 0–100 | days | Short-term freshness. Falls with work, recovers with rest. This is fatigue. |
| **`Wear`** | 0–100 | years | Accumulated career mileage. Recovers barely, and never to zero. This is why veterans are fragile. |
| **`Injuries`** | a list | discrete | Named injuries with an area, a severity and a return date. Each leaves permanent residue in `Wear` and in a per-area fragility map. |

`Condition` is the dial the player pulls on week to week. `Wear` is the one they only notice
after a year of pulling it. `Injuries` are the events.

### Ring readiness is derived, never stored

```csharp
// docs/wrestling-reference/15-injuries-and-attrition.md §3, §6.1
public double RingReadiness =>
    ActiveInjuries.Any(i => i.Severity >= InjurySeverity.Minor)
        ? 0.0
        : Math.Clamp(
              (Condition / 100.0)
            * (1.0 - Wear / 100.0 * WearDrag)          // WearDrag ≈ 0.35
            * ActiveInjuries.Aggregate(1.0, (r, i) => r * i.ReadinessMultiplier),
            0.0, 1.0);
```

One number, 0–1, computed on read. It is what the roster screen shows, what the booking
screen gates on, and what the engine multiplies by. Storing it would mean three sources of
truth for the same fact and a save format that can contradict itself, which is the bug class
`EffectiveOverness` was written to avoid.

### Why `Wear` is not just age

They are different and both are needed. Age is calendar time; wear is what you did with it.
A 38-year-old technical worker who has never had a schedule (doc 15 §4.2 — "technical/mat
styles age extremely well") should be more available than a 31-year-old who has worked 200
dates a year in a hardcore style since 22. Age gates the *aging curve* (§2 below defers it);
wear gates fragility and recovery, and can ship without an age field.

---

## 2. Accrual: where fatigue comes from

### Per match, from the beats

The cost of a match is not a constant. It is the sum of what was actually booked, which is
already sitting in `MatchBeat`:

```
cost = Σ over beats ( IntensityCost(beat.Intensity) × DurationMinutes(beat.Duration) )
     × StyleMultiplier(wrestler.Style)
     × StructureMultiplier(plan.Structure)
```

`BeatIntensity` already runs Low / Medium / High / Extreme and `BeatDuration` already yields
`DurationMinutes`. Nothing new is needed on the beat model. `StructureMultiplier` is a new
`init` property on `MatchStructure` alongside `SideSize` and `RequiresFeud` — a `PhysicalCost`
around 0.8 for **Technical Showcase**, 1.0 for **TV Formula**, 1.4 for **Spotfest**, 1.5 for
**Grudge Brawl**. That single property is what makes doc 15 §4.2 mechanical.

Segments accrue too, at roughly a tenth of a match, because a beatdown is still bumps.

### Per appearance, from volume

Doc 15 §3.1: match volume is "roughly linear — 200 dates is ~2× the risk of 100". So there is
a flat per-appearance cost on top of the per-beat one, charged in `ShowSimulator` at the same
place `LastAppearance` is already stamped. This is deliberately the crudest term in the
system, because it is the one the player controls most directly: the show definitions they
picked in the opening wizard are now a health budget as well as a calendar.

### Recovery, on the clock

`Career.AdvanceOneDay` already charges four daily decays — momentum, title drift, team
chemistry, feud heat. Recovery is the fifth, and belongs in exactly the same loop for exactly
the same reason those are there: *the thing you are not maintaining is quietly getting worse*,
and its mirror, the thing you are resting is quietly getting better.

```
Condition += DailyRecovery(wrestler)      // base ≈ 4.5/day
DailyRecovery = base
              × (0.70 + 0.60 × Conditioning)     // Stamina pays off out of the ring too
              × (1.0 - Wear / 100.0 × 0.40)      // mileage slows healing
              × AgeRecoveryFactor(wrestler)      // 1.0 until an age field exists
```

Full-to-empty in about three weeks of hard work, empty-to-full in about three weeks of rest,
which puts the fatigue cycle on roughly the same clock as the momentum half-life (0.967/day)
and the feud heat clock (0.955/day after 14 days). That is not a coincidence and should not be
one: a player already reads this game in three-week arcs.

---

## 3. Injury: the roll, and the thing it must not be confused with

### Two sources

**Acute** — during a match, on a beat. Rolled per high-cost beat, not once per match, so a
twenty-minute Extreme-intensity brawl genuinely carries more exposure than a four-minute TV
match. This is a bad landing.

**Attrition** — on the daily tick, weighted heavily by low `Condition` and high `Wear`. This
is the body giving out, and it is what makes fatigue *matter* rather than merely reading as a
rating penalty. Doc 15 §3.1 lists "fatigue / travel" as "significant, poorly quantified"; this
is where it lives.

### Multipliers, straight from §3.1

| Factor | Multiplier | Source available today? |
|---|---|---|
| Match volume | linear in appearances | yes — `ShowSimulator` |
| Style (high-flying, hardcore, high-impact) | 1.5–2.5× | yes — `WrestlingStyle` |
| Age over 35 | 1.5–2× | **no — needs an age field** |
| Prior injury, same area | ~2× | yes, once the injury list exists |
| Size | mild, rising | yes — `Physical.Size` |
| Poor conditioning | 1.3–1.8× | yes — `Physical.Stamina` |
| Unsafe opponent | 1.5–2.5× | **no — see §6** |
| Ring quality | 1.0–1.5× | no, and out of scope |

### Severity bands

| Band | Out for | Effect |
|---|---|---|
| **Niggle** | 0 days | Bookable, but readiness-docked and re-injury risk up sharply. The decision to work hurt lives here. |
| **Minor** | 1–6 weeks | Out. Doc 15 §2.1's common band. |
| **Significant** | 3–9 months | Out. Doc 15 §3's "10–18% per year". |
| **Severe** | 9–18 months | Out, and a large permanent `Wear` addition. |
| **Career-threatening** | indefinite | Out. Return is not guaranteed. Rare. |

### Calibration is the acceptance test, not a tuning pass

Doc 15 §3 gives three numbers that are directly checkable, and the test for this feature is
literally those three:

- **35–55%** of full-timers miss time to injury in a year
- **10–18%** suffer a significant (3+ month) injury in a year
- **10–20%** of the roster is injured at any given moment

A test that simulates a year on the shipped 76-wrestler roster against a realistic weekly-TV
schedule and asserts those three fractions is the gate this ships behind. Any set of constants
that passes it is defensible; any that does not is not, however sensible the individual
coefficients look. The §3 header says these are "estimates synthesised from injury reporting
and roster-absence patterns, not published epidemiology" — they are still the best target
available, and having the model agree with them is worth more than having each multiplier
individually justified.

### The angle and the injury are not the same thing

`SegmentResult.Injured` today is a **worked** injury — an angle, doc 15 §6.3, a way of writing
someone off screen. A real injury is a different event with different consequences and they
must not share a field. The plan:

- `SegmentResult.Injured` → renamed `WrittenOff` (or `InjuryAngle`), keeps its
  `FeudHistoryTag`, keeps costing overness, costs no days.
- Real injuries are `Wrestler.Injuries` entries produced by the two rolls above.
- The interesting case is deliberate: booking an injury angle *for* someone who is genuinely
  hurt is the real-life move (§6.3), and the system should recognise it — the angle covers the
  absence, so the feud does not read as abandoned.

---

## 4. Consequences: performance

### Readiness scales the work, not the reaction

A4 established the precedent and it applies exactly here: familiarity damps the **crowd**
ceiling and leaves the technical and storytelling accumulators alone, because two good hands
still wrestle their fifth match well. Fatigue is the mirror image. A hurt worker still gets
their reaction — the crowd does not know and does not care — but the work degrades. So
readiness multiplies the **technical** accumulator and leaves the crowd component alone, and
there should be a test asserting the crowd score is bit-identical across a fresh and an
exhausted performer.

### It moves the fade, not just the score

`FadeFactor`'s per-beat retention is `Clamp(0.855 + 0.115 × conditioning, 0.82, 0.99)`. Low
readiness should shift that band down, so a tired wrestler does not merely score lower — they
fall apart *sooner*, and a long match booked for someone who has no business in one becomes
visibly the wrong call in the play-by-play rather than a number at the end.

### Working hurt is the trade

A performer with a Niggle, or one the player books at low readiness anyway, gets:

- a **storytelling bonus** — selling an injury is compelling and doc 15 §6.3 is full of it
- a **technical penalty** — larger than the readiness dock alone
- a **sharply raised re-injury multiplier**, weighted toward the same body area

That is the Foley trade, and it should be genuinely tempting. If nobody ever books it, the
numbers are wrong.

### Absence, and the one place this collides with existing code

`HeatEconomy.ApplyDailyDecay` currently strips 0.06 overness/day after a 21-day grace, on the
theory that someone off screen is being forgotten. An injured wrestler would fall straight
into that, which is wrong: **the audience knows why you were gone.** Injury absence must decay
noticeably slower than being left off television — different cause, different audience
reading. And doc 15 §7 gives the other half, which the engine has no version of at all: the
**return pop**. A well-built return is worth more than the absence cost, which is exactly why
promotions make an event of it, and it is a clean mechanic to add on the back of `Injuries`
having a known end date.

---

## 5. Consequences: feuds

### Do not charge the player for a dice roll

`Feud.RecordUnresolved` accrues `Distrust` past the third match, and `Feud.ApplyDailyDecay`
strips heat after 14 days. A feud whose participant goes out for four months would take both
barrels — the heat decay is *correct* (nobody is telling the story, so the story dies) but the
distrust is not. Distrust models an audience learning that this promotion does not finish what
it starts. An audience that watched someone get stretchered out has learned nothing of the
sort.

So: `Feud.Interrupt(reason, until)` — freezes distrust accrual and the `PatienceMatches`
counter while a participant is unavailable, and leaves heat decay running.

### Injury as an exit, and as a return

Two things fall out for free:

- **The write-off.** `FeudHistoryTag.InjuryAngle` already exists and the angle path already
  produces it. A real injury covered by an angle should close a feud without the distrust
  charge, which is the honest version of what promotions actually do.
- **The comeback blow-off.** Returning to settle the feud you were taken out of is doc 15 §7's
  loudest moment. `Feud.PayoffFor` already prices blow-offs by intensity (1.45 Nuclear down to
  0.72); a feud carrying an `Interrupt` whose blow-off is booked inside a window of the
  participant's return earns a further multiplier. This is the creative *upside* of injury
  that doc 15's sim implications call out, and without it the whole system is only a tax.

---

## 6. Per-wrestler variation

Three tiers, honestly labelled, because one of them cannot be built yet.

### Tier 1 — buildable today, from stats that already exist

| Signal | Existing field | Role |
|---|---|---|
| Toughness | `Mental.Toughness` | Already resists the segment injury roll (`5.0 × (1 − toughness/150)`). Extend to the match roll and to severity. |
| Conditioning | `Physical.Stamina` → `PerformerProfile.Conditioning` | Fatigue accrual rate and recovery rate. |
| Style | `WrestlingStyle` | The §3.1 style multiplier and the §4.2 longevity curve. |
| Size | `Physical.Size` | Mild risk multiplier; landing mechanics. |
| History | new `Injuries` list | The §3.1 strongest predictor, per body area. |

That set alone produces genuinely different wrestlers: a large Powerhouse with high Toughness
and low Stamina is durable but gasses; a small HighFlyer with the inverse profile works all
night and breaks.

### Tier 2 — needs one new field, and a data pass

**Age.** Doc 15 §4 is the best material in the chapter and none of it is reachable.
`Wrestler` needs a `BornOn` (or a `DebutedOn`, which is arguably more useful — tenure drives
the audience-relationship half of §4.1) and **all 76 entries in
`WrestlingSim.Core/JSON/Wrestlers.json` need a value**, which makes this a data task as much
as a code one — that file today carries `Physical`, `Mental`, `RingSkills`, `Style` and
`Overness`, and nothing temporal at all. Once it exists:

- the age-over-35 injury multiplier (1.5–2×, "rising sharply")
- the recovery slowdown with age
- and, separately and later, the §4 aging curve itself, where athleticism declines from ~28
  while psychology and character rise. **That is not this plan** — see §9 — but it needs the
  same field, so add the field here and let the curve land on top of it.

**Safety.** §3.1 calls working with an unsafe opponent "large and real" and nothing models it.
Two options: a new `Mental.Safety` stat requiring 76 more hand-authored values (`RingSkills`
is six style ratings and is the wrong home for it), or a derived read of `Mental.Psychology`
and `Mental.RingIQ`, which are already authored and already mean something close to "knows
what they are doing in there". **Derive it.** It is
cheaper, it is defensible, and a wrestler the player already reads as sloppy becoming
mechanically dangerous is a satisfying consequence rather than a new number to learn.

### Tier 3 — needs personalities, which do not exist

This is the part that is only a *shape*.

How a performer behaves when hurt is a personality question, not a physical one, and the
game has no personality model. Three axes it will eventually want, kept separate because they
genuinely dissociate in real people:

- **Stubbornness** — will work through an injury that should sit them down
- **Honesty** — reports the injury, versus hides it until it is worse
- **Self-preservation** — protects themself in the ring, takes fewer risks, lasts longer

They are not the same person. A stubborn honest worker tells the doctor and then argues; a
stubborn dishonest one just does not mention it. Only the second one is a nasty surprise for
the player, and that difference is worth modelling when there is a model to hang it on.

**The seam to leave now.** Build every consumer against a single function:

```csharp
// docs/wrestling-reference/15-injuries-and-attrition.md §6 — placeholder.
// Returns 1.0 for everyone until personalities exist; then reads Stubbornness,
// Honesty and Self-preservation. Every caller is already written against this shape.
internal static double RiskAppetite(Wrestler w) => 1.0;
```

Ship it returning a constant. When personalities land, one function body changes and the
whole system gains per-wrestler behaviour without a refactor. Do **not** approximate
personality with `Toughness` in the meantime — Toughness already means "resists the injury",
and quietly making it also mean "chooses to work hurt" is exactly the `Momentum`/`Advantage`
collision A1 had to unpick.

---

## 7. Persistence and UI

**Save format v4.** `WrestlerStateDto` gains `Condition`, `Wear` and `Injuries`. Every one is
optional with a fresh default, so a v3 save opens with a fully-fit roster and no migration
step — the same fallback discipline `ResolvedOverness` uses for pre-split saves. Injuries
serialise as area / severity / start / return, dates ISO like everything else, and rebind by
`Wrestler.Id`.

**Roster screen.** A readiness column, and a filter for "available". This is the screen the
player will actually make decisions on, so readiness has to be visible without a click.

**Booking screen.** An injured wrestler is not offered. A low-readiness one is offered with a
warning naming the number, because §0's first requirement is that the cost is legible before
it lands, not after.

**Dashboard.** A medical panel: who is out, what with, when they are back. Doc 15 §3 says
10–20% of a Tier 0 roster is hurt at any moment, so on a 76-person roster this is a list of
eight to fifteen names — big enough to need its own panel, small enough to read at a glance.

**Show results.** A new injury is a headline event and belongs alongside title changes in the
results view, not buried in a note.

---

## 8. Phasing

Each phase is shippable on its own and leaves the game in a coherent state.

**Phase 1 — the stocks.** `Condition`, `Wear`, accrual in `ShowSimulator`, recovery in
`Career.AdvanceOneDay`, `RingReadiness`, save v4, roster column. No injuries yet. The player
can already run someone into the ground and see it happen, and *nothing is taken away from
them*, which makes this the safest possible first slice.

**Phase 2 — performance consequences.** Readiness into the technical accumulator and the fade
band. `MatchStructure.PhysicalCost`. This is where the spotfest trade becomes real. Still no
injuries.

**Phase 3 — injuries.** Both rolls, the severity bands, the injury list, per-area history, the
`Injured`/`WrittenOff` split, the calibration test against doc 15 §3, and the booking gate.
This is the phase that can ruin the game if the numbers are wrong, which is why it is third
and behind an explicit acceptance test.

**Phase 4 — narrative.** `Feud.Interrupt`, the slower injury-absence decay, the return pop,
the comeback blow-off multiplier, the medical panel. This is the phase that makes injury
*interesting* rather than merely expensive, and it is genuinely optional in a way the first
three are not — which is precisely why it must not be dropped.

**Deferred to their own work, with the field added here:** `BornOn` and the doc 15 §4 aging
curve.

---

## 9. Risks

**This system's entire job is to remove options.** Every other system built so far gives the
player something — more structures, more titles, more brands. This one takes the roster away
in pieces. That is correct and it is what makes the schedule a decision, but it means the
calibration *is* the feature in a way it was not for A1 through A5. Ship it too harsh and the
game is a hospital ward.

**The death spiral.** Tired → worse matches → lower ratings → injured → absent → cold →
lower overness → a weaker card → more load on the people left. Every one of those arrows is
individually correct and together they are a trap with no floor. The system needs at least one
restoring force, and the honest one is the same one real promotions use: **the roster is
larger than the card**. If the player is spiralling it is because they are running eight
people, and the game should say so plainly rather than let them find out over six months.

**Doc 15's numbers are estimates.** §3's own header says so. They remain the best target
available and the acceptance test should use them, but a note in the test explaining what the
numbers are and are not will save an argument later.

**Randomness where the player expects agency.** An injury roll that removes the world champion
the week before the biggest show of the year is realistic and will feel like the game cheated.
Mitigations, in order of preference: make the risk visible in advance (readiness on the
booking screen), weight the attrition roll toward people the player has actually overworked so
it reads as consequence rather than dice, and consider a difficulty setting rather than
pretending one calibration suits everyone.

---

## 10. What this is not

- **Not the aging curve.** Adjacent, needs the same `BornOn` field, and deferred deliberately.
  Doc 15 §4 deserves its own plan.
- **Not a medical simulation.** Body areas exist to make recurrence risk site-specific
  (§3.1's doubled recurrence), not to model anatomy. Five or six areas, not fifty.
- **Not concussion and CTE modelling.** Doc 15 §2.3 and §8 are real and serious and belong to
  an era-and-policy system that does not exist. Out of scope here, and worth saying out loud
  rather than letting it arrive by accident.
- **Not a business axis.** Injury has a financial cost that doc 15 §6.1 gestures at, and this
  plan cannot price it for the same reason A3's blow-off cannot pay out as a business result:
  there is no business axis to pay into yet.
- **Not personalities.** §6 Tier 3 leaves a named seam and a function that returns 1.0. That
  is the whole of the commitment.
