# The living world — a plan to talk through

**Status: a draft for the planning session. Nothing here is built and nothing here is decided
except where it says "decided".** It exists so the world outside the player's promotion is
designed once, as a whole, instead of being discovered one missing piece at a time.

## 0. Why this document exists

Every slice of the business layer has ended the same way: a figure looks wrong, and the cause
turns out to be that something outside the player's ring is not simulated. Rival promotions did
not book cards. Then they booked cards but nobody on them changed. The planner (slice 6) made
the pattern impossible to miss, because a promotion that plans ahead needs a world worth
planning in.

What the code does today, checked against the source on 13 Nov 2026:

| | Player's show | Rival's show |
|---|---|---|
| Money, reputation, TV audience | yes | yes |
| Audience tiring of acts and match-ups | yes | yes |
| Titles change hands, prestige moves | yes | yes |
| Overness and momentum from results | yes | **no** |
| Fatigue and sharpness | yes | **no** |
| Injuries | yes | **no** |
| Feud heat building | yes | **no** — match-ups are counted, nothing more |
| Tag teams, stipulations, turns | yes | **no** |
| Match quality from a worked match | yes | **no** — a formula on skill and overness |

And outside any show:

- **The population is a closed list of 90.** 49 men, 41 women, ages 21–47, median 35, nine
  under 27. Nothing creates a wrestler and nothing removes one.
- **Nobody's ability changes.** Ring skills, charisma, physical and mental attributes are never
  written after load. `AgeCurve` exists and is right in shape, but it is a ratio applied inside
  the player's match engine; a rival's show does not use it and the stored numbers never move.
- **Outside the player's ring the only thing that happens to overness is that it falls.** Five
  years of the default world take the median from 59 to 18.
- **Promotions never change tier, and none ever opens.** One that closes stays closed.

What followed from that in the planner's five-year run: the two TV companies signed every
wrestler the indies booked and both indies closed; exclusive deals were written at $9,000 a
year; the major banked $140M a year because its payroll cooled with its roster.

The reference docs already describe the real version of each of these. Doc 24: indies "cannot
retain the talent they make valuable" — but they are also "the industry's training system", so
the talent is replaced. Doc 15 §5: a career is 15–25 years, retirement at 35–45, and 5–10% of
those who start training ever have one. Doc 29: a Tier 0 promotion holds 150–200 people under
contract and a Tier 1 holds 100–160. The game has the drain and not the supply.

## 1. The rule this plan follows

> **Anything that can happen to a wrestler or a promotion happens the same way whoever is
> booking.** The player's promotion is one promotion in the world, running on the world's
> rules. There is no second, simpler simulation for everybody else.

Where a full simulation is too expensive to run for every promotion every night (a beat-by-beat
match is), the cheap version has to be *the same model summarised*, held to the full one by a
test — not a different model. `TheForecastIsTheWorldMinusTheNoise` is the pattern.

Two rules carried over from the planner, unchanged:

- When the world does something stupid, fix the model or the objective. Never add a rule.
- Nothing is tuned so that a named promotion or wrestler meets a named fate. Calibrating
  *rates* to the reference docs (how many debut a year, when people retire) is a different thing
  and is labelled as calibration.

## 2. The whole thing, in eight parts

Each part says what it is, what real life it stands for, a first proposal, and what is open.
The proposals are there to be argued with.

### Part 1 — Where the world comes from

**Decided by the owner (13 Nov):** at the start of a career the player picks the world: a
database of real people, or a fully generated one. A database that is too small for the
promotions in it is filled out with generated wrestlers. This replaces the standing rule "the
roster is real WWE wrestlers; do not invent wrestlers" — real people are still never altered or
invented *as* real people; generated people are marked as generated.

Proposal:

- A **world source** with three choices on the new-career screen: the bundled database (today's
  90), a custom database (loaded from a file — the loader is not built), fully generated.
- A **generator** that makes a wrestler from a seed: name, year born, year debuted, division,
  style, physical and mental attributes, ring skills, charisma, a gimmick, overness — drawn
  from distributions measured off the bundled 90, so a generated mid-carder looks like the
  hand-authored ones. Deterministic from the career's seed.
- **Top-up**: work out how many people the world's promotions need, by tier and division, and
  generate the shortfall. Generated people fill from the bottom: a top-up never invents a
  bigger star than the database's own.
- Generated names come from first-name and surname lists, checked against the database so
  nobody shares a real wrestler's name.

Open:

1. **How big is the world?** True scale (Tier 0 holds 150–200) or the game's present scale
   (the global major opens with 40)? This sets everything downstream, including how long a
   week takes to simulate.
2. Is "the real-life one" a third source distinct from the bundled WWE database — a bigger
   database covering several real companies — or the same thing?
3. Do generated wrestlers get full movesets and gimmicks, or a reduced set until the player
   books them?
