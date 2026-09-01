# 31 — Mapping to WrestleSim

*What the engine models today, what the reference says is missing, and a prioritised view of
what to build next.*

This document is **opinion about design**, unlike the rest of the reference which is
description of the industry. It is written against the codebase as of this document's
creation; check the code before trusting the "current state" column.

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
| **`CardPosition` derived from popularity** | `Wrestler.CardPosition` | Position as an output of audience perception, not an input. |
| **Feud heat accumulated by booking, intensity derived** | `Feud.Heat` | "You don't declare a feud, you book one" — exactly right. |
| **Feud history tags gating beats** | `FeudHistoryTag` | Reproduces how real callbacks work: you can't do the revenge spot without the betrayal. |
| **Show position multipliers and same-kind fatigue** | `ShowSimulator` | Card composition as a real constraint. See [04](04-booking-philosophy.md) §3.8. |
| **Crowd mood carrying between items** | `ShowSimulator` | The crowd is a continuous state across a show, not per-match. |
| **Runtime budget with an overrun penalty** | `Show.OverrunFraction` | A real television constraint. |
| **Gimmick freshness that decays** | `Gimmick.Freshness` | The single most important hidden variable in wrestling, already present. |
| **Roster attributes spanning most of their legal range** | `RosterAttributes_UseMostOfTheirLegalRange` test | Enforcing spread so archetypes are distinguishable — a genuinely good engineering decision. |

---

## 2. The gap analysis

Ordered by **value-per-unit-of-work**, highest first.

### Tier A — high value, contained work

#### A1. Split `Popularity` into `Overness` (stock) and `Momentum` (flow)
**Reference:** [17](17-heat-and-getting-over.md) §1.1
`Popularity` currently does both jobs. Splitting them enables hot/cold dynamics, the
peak-and-cash-in decision, cooling from absence, and the entire heat lifecycle.
- `Overness` — slow-moving accumulated audience relationship; drives crowd ceiling
- `Momentum` — fast-moving current trend; drives reaction growth week over week and
  decays without exposure
- `CardPosition` should read `Overness`; crowd energy should read both.

#### A2. Heat transfer on match results (status economics)
**Reference:** [17](17-heat-and-getting-over.md) §6, [12](12-pushes-and-positioning.md) §6.1
The engine scores match *quality* but the result currently has no status consequence. Add:
- Beating a higher-`Overness` opponent transfers a meaningful amount
- Beating a lower-`Overness` opponent transfers almost nothing and costs the loser
- Losing without a story (clean, no interference, no stipulation) costs more than losing
  with one

This single addition makes booking decisions matter beyond the star rating, which is the
most significant thing missing from the sim right now.

#### A3. Feud heat decay and blow-off as a terminal event
**Reference:** [20](20-storylines-and-feuds.md) §9, [04](04-booking-philosophy.md) §1.1
- Feud heat should **decay** when the feud isn't advanced (currently it only accumulates)
- A **blow-off** should pay out accumulated heat as a large one-time result and *end* the
  feud
- Continuing past the blow-off should be penalised
- Never blowing off should eventually convert heat into audience distrust

#### A4. Match-count decay per pairing
**Reference:** [20](20-storylines-and-feuds.md) §9.1, [29](29-benchmarks-and-numbers.md) §10.1
`Feud.MatchCount` already exists and is described as feeding fatigue decay. Extend it to a
full curve: 1st 100%, 2nd 85–95%, 3rd (with stipulation) 90–110%, 4th+ 50–70% and falling.
This forces roster rotation and makes fresh pairings valuable — one of the strongest
pressures in real booking.

#### A5. Reaction *type*, not just magnitude
**Reference:** [16](16-crowd-psychology.md) §2
Crowd energy is a scalar. The real model is a small vector: **pop / heat / go-away-heat /
silence / hostility**. A match that is "hostile and engaged" should be a completely
different outcome from "quiet and uninvested", and silence — not booing — should be the
failure state.

#### A6. Persistent limb damage across beats
**Reference:** [18](18-match-craft.md) §2.2
Beats that target a body part should modify later beats, and failing to sell the damage
should cost storytelling score. This turns limb work from a beat type into actual ring
psychology and would make `Selling` a much more interesting attribute.

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

#### B3. Audience segments
**Reference:** [23](23-fanbase-segments.md)
Casual / regular / hardcore / family, each with separate satisfaction, each driving a
different revenue line, each wanting incompatible things. Turns booking from optimisation
into strategy. Probably the highest-ceiling addition in the whole reference.

#### B4. The promotion layer
**Reference:** [02](02-promotion-anatomy.md), [06](06-schedule-and-cadence.md), [09](09-revenue-and-costs.md)
The sim currently books shows in a vacuum. A promotion entity with tier, calendar, roster,
finances, media rights, and markets converts a match simulator into a management game.
Start with: tier, revenue lines, a TV contract with an expiry, and a show calendar.

#### B5. Markets
**Reference:** [07](07-live-events-and-touring.md)
Persistent markets with heat that depletes when run and recovers over time. Produces the
touring loop as an emergent strategy and gives `Show.Location` real meaning.