4. In a fully generated world, are the promotions generated too, or are they the four default
   fictional ones?

### Part 2 — Careers that move

Real life (doc 15 §4–5): athleticism peaks at 24–28 and falls; ring psychology rises to 36–40
and holds; connection with an audience is tenure and takes a decade. People improve by working,
fastest early, and not all of them get where they could have.

Proposal:

- Every wrestler has **what they are** (today's attributes) and **what they could be** (a
  potential per skill group, hidden from the player except through scouting language).
- Attributes are **stored and they change**. A monthly step moves each one: toward potential
  with ring time and good opponents, away with age and injury. `AgeCurve` stops being a lens
  and becomes the thing that drives the step, so the number on the roster screen is the number
  the engine uses, for everyone, in every promotion.
- **Not everyone gets there.** Growth depends on being used, on who they work with, on injury,
  and on a per-wrestler trait (work rate / coachability) — so two rookies with the same
  potential end up different, for reasons the player can see.
- The bundled database's hand-authored numbers stay true on day one, as now.

Open:

5. Does the player see potential as a number, as words ("has a ceiling", "could main-event"),
   or only through results?
6. Is growth something the player can direct (who trains with whom, a performance centre), or
   only something that happens through booking? The second is far less to build.
7. Charisma and the gimmick: do they develop too, or is charisma fixed and the gimmick the
   thing that changes?

### Part 3 — Arriving and leaving

Real life: a class debuts every year; training is 6 months to 2 years; 3–7 years of indie work
before a meaningful chance; retirement at 35–45, later on a reduced schedule; serious injuries
end careers (doc 15).

Proposal:

- **A yearly intake.** Each year a class of rookies enters at the bottom of the unsigned pool,
  sized so the population holds steady against retirements. Most are ordinary; a few have real
  potential; one in a long while is a prodigy.
- **Leaving.** A yearly chance of retirement that rises with age, with accumulated injuries,
  and with having had nowhere to work. A retirement is news. A champion who retires vacates.
- **Coming back.** Doc 10 calls returns "extremely common". A small chance a retired name
  returns for a run — later, not first.
- The 5–10% funnel is represented by the intake already being the survivors of training; the
  washing-out that happens *after* debut is the "nowhere to work" exit.

Open:

8. Do real wrestlers in a real database retire on the simulation's schedule (Randy Orton gone
   in three years), or are real people protected for some period?
9. Do retired wrestlers stay in the world as anything — trainers, authority figures, agents —
   or simply leave?

### Part 4 — One set of consequences for every show

This is the table in §0. A rival's night must do to its people what the player's does: overness
and momentum from results, fatigue and sharpness, injuries, feud heat, tag-team chemistry.

It was tried once and reverted. Applying `HeatEconomy` to a rival's dice-rolled results took the
top ten from 92 to 67 overness in five years, because that arithmetic assumes somebody is
choosing who wins. So this part cannot be built without Part 5.

Proposal:

- A **summary match**: the same inputs and the same outputs as the worked match (a rating, a
  winner, a finish, a chance of injury, fatigue), computed without beats. Held to the full
  engine by a test that runs both on the same pairings and compares the distributions.
- Rival nights feed the summary match's results into the *same* `HeatEconomy`, `RingCondition`,
  `InjuryRisk` and `FeudBook` calls the player's nights use.
- The planner's forecast reads the same summary, so it stays honest for free.

Open:

10. Should a rival's show ever be worked beat by beat — when the player chooses to watch one,
    say — or is the summary always enough?

### Part 5 — Rival booking that means something

Real life (docs 04, 12): a booker decides who wins, protects the people who draw, builds
somebody up over months, and takes a belt off a champion who has gone stale. "Protecting
everyone destroys the graph" — somebody has to lose.

Today the favourite wins 72% of the time, a champion 85%, by dice.

Proposal:

- The rival's booker chooses **results as well as match-ups**, by the same forecast-and-score
  approach as the planner: who wins this, given what each outcome does to the people in it and
  to the cards it can run afterwards.
- A small set of **booking intents** that persist — a push for one wrestler, a title programme,
  a feud with a blow-off — so a rival does not re-decide its whole direction every week. This is
  the "persistent direction" the outside review asked for (plan §5A.6).
- The booker chooses its card's **make-up** (how many matches, a tag, a title defence or not),
  which retires the recipe and the `NoBetterWithout` patch.
- How good a rival is at this is its **competence**, a property of the promotion — so a badly
  run promotion books badly for reasons that are in the model.

Open:

11. How much of a rival's storyline does the player see — just results and champions, or its
    pushes and feuds as they build?
12. Rejection of a push (the Reigns case) was the next audience slice. It belongs here: it is
    the thing that makes a rival's — and the player's — push able to fail. Fold it in?

### Part 6 — Wrestlers who want things