#### B6. Age and the aging curve
**Reference:** [15](15-injuries-and-attrition.md) §4
`Wrestler` has no age. Adding one plus per-stat curves — athleticism declining from ~28,
psychology rising to ~40, connection rising with tenure — reproduces the real career arc
with the stats the model already has.

#### B7. Injuries
**Reference:** [15](15-injuries-and-attrition.md) §3
Probabilistic, driven by volume, style, age, injury history, opponent safety, and
conditioning. `Toughness` and `Stamina` already exist as inputs. Injury history as a
permanent compounding attribute is the important detail.

### Tier C — distinctive, ambitious

#### C1. Merit vs Inertia
**Reference:** [28](28-cultural-inertia.md) §Sim implications
Split promotion drawing power into fast-moving `Merit` and slow-moving `Inertia`. Nostalgia
acts give an immediate boost while consuming `Inertia` and blocking a star-creation slot.
A competitor's existence lowers the `Inertia` floor. **Show the player good revenue numbers
while the warning signs accumulate.** This would be genuinely novel among wrestling games.

#### C2. Brands and split integrity
**Reference:** [22](22-brand-splits.md)
Brands with exclusivity, and a `SplitIntegrity` value that every crossover reduces. The
player gets an immediate ratings boost for putting their star on the other show, and pays
for it structurally. The entire real-world dynamic in one loop.

#### C3. Regional rule sets
**Reference:** [25](25-international-models.md)
Japanese mode (tours, tournaments, units, dojo, excursions), lucha mode (trios, masks,
apuestas), British mode (rounds and points). Each is a genuinely different rule set, not a
reskin.

#### C4. Eras
**Reference:** [26](26-eras-and-history.md) §4
A parameter set per era — revenue mix, schedule, contract type, content rating, match
length, medical policy, title reign norms — turns history into configuration rather than
special-case code.

#### C5. Rate well vs draw well as separate outputs
**Reference:** [18](18-match-craft.md) §7
The sim reports a star rating. It should also report a **business result**, and the two
should be able to diverge sharply. That divergence *is* professional wrestling.

---

## 3. Attribute gaps on `Wrestler`

Attributes the reference says matter that the model doesn't currently carry:

| Attribute | Reference | Why |
|---|---|---|
| **Age** | [15](15-injuries-and-attrition.md) §4 | Gates the entire career arc |
| **Safety** | [03](03-roles-and-competence.md) §5.4 | Injury risk to opponents; a social/reputation variable |
| **Reliability** | [03](03-roles-and-competence.md) §5.4 | Ends more careers than anything else |
| **Adaptability** | [03](03-roles-and-competence.md) §5.1 | Mitigates style clash; the Flair attribute |
| **Durability** | [15](15-injuries-and-attrition.md) | Distinct from Toughness — schedule tolerance, not bump tolerance |
| **Promo (scripted) / Promo (improv)** | [19](19-characters-and-promos.md) §3.5 | Two different skills; scripting policy determines which is used |
| **Locker-room standing** | [14](14-locker-room.md) §2 | Invisible to fans, decisive to careers |
| **Injury history** | [15](15-injuries-and-attrition.md) §3.1 | The strongest predictor of future injury |
| **Overness / Momentum** | [17](17-heat-and-getting-over.md) §1.1 | See A1 |
| **Merch draw** | [09](09-revenue-and-costs.md) §3.4 | The least deniable signal of who is over |

And on `Gimmick`:

| Attribute | Reference | Why |
|---|---|---|
| **Motivation** | [19](19-characters-and-promos.md) §2.3 | A gimmick without one is the identified failure mode |
| **Participation hook** | [16](16-crowd-psychology.md) §3.8 | Crowd bonus scaling with audience tenure |
| **Fit** (gimmick↔performer compatibility) | [19](19-characters-and-promos.md) §2.2 | The "80% the performer" rule; gates how much of the gimmick's potential is realised |
| **Comedy flag** | [19](19-characters-and-promos.md) §5.3 | The one-way door |

---

## 4. Suggested build order

A pragmatic sequence that keeps the game playable at every step:

**Phase 1 — make results matter** (all Tier A, contained to the existing engine)
1. A1 Overness/Momentum split
2. A2 Heat transfer on results
3. A3 Feud decay + terminal blow-off
4. A4 Match-count decay per pairing

*After this phase, booking decisions have consequences beyond a star rating.*

**Phase 2 — deepen the match** (Tier A remainder)
5. A5 Reaction type vector
6. A6 Persistent limb damage
7. A7/A8 Finisher and interference credibility

*After this phase, ring psychology is a real system.*

**Phase 3 — the promotion** (Tier B core)
8. B4 Promotion layer with a calendar and a P&L
9. B1 Championships
10. B5 Markets
11. B2 Booked vs perceived position

*After this phase, it is a management game.*

**Phase 4 — the world** (Tier B remainder + Tier C)
12. B6/B7 Age and injuries
13. B3 Audience segments
14. C1 Merit vs Inertia
15. C2 Brands, C3 regional rule sets, C4 eras, C5 dual outputs

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