Real life (docs 10, 11, 14): a wrestler chooses between offers on money, on how they will be
used, on where their friends are, and on loyalty. Somebody sitting unused asks for their
release. The cheapest exclusive deal at a TV company is $50–100k (doc 29).

Proposal:

- A **floor under a guarantee**, from doc 29. (Moves the opening rosters, so the measured
  medians are re-measured with it.)
- A wrestler **weighs an offer**: money, expected position on the card, the promotion's
  standing, tenure where they are. The best money does not always win.
- **Morale from use**: unused or misused people become unhappy, ask out, and work worse. This is
  the real-life cost of hoarding, and the thing that should limit a TV company signing forty
  people it will never book.

Open:

13. The locker room (doc 14) — relationships, cliques, backstage heat — is a large subject of
    its own. In this plan as "morale from use" only, with the rest later?

### Part 7 — Promotions that rise, fall, open and close

Real life (docs 01, 26): promotions grow and shrink with their audience, win and lose
television, and new ones are founded when there is money and talent to found them with.

Proposal:

- **Tier follows sustained results**: a promotion that has filled its houses and held its
  reputation for long enough steps up; one that has not, steps down. The player's included —
  which is the career's long game.
- **Television is won and lost**, for rivals as for the player, and the network can walk away.
- **New promotions are founded** when the pool of unsigned talent and the gap in the market
  make room, with a founder motive drawn from doc 01.

Open:

14. Is tier movement for the player in scope here, or its own slice? It is the largest
    player-facing change in the plan.

### Part 8 — A way to tell whether it works

One deterministic world is one sample. The planner's findings were read off a single run.

Proposal:

- A **harness** that runs many worlds (different seeds, and different sources) for ten to
  twenty years with no player, and reports distributions: population and age pyramid, overness
  by percentile over time, how long a star stays a star, title reign lengths, how many
  promotions close and open, roster sizes by tier, wages.
- Each distribution sits beside the reference figure it should resemble. Outside the range is
  something to look at, not a test failure.
- Run by hand and in a nightly-style job, not in the unit suite.

## 3. Order of work

The parts depend on each other, so the order is mostly forced:

```
Part 1  population ──┬─► Part 2 careers ──┬─► Part 4 consequences ──┐
                     └─► Part 3 arriving  ┘        ▲                ├─► Part 8 harness ─► re-measure
                                           Part 5 booking ──────────┘
                                                                     then Part 6, Part 7
```

1. **Part 1** — nothing else can be judged in a world of 90.
2. **Parts 2 and 3 together** — a population that ages with nobody arriving is a countdown.
3. **Parts 4 and 5 together** — consequences without a booker deflate everybody.
4. **Part 8** — and at that point, not before, **re-measure everything**: `TypicalInvestment`,
   `TypicalCardPullFor`, the daily overheads, the planner's objective weights.
5. **Parts 6 and 7.**

**Until step 4, no business-layer constant is tuned.** They are all calibrated against a world
that drains.

"The whole thing" means all eight. Parts 1–5 are what make the world live; 6 and 7 make it
react; 8 is how any of it is judged.

## 4. What this changes that is already built

- **The roster rule in `CLAUDE.md`** ("do not invent wrestlers") is replaced by Part 1.
- **`AgeCurve`** changes from a lens to a driver. The player's match results must come out the
  same on day one.
- **Saves** gain generated wrestlers, potentials, changing attributes. Additive, with a
  migration for existing careers: they keep their 90 and are topped up.
- **Performance.** A week of the clock takes 0.5–2.7 s in the browser now. A world four to ten
  times the size, with consequences on every show, has to be designed for that from the start,
  not optimised afterwards. A budget is set in the planning session.
- **Every measured constant** moves. That is expected and is step 4.

## 5. Not in this plan

Written down so they are not mistaken for covered: the locker room beyond morale (doc 14),
brand splits for rivals (doc 22), touring and house shows (doc 07), international territories
and excursions (doc 25), managers and non-wrestlers, merchandise as its own business, and
eras — the industry's own boom and bust (doc 26).

## 6. Questions for the planning session

The fourteen open questions above, most consequential first:

1. How big is the world — true scale or the game's scale? (Part 1)
2. Is there a third "real-life" source beyond the bundled database? (Part 1)
3. Do real wrestlers retire on the simulation's schedule? (Part 3)
4. Can the player direct development, or only book it? (Part 2)
5. How is potential shown? (Part 2)
6. Fold rejection of a push into rival booking? (Part 5)
7. Is tier movement for the player in this plan? (Part 7)
8. How much of a rival's storylines does the player see? (Part 5)
9. Are promotions generated in a generated world? (Part 1)
10. Morale only, or more of the locker room? (Part 6)
11. Do generated wrestlers get full movesets and gimmicks? (Part 1)
12. Does charisma develop? (Part 2)
13. Is a rival's show ever worked beat by beat? (Part 4)
14. What becomes of retired wrestlers? (Part 3)
