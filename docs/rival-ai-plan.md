# Rival AI — from rules to objectives

*A build plan. Written to be executed by a later session with no memory of the conversation
that produced it. Read the whole file before writing code.*

## 0. What this is, in one paragraph

Rival promotions currently decide things with hand-written rules in a fixed order
(`World.Business` in `WrestlingSim.Core/Engine/RivalSim.cs`): if cash is short release two, else
bid on anything "wanted", else sign up to two. A founder motive is five dials on those rules.
The owner of this repo wants the opposite design: **each promotion has an objective, a way of
forecasting what a move would do, and picks the move that best serves the objective.** Nobody
scripts *what* it does or in what order. The behaviour we want — the wealthy fan burning out, a
media company spending before its TV renewal, a business cutting dead weight — should emerge,
and is checked by long simulated runs, not by rules.

The standard name for this is **utility-based AI with a forward model**. The forward model
already exists: it is the game's own economics (`BoxOffice`, `Television`, `TalentMarket`).

### The one rule of this work

> **If the planner does something stupid, fix the model or the objective. Never add a rule.**

**And its corollary, from the owner: whatever stops something in real life is what must stop
it in the game.** A national promotion cannot run on six wrestlers because the audience would be
bored in a fortnight and the titles would mean nothing — so the game must have an audience that
gets bored and titles that need divisions. "The model allows it" is never a reason to accept a
behaviour; it is a bug report against the model. **Nothing already built is off limits**: if the
audience model, the box office or the title system has to change to make this true, propose the
change (§2A does) — do not work around it.

A rule ("never go below 20 wrestlers", "always bid on champions") is exactly what this rebuild
removes. When a simulated run shows degenerate behaviour, the cause is one of two things: the
abstract world is missing a cost real promotions pay (fix the model, §3), or the objective values
the wrong thing (fix the weights, §5). Write down which it was. If neither fix works, **stop and
report it** — do not paper over it. Add this rule to `CLAUDE.md` in PR B.

---

## 1. Where things stand

Read these first:

- `WrestlingSim.Core/Engine/RivalSim.cs` — `World.Tick`, `RunNight`, `PairUp`, `Rating`,
  `Wants`, `SettleContracts`, `Business`, `FreeAgents`, `Fold`. Most of this file is rewritten.
- `WrestlingSim.Core/Engine/World.cs` — `MotiveProfile`, `Profile`, `DefaultRivals`,
  `CredibleEmployers`, `WageFactor`, `Interest`, `Open`.
- `WrestlingSim.Core/Models/World/Rival.cs` — `RivalPromotion`, `RivalNight`, `NonCompete`,
  `Bid`, `NewsItem`, `FounderMotive`.
- `WrestlingSim.Core/Models/World/Career.cs` — the "world" section: `Sign`, `Release`,
  `SettleContracts`, `Unavailable`, `BestRivalBid`, `PlayerBid`.
- `WrestlingSim.Core/Engine/BoxOffice.cs` (`Night`, `Draw`, `CardPull`, `Demand`,
  `ReputationAfter`), `Television.cs` (`TuneIn`, `Retention`, `AudienceIndex`,
  `RenewalMultiple`, `RenewedFee`), `TalentMarket.cs`, `OpeningRoster.cs`.
- `WrestlingSim.Tests/RivalTests.cs`, `BusinessTests.cs`, `ContractTests.cs`.
- `docs/business-design-record.md`, "Slice 4" — what rivals do today and what was measured.
- `docs/wrestling-reference/01-industry-map.md` §7 (founder motives),
  `08-television-and-media.md` §2–3 (what a network wants, the renewal cycle),
  `09-revenue-and-costs.md` §2.2 and §5, `13-releases-and-firings.md` §2.

**Prerequisite.** The rivals slice is commit `597e59a` on `claude/lucid-fermi-unlfr7`, pushed,
with **no pull request open**. Ask the owner whether to open and merge it before starting, or
to build this on top of it. Do not start from `main` without it.

### What is rules today, and what replaces it

| Today (rule) | Replaced by |
|---|---|
| `MotiveProfile.Budget`, `BudgetOf`, the 1.1×/1.15× budget checks | The Wealth and Survival terms of the objective (§5) |
| `MotiveProfile.BuysStars` sort order | The Stars / Quality / Wealth weights |
| `MotiveProfile.BidPremium` | A reservation price computed from the objective (§6.3) |
| `Wants` ("at least as over as its middle") | Whether signing improves utility |
| "Release two worst-value when cash < 0" | Release is a move like any other, taken when it improves utility |
| "Sign up to two a week while cash > 10% of opening" | Up to `MaxMovesPerWeek` best improving moves |
| `PairUp` (best vs next-best, every week) | `BookCard`, which chooses the card that best serves the objective (§3.2, §8) |
| Re-sign if `Wants` and under budget | Re-sign if keeping at the price beats letting go; counter the player up to the reservation price |

**What stays as it is — these are physics, not behaviour:** daily running costs and payroll,
contract tolling, the fold threshold (`FoldAt`), non-competes, exclusivity, `WageFactor` and
`Interest`, TV renewal on the clock, and `World.Open` (see §10 for why the opening is left alone
in this plan).

---

## 2. The shape of the design

Four parts, each a separate concern:

```
  Objective      what this promotion wants        data: weights per term     §5
  Disposition    how it sees the future           data: horizon, optimism…   §5
  Forecast       what a roster would produce      pure function, uses the    §4
                                                  game's own economics
  Planner        try each move, forecast it,      generic; knows nothing     §6
                 score it, take the best           about any motive
```

The planner is the same code for every promotion. A wealthy fan and a media company differ
**only** in the `Objective` and `Disposition` data they are handed. That is the test of whether
the design has been followed: there must be no `switch` on `FounderMotive` anywhere in the
planner, the forecast or the card booker. `FounderMotive` appears in exactly one place — the
table that maps a motive to its default objective and disposition.

Objectives are data so they can be set per promotion later (a custom database naming a
promotion and what it wants). `RivalPromotion` gets optional overrides for both.

---

## 2A. PR 0 — the audience values the whole show

> **Status.** The owner approved this section. **The first half is built** — the shared model and
> the player's shows: §2A.1 (a), (b) and (d), §2A.2, and the player-side tests of §2A.5. See
> `docs/business-design-record.md`, "Slice 5", for what was built, measured and corrected.
> **Still to build from this section:** §2A.2a (rival cards as real cards), §2A.3 (rival
> titles), a rival's own `Audience`, the rival-side tests of §2A.5, and §2A.4 if those are not
> enough. §2A.1 (c), rejection, is its own slice.

**This comes before everything else, and it changes the player's game as well as the rivals'.**

The owner's test case: *a roster of six.* In real life it fails at anything above the most
local level, within weeks. The reference says why:

| Why it fails in real life | Source | In the game today |
|---|---|---|
| Overexposure — "on television every week, doing the same thing… the reactions just get smaller" | doc 17 §3.2, doc 16 §4.10 | Missing. A *pairing* goes stale (`Feud.Familiarity`) and a body gets tired (`Fatigue`), but the audience never tires of a *person*. |
| A show is 4–6 matches and 4–7 segments; the crowd has an energy budget across the night | doc 29 §6, doc 16 §5 | Half there. `BoxOffice.CardPull` reads the top three items only; nothing penalises a show that under-fills its runtime. |
| A division needs 6–10 performers; a champion needs 6–12 credible defences a year | doc 29 §12–13, doc 21 | The player has titles and prestige. **Rivals have no titles at all.** |
| Status comes from beating credible people — "protecting everyone destroys the graph" | doc 12 §6 | The player's results move standing (`HeatEconomy`). Nothing moves from a rival's show. |
| One injury and there is no show | doc 15 | Rival shows cause no fatigue and no injuries. |

So the hole is not in the rival AI. A *player* could run a thin roster today and pay for it only
in fatigue. The fix is one audience model for everybody.

### 2A.0 What research found (and what it overturned)

The first draft of this section modelled *exposure*: appear too often and the audience tires.
The owner asked for research before any number was chosen. It overturned the premise.

**Quality of evidence, stated plainly:** nothing measures this for wrestling. The wrestling
sources are trade press and opinion; the measured work is from advertising and television and
is applied here by analogy. Treat every rate below as a design estimate to be tuned in play.

- **Screen time alone does not wear an audience out.** Parasocial research finds liking and
  attachment *rise* with exposure to a character (Rubin & McHugh 1987; a season-long tracking
  study at the University of New Brunswick found small weekly increases). Advertising research
  largely fails to find wear-out from repetition (Craig, Sternthal & Leavitt 1976 found none at
  very high repetition; Kantar finds none in its tracking; System1 found 14 worn-out creatives
  in 50,000) and attributes what exists to saturation and weak creative.
- **Sameness does.** Berlyne's work ties boredom to low novelty and low complexity; liking
  peaks at intermediate complexity, and richer stimuli survive repetition longer.
- **Wrestling cases fit that, not the exposure story.** Rhea Ripley was on screen constantly
  through 2023–24 and stayed a top merchandise seller; the complaints were from a subset of
  fans, about always being in the title picture. Hulk Hogan's "first real sign fans were tiring"
  (the 1992 Rumble) came after years of *the same act* headlining every major show.
- **Rejection is a different mechanism from boredom, and it is segment-specific.** Roman Reigns
  2015–18 was the audience sensing the promotion overruling its choice (doc 23 §7 already lists
  "rejecting a push: sensing the promotion is overruling them"). John Cena was cheered by
  children and booed by adults in the same building for years, while the top merchandise seller.
- **Show-level fatigue is real and separate.** Raw routinely lost 20–28% of its audience by its
  third hour; critics of its 2018–19 decline cite the same matches week after week.

This repo's own doc 17 already had the right variable: §4, "Freshness — the underlying
variable… everything in wrestling decays", with decay periods per *thing* (a match-up: 2–4
encounters; a new character: 6–18 months; a faction: 12–24 months), and §3.4: "athletic novelty
depletes faster than character novelty".

**The six-wrestler case needs no tuned number.** Six people in two divisions have about six
possible match-ups. At 2–4 encounters each, a weekly show has spent every match it can offer in
about a month; a promotion running every six weeks takes years to. Pairing arithmetic on the
existing freshness curve already says "weeks at National, fine at Local".

### 2A.1 Staleness — sameness, not screen time

Three separate things, per the owner. Do not merge them into one meter.

**(a) A wrestler's act goes stale when nothing about it changes.**

- `Wrestler.Staleness` (0–100, per promotion's audience, persisted). Each appearance adds
  staleness **unless the appearance carried something new**. "New" is read from what the game
  already records: a first-time or long-rested opponent (the pairing's freshness), a feud that
  gained heat or a history tag since the last appearance, a title won or lost, a turn or gimmick
  change, a stipulation not run lately (`StipulationBook`). Something new *reduces* staleness.
- **The rate is the wrestler's.** It scales down with `Charisma` and with a gimmick that has
  somewhere to go, and up for overness that rests on athletic novelty (doc 17 §3.4). The game
  has `Charisma` and `Style` today; doc 31 §3 already lists the missing gimmick attributes
  (*Motivation*, *Fit*) — if a "depth" reading cannot be derived honestly from what exists,
  say so and propose the attribute rather than inventing a proxy.
- Time off screen lets it fall, but slowly, and doc 17 §3.9 still applies: six to ten weeks
  away with no story costs heat. Rest is not a free reset.
- Calibration targets are doc 17 §4.1's ranges, not a guess: a strong character doing the same
  thing should hold for many months; a spot-based act for far fewer.

**(b) Each audience segment feels it differently.**

- The game has `FanGroup` and per-gimmick `AppealRatings`, and the crowd model reads them. Give
  each group a staleness tolerance and a taste (doc 23 §2–3): casual fans want familiar stars
  and barely tire; regulars want stories that progress; hardcore fans want novelty and tire of
  dominance ("winning too much, too easily" — doc 17 §3.11).
- **Add a Regular group, as a viewing habit only** (owner's default, confirmed in principle).
  `FanGroup` is Casual, Hardcore, Online, International, Families, Kids, but in the shipped
  roster only Casual (88 of 90) and Hardcore (89 of 90) are filled in — "Casual" has meant
  "everybody who is not hardcore". Doc 23 splits that by how often they watch: the casual
  viewer comes for big events and famous names; the regular viewer watches weekly and is "the
  audience the weekly product is actually made for". For staleness that difference is the whole
  point — the regular sees everything and feels sameness first; the casual sees little and
  barely feels it. So: add `FanGroup.Regular`; where a gimmick has no Regular appeal, read its
  Casual appeal (**no roster data is rewritten**); the two differ only in how often they watch
  and what bores them. Weekly television audiences lean Regular; premium events and big gates
  lean Casual.
- So staleness is evaluated **per group**, and one wrestler can be hot with one and stale with
  another — the Ripley and Cena pattern. The draw is the groups' responses weighted by who is
  in the building or watching.
- This is the first half of B3 (audience segments) in `31-sim-mapping.md`. Keep it to what
  staleness needs: per-group tolerance and per-group response. Separate per-group satisfaction
  and revenue lines stay out of this slice — write that down.

**(c) Rejection is its own mechanic.**

- When the booked position runs ahead of the perceived one — someone pushed whom the audience
  did not choose — the response turns hostile, hardcore first, and escalates with insistence
  (doc 12 §4.3, doc 23 §7, doc 27's Bryan and Reigns patterns). This is B2 in the mapping doc.
- It is **not** part of staleness and must not be folded into it. It is listed here because the
  research showed the famous "overexposure" cases were mostly this. **Out of scope for PR 0;
  the owner has agreed it is its own slice, straight after this one.**

**(d) The show itself goes stale** — see §2A.2.

Player-visible: a staleness reading per wrestler that says *why* ("the same match four weeks
running — hardcore fans are restless, casual fans are fine"), in the roster, the picker and the
box office's reasons.

### 2A.2 The whole card counts

- Demand (tickets, tune-in) stays led by the top of the card — story sells the ticket. Keep
  `BillingWeights`.
- **Retention and reputation read the whole show**: every item's rating, weighted by its
  minutes, not the top three. For rivals this is `ExpectedRating` over every match booked.
- **Under-filling the runtime is penalised** like overrunning it already is: a television show
  that fills 60% of its slot is a worse show. Applies to the player (`ShowSimulator`) and to
  rivals (the short card of §3.2).
- **The same person twice in a night** is two appearances with nothing new between them, and is
  flagged in the booking read.
- **The show's own staleness** (separate from any wrestler's): a per-promotion reading of how
  alike recent shows have been — the same match types, the same running order, the same faces in
  the same slots, the same stipulation. It lowers retention and reputation for everybody on the
  card. Variety resets it. Raw losing a fifth of its audience by hour three is the reference
  case. It applies to rivals as well, which requires §2A.2a.

### 2A.2a Rival cards are real cards (owner's ruling)

Rival shows were built as five plain singles pairings and nothing else — no tags, no title
matches, no stipulations, no segments. That was a shortcut taken under the word "abstract", and
the owner has ruled it out: rivals book with the same building blocks the player has.

- A rival card is a list of the **same `ICardItem` types the player books**: `BookedMatch`
  (singles, tag, trios, multi-man — `MatchPlan.Sides`), with `TitleAtStake` and `Stipulation`
  set where booked, and `Segment` for promos and angles. `BoxOffice.DrawOf` already prices all
  of these; nothing new is needed on the draw side.
- **Still not simulated beat by beat.** "Abstract" now means only that: the rating of an item
  comes from who is in it, what kind of item it is, and what is at stake — not from running
  `MatchEngine`. Give each item kind an expected-rating rule built from what the engine already
  knows (side size, the structure's intent, stipulation), and say in the design record how far
  that sits from the real engine on a sample of the same matches.
- `StipulationBook` per rival, so a stipulation run too often is worth less to it too.
- Standing tag teams: rivals use `TagTeamSeed` / `TagTeam` chemistry for their own rosters.
- This is what lets show staleness, title defences and "something new" (§2A.1a) mean anything
  for a rival, and it is what PR C's booker (§8) chooses between.
- Player-visible: the Rivals screen's "last night" lists the card — match types, the title on
  the line, the segment — not just a headline.

### 2A.3 Rivals have titles

- Each rival gets a `TitleRegistry`, seeded with the same default slate as the player's
  (`SeedDefaults`). Reuse `TitleEconomy` — do not write a second prestige model.
- A rival's premium event books a title match per belt: the champion against the most credible
  available challenger *by the ledger's freshness* — a thin division runs out of fresh
  challengers, repeats, and the belt's prestige falls through the existing daily drift and
  defence rules.
- Title stakes feed a rival's draw exactly as they feed the player's (`ItemDraw.Stakes`).
- A wrestler who leaves vacates, as the player's already do (`Career.VacateTitles` — generalise
  it to take a registry).
- Player-visible: champions and prestige on the Rivals screen.

### 2A.4 Results and wear on rival shows (second step — build only if 2A.1–3 are not enough)

- Abstract results: each match has a winner chosen by the booker (§8 makes that a decision);
  `HeatEconomy` moves overness and momentum as it does for the player. Enhancement talent and
  depth become worth paying for, and rivals can make — or bury — stars.
- `RingCondition` work cost and `InjuryRisk` on abstract matches, so a thin roster is one bad
  night from a short card.

This is the largest piece and touches the most. Do 2A.1–3, run the test, and only then decide.

### 2A.5 The acceptance test is the owner's claim

New file `AudienceTests.cs`, corpus tests, `StableSeed`:

- `SixWrestlersCannotCarryANationalPromotion` — build a National promotion with the six most
  over wrestlers and nobody else; run its weekly television with the best card those six can
  make. Its fill and viewers fall steeply and its reputation with them. **Do not assert a
  number of weeks chosen in advance**: measure how long it takes, record it, and check it
  against doc 17 §4.1 (a match-up is fresh for 2–4 encounters, and six people have about six).
  If it takes many months, the model is wrong. No line of code may mention roster size.
- `ACharismaticActOutlastsASpotAct` — two wrestlers of equal overness doing the same thing
  weekly: the one with charisma and character holds; the one built on athletic novelty cools
  first (doc 17 §3.4).
- `SomethingNewResetsIt` — a stale wrestler given a fresh opponent, a turn or a title is no
  longer stale.
- `OneWrestlerTwoAudiences` — strong booking held for months leaves casual response intact and
  hardcore response falling.
- `SixWrestlersCanCarryALocalOne` — the same six at Local cadence hold their houses for a year.
- `ADeepRosterHoldsItsAudience` — the same National promotion with a tier-sized roster holds
  within 10% of week one over the same eight weeks.
- `AChampionWithNoDivisionDevaluesTheBelt` — a rival with a two-person division loses prestige
  on that belt over a year; one with eight does not.
- `ThePlayerPaysForSamenessToo` — a player's wrestler in the same match six weeks running draws
  less than the same wrestler given something new.
- `BeingOnEveryWeekIsNotTheProblem` — a wrestler on every show for six months, in a feud that
  keeps moving, does **not** go stale. This is the test that the model is sameness and not
  screen time.

### 2A.6 What this costs

- **It re-calibrates the player's economy.** `BoxOffice.TypicalCardPullFor` is measured on a
  fresh opening roster, so it should not move — but every multi-week measurement (tier margins
  at cadence, rival fold timing) will. Re-measure; re-solve the running costs if a tier leaves
  doc 09 §4's band; record before and after in the design record.
- **Saves**: `Staleness` per wrestler per promotion, show staleness per promotion, rival titles. Additive; an old save opens
  with everybody fresh and rivals' belts vacant-then-filled on the first premium event.
- **Scope.** This is a slice of its own — the size of the box-office slice — and it is worth
  doing whether or not the planner is ever built, because it closes a hole in the player's
  game.

---

## 3. PR A — make the abstract world honest enough to optimise against

**Do this straight after PR 0, and do not skip it.** PR 0 gives the world an audience; this PR
gives rivals the remaining pieces of an honest abstract show and the forecast built on it.
An optimiser exploits every gap in its model. Today's
abstract show has gaps a planner would walk straight through:

- `BoxOffice.CardPull` reads only the top three items, and a show is five matches. A planner
  would conclude that a roster of about six people is optimal and release thirty-four of forty.
- The same top pairing headlines every week with no cost. Real crowds tire of a rematch — the
  player's matches already pay for this through `Feud.Familiarity`; rivals pass `feuds: null`.
- A show with too few bodies still runs at full value.

So before any planner exists, the abstract show gets the costs that make depth worth paying for.

### 3.1 A pairing ledger: rematches go stale

New class `PairingLedger` in `WrestlingSim.Core/Models/World/PairingLedger.cs`:

- Holds, per unordered pair of wrestler ids, `(double Meetings, DateOnly Last)`.
- `double MeetingsRemembered(a, b, DateOnly today)` — the same forgetting as
  `Feud.MeetingsRemembered`: nothing forgotten for `Feud.FreshnessGraceDays` (60), then one
  meeting per `Feud.FreshnessRecoveryDays` (120) idle days. Reuse the constants; do not copy
  the numbers.
- `double Freshness(a, b, today)` = `Feud.FamiliarityFor(MeetingsRemembered + 1.0)`.
  `FamiliarityFor` is the existing static curve (1.00, 0.90, 0.85, 0.65, 0.57, 0.50 …).
- `void Record(a, b, today)` — applies the forgetting, then adds one meeting.
- `PairingLedger Clone()` — the forecast simulates shows on a copy. It must be cheap: one
  dictionary copy.
- Each `RivalPromotion` owns one (`Pairings`). Persist it (see §9).

Why not give each rival a real `FeudBook`: `FeudBook.Among` returns live stories only, a
heatless feud may not count as live, and a `FeudBook` is expensive to clone for every forecast.

Wire it into the draw without touching the player's path: add an optional
`Func<BookedMatch, double>? freshness = null` to `BoxOffice.Draw`, `BoxOffice.DrawOf` and
`BoxOffice.Night`. When supplied, a match's `ItemDraw.Freshness` is that value instead of the
feud's familiarity. The player never passes it, so `Career.Projection` is unchanged — pin that
with a test (`ThePlayersBoxOfficeIsUntouchedByRivalFreshness`).

### 3.2 `BookCard`: the card is chosen, and it has a size

Replace `World.PairUp` with `World.BookCard(roster, ledger, today, size, score)`:

- `size` is `rival.Promotion.TypicalCardSizeFor(type)` (5–8; it exists on `Promotion`).
- Candidate pairings: within each division, every pair among the available wrestlers.
  For a division of n that is n(n−1)/2 pairs; cap the pool at the 14 highest-scoring
  wrestlers per division to bound it (91 pairs).
- Greedy: repeatedly take the highest-scoring pairing whose two wrestlers are both unused,
  until `size` matches are booked or no pairing is left.
- In PR A the score is draw only: `StarPower × Freshness` — compute it by building the
  `BookedMatch` and calling `BoxOffice.DrawOf(match, null, today, freshness)` and reading
  `.Pull`. In PR C the score becomes the objective's (§8). Take the score as a
  `Func<BookedMatch, double>` parameter now so PR C changes one argument.
- Deterministic: break ties by the two wrestler ids, ordinal.

A card that cannot be filled is a **short card**: if `booked < size`, the show's rating is
multiplied by `booked / (double)size`, and the `RivalNight` records it (`Short = true`). This is
the cost of a thin roster. No card at all (zero matches) means no show, as today.

After a show runs, `Record` every pairing on it in the ledger.

### 3.3 Split the rating into expectation and noise

`World.Rating` becomes:

- `World.ExpectedRating(rival, worked, booked, size)` — the deterministic part:
  `22 + 6 × avg skill + 0.2 × avg overness + traits.RatingBias`, times the short-card factor,
  clamped 5–95.
- `World.Rating(...)` = `ExpectedRating` + the existing seeded ±8 noise. Keep the seed
  expression `StableSeed.From(rival.Id, on.DayNumber)` exactly, so existing runs do not shift
  for a reason unrelated to this work.

The forecast uses `ExpectedRating`. The world uses `Rating`.

### 3.4 Split `MotiveProfile` into traits and (later) objective

Rename `MotiveProfile` → `MotiveTraits`, keeping only what is **physical** about an
organisation: `Overhead`, `RatingBias`, `OpeningBudget` (the old `Budget`, now used *only* by
`World.Open`), and `Reading`. `BidPremium` and `BuysStars` stay in PR A (the old `Business`
still reads them) and are deleted in PR B.

### 3.5 The forecast

This is the heart of the rebuild, and it lands in PR A so it can be tested against reality
before anything depends on it. See §4 for the full specification.

### 3.6 PR A acceptance

- `BookCard`, `PairingLedger`, `ExpectedRating`, `World.Forecast` exist and are used by
  `RunNight` (booking, freshness, short cards). `Business` is still the old rules.
- Tests (new file `WrestlingSim.Tests/RivalModelTests.cs`):
  - `ARematchEveryWeekGoesStale` — the same pairing booked eight weeks running draws less each
    time, and recovers after `FreshnessGraceDays + FreshnessRecoveryDays` idle.
  - `ADeeperRosterKeepsItsMainEventsFresh` — over 26 simulated weeks, a 12-per-division roster
    averages a higher top-item freshness than a 4-per-division one.
  - `AThinRosterRunsAShortCard_AndItRatesLower`.
  - `TheForecastIsTheWorldMinusTheNoise` — see §4.4. **The most important test in PR A.**
  - `ThePlayersBoxOfficeIsUntouchedByRivalFreshness`.
  - `BookCardIsDeterministic`.
- Re-run the three-year world probe (§11.2) and record in the design record what PR A alone did
  to: Ironclad's fold month, Vantage's margin, the indies' survival. The existing test
  `TheWealthyFansPromotionBurnsOut_TheMajorDoesNot` asserts 18–40 months; if staleness moves
  Ironclad outside it, **re-solve `MotiveTraits.OpeningBudget`/`Overhead` and say so** — do not
  widen the band. (The band is doc 01 §7's.)
- `BoxOffice.TypicalCardPullFor` should **not** move: it is measured on opening rosters with the
  player's box office, which this PR does not touch. Confirm
  `ATypicalCardFillsTypically_AndThatIsMeasuredNotAsserted` still passes untouched. If it fails,
  something leaked into the player's path.
- Player-visible: the Rivals screen's "Last night" shows a short card when there was one, and
  the top item's freshness ("main event seen 4 times lately").

---

## 4. The forecast

`WrestlingSim.Core/Engine/RivalForecast.cs`. A pure function: no mutation of the career, the
rival or any wrestler.

```csharp
public sealed record Outlook(
    double  CardPull,            // average over the simulated cycle
    double  Rating,              // average expected rating
    double  Fill,                // average attendance / capacity
    int     Viewers,             // average per televised night; 0 below television
    decimal RevenuePerShow,
    decimal NetPerShow,          // before running costs and payroll
    decimal DailyFixed,          // running costs + payroll, per day
    decimal CashAtHorizon,
    double  ReputationAtHorizon,
    double  RenewalMultiple,     // projected at term end; 1.0 below television
    double  YearsToRenewal,
    int     RunwayDays,          // days until cash crosses the fold line; capped at 9999
    double  FavourShare,         // share of headline slots held by favourites (§5.3)
    bool    FillsItsCard);

public static Outlook Forecast(
    Career world, RivalPromotion rival,
    IReadOnlyList<Contract> roster,     // the hypothetical roster
    decimal oneOffCost,                 // e.g. a release payout
    Objective objective, Disposition disposition);
```

### 4.1 How it is computed

1. **Simulate a short cycle of shows** — `CycleShows = 6` — on a `Clone()` of the rival's
   pairing ledger, using the **same** `BookCard` the world uses, with the same score function,
   continuing the rival's actual show-type sequence (three television, one premium event) and
   stepping the date by the tier's `TypicalShowIntervalDays`. Each show is priced by
   `BoxOffice.Night` with the rival's current reputation and TV deal, `world.Interest`, the
   freshness function from the cloned ledger, and the fee function
   `w => guaranteedInRoster(w) ? 0 : world.PerDateAsk(w)`. Record each pairing in the cloned
   ledger so staleness accrues across the cycle. Rating is `ExpectedRating`.
   Availability filter is the same as `RunNight`'s (not injured, no non-compete, not held by
   somebody else).
2. **Average** pull, rating, fill, viewers (`TuneIn × Television.Retention(rating)`), revenue
   and net across the cycle.
3. **Optimism** applies here and only here: `revenue' = gate and merchandise × Optimism +
   rights`, `net' = revenue' − show costs`. A promotion with `Optimism = 1.25` believes its
   cards sell a quarter more than they do. Rights are contractual and are not inflated.
4. **Scale to the horizon** `H = disposition.HorizonDays`:
   `shows = H / interval`; `DailyFixed = overhead × traits.Overhead + Σ roster DailyCost`;
   `CashAtHorizon = cash + shows × net' − H × DailyFixed − oneOffCost`.
5. **Reputation at the horizon:** apply `BoxOffice.ReputationAfter` once per show at the average
   rating, with the cycle's show types. A loop of at most ~120 steps of arithmetic.
6. **Renewal** (television tiers): add `Television.ProjectedMultiple(deal, tier, viewers,
   interval, today)` — the renewal multiple if every remaining episode of the term drew
   `viewers`. Compute it in closed form, not by building hundreds of episode objects:
   with `S, W` the weighted sum and weight of the episodes so far (final-year episodes weigh 2,
   as in `AudienceIndex`), and `n1`, `n2` the remaining episodes before and inside the final
   year, the projected index is `(S + (viewers / baseline) × (n1 + 2·n2)) / (W + n1 + 2·n2)`;
   consistency is taken as it stands today. Pin it with a test that it equals
   `RenewalMultiple` on a deal explicitly extended the slow way.
7. **Runway:** `foldLine = OpeningCash × FoldAt`; `dailyNet = shows × net' / H − DailyFixed`;
   `RunwayDays = dailyNet >= 0 ? 9999 : (int)((cash − oneOffCost − foldLine) / −dailyNet)`.

### 4.2 Invariant: the forecast books with the booker

The forecast must call the very function the world calls to book a card, with the very score
function. If they drift, the planner optimises a world that does not exist. There is one
`BookCard` and one place that builds the score function from an objective.

### 4.3 Cost

One forecast = 6 `BookCard` + 6 `BoxOffice.Night` calls. Budget in §7.

### 4.4 The test that keeps it honest

`TheForecastIsTheWorldMinusTheNoise`: build the default world; for each active rival, take
`Forecast` with `Optimism = 1.0` and the rival's real roster; then **actually run** the world for
`HorizonDays` with every rival's planning switched off (so rosters hold still) and compare:

- average show net within ±10% of `NetPerShow`,
- average rating within ±3 points of `Rating`,
- cash at the horizon within ±10% of revenue-over-horizon of `CashAtHorizon`.

Use `StableSeed`; this is a corpus test. If it fails, the forecast is wrong — fix the forecast.
Give the world a way to switch planning off for a test (`World.Tick(career, plan: false)` or an
internal flag) rather than duplicating the tick.

---

## 5. Objectives and dispositions

`WrestlingSim.Core/Models/World/Objective.cs`.

```csharp
/// What a promotion wants. Weights, each >= 0. Only ratios between them matter.
public sealed record Objective(
    double Wealth,      // money in the bank at the horizon
    double Franchise,   // the value of the next television deal
    double Stars,       // the drawing power of its cards
    double Quality,     // how good its shows are
    double Reputation,  // its name for good shows
    double Favour,      // its owner's favourites on top
    double Stability);  // not changing things

/// How a promotion sees the future. Not what it wants — how well it sees.
public sealed record Disposition(
    int    HorizonDays,      // how far ahead it looks
    double Optimism,         // multiplies the gate and merch it expects; 1.0 is sober
    int    RunwayFloorDays,  // how much money-time it wants in hand before it worries
    double RiskAversion);    // how hard it reacts once below that
```

### 5.1 Scoring a move

Every move is scored **against doing nothing**. Let `hold` be the outlook of the current roster
and `after` the outlook with the move applied. Each term is a relative change, so the terms are
comparable and the weights mean something:

| Term | Value |
|---|---|
| Wealth | `(after.CashAtHorizon − hold.CashAtHorizon) / (hold.RevenuePerShow × showsInHorizon)` |
| Franchise | `δ^after.YearsToRenewal × (after.RenewalMultiple − hold.RenewalMultiple)`, with `δ = 0.5` a year; 0 below television |
| Stars | `(after.CardPull − hold.CardPull) / BoxOffice.TypicalCardPullFor(tier)` |
| Quality | `(after.Rating − hold.Rating) / 50` |
| Reputation | `(after.ReputationAtHorizon − hold.ReputationAtHorizon) / 50` |
| Favour | `after.FavourShare − hold.FavourShare` |
| Stability | `−(1 + movesInLast90Days) / 10` for any move; 0 for holding |

Plus one term that is not weighted by the objective, because it is about staying alive rather
than about wanting something:

```
barrier(o)  = −RiskAversion × max(0, (RunwayFloorDays − o.RunwayDays) / RunwayFloorDays)²
Survival    = barrier(after) − barrier(hold)
```

So a move that buys runway when the promotion is in danger scores well, and a move that spends
runway it does not have scores badly — *in proportion to how much that promotion fears it*.

```
ΔU = Σ weightᵢ × termᵢ  +  Survival
```

Keep every term's value alongside the total (`Score` record: total plus the seven-plus-one
term contributions). The reason shown to the player (§9.2) is read off it.

`δ` (the discount on a renewal that is years away) is what should make spending rise as a
renewal approaches: the same audience gain is worth more when the deal it feeds is closer, and
final-year episodes already weigh double in `ProjectedMultiple`. **Whether it does is a thing to
measure (§11.3), not to assume.**

### 5.2 Starting values

These are **starting values to be tuned by measurement**, not findings. One table, in
`World.ObjectiveFor(FounderMotive)` and `World.DispositionFor(FounderMotive)` — the only place
`FounderMotive` is switched on outside `MotiveTraits`.

| Motive | Wealth | Franchise | Stars | Quality | Reputation | Favour | Stability |
|---|---|---|---|---|---|---|---|
| Business — wants a return | 1.0 | 0.6 | 0.1 | 0.1 | 0.2 | 0 | 0.1 |
| Platform — wants audience for a network | 0.5 | 1.0 | 0.4 | 0.2 | 0.3 | 0 | 0.3 |
| MarkMoney — wants to be in the wrestling business | 0.1 | 0.2 | 1.0 | 0.3 | 0.5 | 0 | 0.0 |
| Ideology — wants wrestling to be good | 0.3 | 0.1 | 0.1 | 1.0 | 0.6 | 0 | 0.2 |
| Legacy — wants it to carry on as it is | 0.5 | 0.3 | 0.2 | 0.3 | 0.4 | 0 | 1.0 |
| Vanity — wants the owner on top | 0.3 | 0.2 | 0.4 | 0.1 | 0.3 | 1.0 | 0.2 |

| Motive | HorizonDays | Optimism | RunwayFloorDays | RiskAversion |
|---|---|---|---|---|
| Business | 365 | 0.95 | 365 | 4 |
| Platform | 365 | 1.00 | 270 | 3 |
| MarkMoney | 120 | 1.25 | 45 | 1 |
| Ideology | 270 | 1.00 | 180 | 3 |
| Legacy | 365 | 1.00 | 365 | 4 |
| Vanity | 180 | 1.10 | 120 | 2 |

How the wealthy fan is *meant* to burn out under this design, so the executor can tell whether
it is happening for the right reason: it barely values cash, looks only four months ahead,
believes its cards sell a quarter more than they do, and does not worry until it has six weeks
of money left. So it signs stars against revenue that never arrives, and by the time the
barrier bites, three-year guarantees and ninety-day payouts mean cutting no longer saves it.
Nothing says "fold at month 29".

### 5.3 Favourites (for Vanity)

`RivalPromotion.Favourites` — a list of wrestler ids, set at `World.Open` to the most over
wrestler on its opening roster ("the owner") for a Vanity promotion, empty otherwise.
`FavourShare` in the outlook = the share of the cycle's shows whose top item features a
favourite. No Vanity promotion is in the default world; it is exercised by a constructed world
in tests (§11.4). Releasing a favourite is not forbidden — it simply costs `Favour`.

---

## 6. PR B — the planner

`WrestlingSim.Core/Engine/RivalPlanner.cs`.

### 6.1 Moves

```csharp
public abstract record Move;
public sealed record Hold : Move;
public sealed record SignMove(Wrestler Who, decimal Annual, int Years) : Move;
public sealed record ReleaseMove(Wrestler Who) : Move;
public sealed record BidMove(Wrestler Who, decimal Annual, int Years) : Move;    // the player's expiring wrestler
public sealed record AddRegular(Wrestler Who) : Move;                            // below television
public sealed record DropRegular(Wrestler Who) : Move;
```

Each move knows how to produce its hypothetical roster and one-off cost, and how to apply
itself for real. Applying reuses today's effects exactly: a release pays
`min(ReleasePayoutDays, days left) × DailyCost`, adds a `NonCompete`, writes news; a signing
adds a three-year guaranteed `Contract` at the ask; a bid adds a `Bid` and "about you" news.

**Term length is fixed at three years** for rival signings, as today. A planner that looks a
year ahead cannot see what a long contract risks, so it would always take the cheapest (longest)
term for the wrong reason. Written down in §12 as not built.

### 6.2 Generating candidates

Bound the work; the planner must be cheap (§7).

- **Sign** (television tiers): from `FreeAgents`, the 8 with the highest `EffectiveOverness`,
  the 6 with the highest ring skill, and the 6 with the highest overness per dollar of ask —
  the union, de-duplicated. Three shortlists so that every objective's best candidate is on
  the list; the objective, not the shortlist, chooses.
- **Release**: every guaranteed contract, but pre-filtered to the 10 with the lowest
  `EffectiveOverness / AnnualGuarantee` plus the 2 with the highest guarantee.
- **Bid**: each of the player's guaranteed contracts inside `TalentMarket.ExpiryWarningDays`
  that this rival has not already bid on.
- **AddRegular / DropRegular** (below television): the 10 best free agents by overness and by
  skill whose `PerDateAsk` the forecast can price; every current regular.
- **Hold**, always.

The shortlists are a performance device, not behaviour. If a measured run shows a promotion
missing an obviously good move because it was not shortlisted, widen the shortlist.

### 6.3 Reservation price

`decimal Reservation(Wrestler w)` = the highest annual guarantee at which signing `w` still
scores `ΔU >= 0`. Bisect on the annual between the ask and three times the ask, 6 steps.
Zero if signing at the ask already scores below zero.

- **A bid** for the player's wrestler is `ask + 0.5 × (reservation − ask)`, rounded to the
  thousand. No reservation above the ask, no bid. This replaces `BidPremium`: a promotion that
  values somebody more bids more, and how much it values them comes from its objective.
- **Keeping its own** at expiry (`SettleContracts`): re-sign at the ask if
  `reservation >= ask`. If the player has a lodged offer, the rival counters up to its
  reservation: the player wins only if `mine.Annual > reservation`. Otherwise let them go.

### 6.4 The weekly decision

On the rival's weekly business day (keep today's `(DayNumber + |StableSeed(Id)|) % 7 == 0`):

```
repeat up to MaxMovesPerWeek (3):
    hold    = Forecast(current roster)
    best    = the candidate with the highest ΔU          (ties: lowest wrestler id, ordinal)
    if best is Hold or ΔU(best) <= Epsilon (0.002): stop
    apply best; log it (§9.2)
```

`Epsilon` is friction: a move has to be worth making. It is global, not per motive — a
promotion's dislike of change is its `Stability` weight.

No randomness in the planner. Same world, same history; pin that
(`TheSameWorldMakesTheSameHistory`).

### 6.5 What is deleted

`World.Business`, `World.Wants`, `World.BudgetOf`, `MotiveTraits.BidPremium`,
`MotiveTraits.BuysStars`, and the budget arithmetic inside `SettleContracts`. If any of them
survives PR B, the PR is not finished.

### 6.6 PR B acceptance

- No `switch`/`if` on `FounderMotive` in `RivalPlanner.cs`, `RivalForecast.cs` or `BookCard`.
  Check with `grep -n "FounderMotive" WrestlingSim.Core/Engine/Rival*.cs` — the only hits allowed
  are in the objective/disposition/traits tables.
- The emergence tests in §11.3 and §11.4 pass, or the measurement that failed is reported to
  the owner with the model/objective analysis. **A failed emergence test is a finding to
  report, not a number to tune a rule around.**
- The existing player-facing tests in `RivalTests.cs` still pass. Three of them lean on today's
  rules and may need their *setup* changed (not their claim):
  - `ARivalBidsForAnExpiringStar_AndTakesThemIfThePlayerDoesNotMatch` and
    `MatchingTheBidKeepsThem` assume some rival bids for the player's most over wrestler within
    eight days. Under the planner a bid happens only if some rival's reservation is above the
    ask. If none is, construct the situation honestly (e.g. the star is clearly above the
    bidder's current headliners) and say so in the test's comment.
  - `APlayerOfferBeatsARivalsAndTheWrestlerArrivesAtExpiry` offers twice the ask; it now wins
    only if that exceeds the holder's reservation. Assert against the reservation instead of
    assuming.
- `CLAUDE.md` gains the rule from §0 and a line that the emergence tests are corpus tests.
- Player-visible (§9.2): reasons in the news, "what it wants" and the outlook on the Rivals
  screen.

---

## 7. Performance

The clock runs in WebAssembly in the browser, and "advance to next show" ticks many days at
once. Planning happens once a week per rival, so the cost that matters is one planning session.

Budget: **at most 150 forecasts per rival per week** (≈ 30 candidates × up to 3 rounds, plus
reservation bisections), each 6 `Night` calls. Enforce it with a counter, not a stopwatch —
timing tests are flaky:

- `World.ForecastsRun` (an internal counter) and a test `AWeeksPlanningStaysInsideItsBudget`.
- Memoise the `hold` forecast within a round, and reuse it as the next round's baseline only
  after re-forecasting once (the roster changed).
- If the budget is exceeded, shrink the shortlists in §6.2 — not `CycleShows`, which the
  forecast-accuracy test depends on.

Then measure wall-clock **once, in the browser**: advance a National career one full year and
record seconds-per-year in the design record. If a year takes more than about ten seconds,
report it before optimising; the likely levers are caching `BookCard`'s candidate pool between
forecasts and skipping candidates whose roster change cannot alter the top `size × 2` wrestlers.

---

## 8. PR C — booking as a decision

Smaller than A and B. `BookCard`'s score function becomes the objective's:

```
score(match) = Stars   × pull(match) / typicalPull
             + Quality × expectedMatchSkill(match) / 5
             + Wealth  × −fees(match) / typicalNightRevenue
             + Favour  × (match features a favourite ? 1 : 0)
```

so a business promotion books the pairing that draws per dollar, an ideology books the best
match, and a vanity books the owner. The forecast picks this up automatically (§4.2).

Below television, the same planner chooses the regulars (`AddRegular` / `DropRegular`), which
replaces the last rule in the old `Business` (take the best free agents under a price ceiling).
If PR B is already large, the regulars moves belong here.

Acceptance: `ABusinessBooksValue_AnIdeologyBooksTheBestMatch` — same roster, same ledger, two
objectives, different cards in the predicted direction; plus the §11 suite re-run and
re-recorded.

---

## 9. Saves and screens

### 9.1 Saves

Additive fields on `RivalDto` (`WrestlingSim.Core/Persistence/SaveGame.cs`), no version bump,
all optional so a save written before this work loads and behaves:

- `Pairings` — list of `{ A, B, Meetings, Last }`.
- `Favourites` — list of ids.
- `Objective`, `Disposition` — optional overrides; null means "the motive's default".
- `Decisions` — the decision log (below), capped.
- `RivalNightDto.Short`, `RivalNightDto.TopFreshness`.

Tests: round-trip (`ThePlannersStateSurvivesASave`), and a pre-planner save loading with empty
ledgers and default objectives.

### 9.2 What the player sees (CLAUDE.md: a feature the player cannot reach is not finished)

- **Reasons in the news.** Every applied move writes its news line with the dominant term of
  its score, in plain words: *"Vantage signed Seth Rollins — it expects a bigger audience into a
  renewal nine months out."* / *"Ironclad released Baron Corbin — it is running out of money."*
  Map each term to one clause; pick the term with the largest positive weighted contribution.
- **Decision log** on `RivalPromotion` (`Decisions`, capped at 20): date, move, wrestler, money,
  the reason clause, and the term contributions.
- **Rivals screen** (`WrestlingSim.Web/Screens/RivalsScreen.razor`), per rival:
  - *What it wants* — the objective as a short ranked list in words ("stars above all, then
    its name for good shows; cares little for cash"), derived from the weights, not hard-coded
    per motive.
  - *How it looks* — from the current `hold` outlook: money-time left ("about 14 months at this
    rate" / "profitable"), projected renewal ("would renew at ×1.12"), main-event freshness.
  - *Lately* — the last few decisions with their reasons.
- Money through `Money.Format`; percentages through `Money.Percent` (the WebAssembly build is
  globalization-invariant — a named culture throws, and `ToString("P0")` prints "93 %").

Verify in the browser the way the earlier slices were: seed a save from a temporary test that
writes `SaveSerializer.ToJson(career)` to the scratchpad, load it into `localStorage` under
`wrestlesim.save.<CareerId>` with a Playwright script, click through, screenshot. Delete the
temporary test. Kill the dev server (`pkill -f WrestlingSim.Web`).

---

## 10. What this plan deliberately leaves alone

- **`World.Open`.** Opening rosters still come from `TierOpeningRoster` with
  `MotiveTraits.OpeningBudget`. Letting the planner build opening rosters is the natural end of
  this work, but it moves every tier's opening roster, which moves
  `BoxOffice.TypicalCardPullFor` and every running cost solved from it. It is a re-calibration
  of the whole economy and gets its own decision from the owner — listed in §12.
- **The player's box office, contracts and screens.** Nothing in `Career.Projection`,
  `Career.Sign` (beyond how a rival's counter is priced), or the booking flow changes.
- **The economics tables** (`TierEconomics`, `TalentMarket`, `Television` constants).

---

## 11. How it is tested

### 11.1 Conventions (from `CLAUDE.md` — follow them)

- Corpus tests use `StableSeed.From(...)`, never `HashCode.Combine`.
- A measured constant that drifts is re-measured, never widened.
- Test names say the claim; the doc comment says which reference doc the claim is from.
- When a test's premise changes, say why in its comment (see
  `ARoadTheTierCanAffordFillsItsTownsAndPays_ARoadOfStarsDoesNot` for the pattern).
- The full suite takes about four and a half minutes; there is **no CI on pull requests** in
  this repo, so run `dotnet test` locally before every push and say that you did.

### 11.2 The probe

A temporary test (not committed) that builds the default world for a player at Local,
Established and Global, advances three years with no player input, and prints per rival every
half-year: cash, reputation, roster size, payroll, last rating, attendance, and at the end the
last dozen news lines. Use it after every change to the model or the weights. The numbers that
go into the design record come from it.

### 11.3 Emergence in the default world (new file `RivalEmergenceTests.cs`)

Each runs the default world for years with no player input and asserts a pattern the reference
predicts. None asserts a specific move.

| Test | Claim | Source |
|---|---|---|
| `TheWealthyFansPromotionBurnsOut_TheMajorDoesNot` (exists — must still pass, same band) | Ironclad folds in 18–40 months; Vantage does not and ends richer | doc 01 §7 |
| `AMediaCompanySpendsIntoItsRenewal` | Vantage's payroll (or card pull) averaged over the last 12 months of a TV term exceeds its average over the 12 before | doc 08 §3 |
| `NobodyHollowsOutTheirRoster` | at every half-year checkpoint, every active television rival can fill its card and holds a roster a real promotion of its tier would (doc 29 §12: 20–35 trackable characters on weekly TV, 6–10 a division) — **emerging from §2A, with no rule about size** | doc 29 §12; the owner's test case |
| `NobodyChurns` | no rival releases and re-signs the same wrestler within 180 days; each rival makes fewer than ~40 moves a year | the bug slice 4 caught by hand |
| `TheIndiesSurvive` | both non-television rivals are still running after three years | slice 4's measurement |
| `TheSameWorldMakesTheSameHistory` | two runs produce identical decision logs | determinism |

`AMediaCompanySpendsIntoItsRenewal` is the one most likely to fail honestly. If it does: check
`δ`, check that the Franchise term's contribution is visible in the score at all, and report the
measured ratio. Do not add a "spend before renewal" rule.

### 11.4 Same world, different objective (the test of the design)

`ObjectivesChangeBehaviourTests.cs`. Build one world, clone it, and change **only** the
objective/disposition of one television rival. Run both two years. Assert the direction, with
the measured figures in the output:

- MarkMoney's top six average more overness than Business's, and it has less cash.
- Ideology's roster averages more ring skill than Business's.
- Business ends with the most cash of the four.
- Legacy makes the fewest moves.
- Vanity's favourite headlines the majority of its shows.

If two objectives produce the same roster, the objective is not driving the behaviour and the
design has not been achieved, whatever else passes.

### 11.5 Unit tests for the parts

Scoring: each term's sign and scale on a hand-built pair of outlooks; the barrier is zero above
the floor and grows below it. Reservation: monotone in the wrestler's overness; zero when
signing at the ask is already a loss. `ProjectedMultiple` equals `RenewalMultiple` on the
explicitly extended deal. Candidate generation respects exclusivity, non-competes and the
player's lodged bids.

---

## 12. Not built by this plan — write these into the design record

- **Planner-built opening rosters** (§10).
- **Contract length as a decision.** Fixed at three years; a one-year horizon cannot price the
  risk of a long deal.
- **Looking more than one move ahead.** The planner is greedy: best single move, repeated. It
  cannot plan "release two to afford one".
- **Reacting to the player or to each other.** A rival does not model what the player will do,
  bid against another rival, or counter-programme a night.
- **Imperfect information.** Every promotion sees every wrestler's true numbers. Doc 10 §3 says
  charisma should be hidden until somebody is in front of a crowd.
- **Results and wear on rival shows** if §2A.4 was not needed to pass §2A.5 — say so, and why.
- **Other levers** a real promotion has: ticket prices, how often it runs, house-show loops,
  mid-contract buyouts, acquisitions.
- **Objectives the player can edit.** The data shape allows it; no screen does.

---

## 13. Order of work, and how to ship it

Four pull requests, each mergeable on its own and each leaving the game playable:

0. **PR 0 — the audience** (§2A). Changes the player's game too. Needs the owner's yes first.
1. **PR A — the honest model** (§3, §4). No decision logic changes.
2. **PR B — the planner** (§5, §6, §7, §9). The rules are deleted.
3. **PR C — booking and regulars as decisions** (§8).

For each: build → probe (§11.2) → tests → full suite → browser check → design record → push.
Update `docs/business-design-record.md` with a "Slice 5: rivals that plan" section per PR:
what it does, what was measured, what a measurement corrected, what is not built. Corrections
go inline next to the claim they correct.

Review: two rounds per PR at most, and stop when findings become test-adequacy notes
(`CLAUDE.md`).

### Practical notes that cost time last time

- Inside namespace `WrestlingSim.Models.World`, the class `World` must be written
  `Engine.World` — the bare name resolves to the namespace.
- `Math.Round(decimal, -3)` throws. Round to the thousand with
  `Math.Round(x / 1_000m) * 1_000m`.
- Pushing: the session may push only to its designated branch, and force-pushes are refused.
  After a squash-merge the remote branch holds pre-squash commits; restart it from `main` with
  `git merge -s ours origin/<branch>` on top of the new work, then an ordinary push.
- Do not open a pull request unless asked.

---

## 14. Decisions for the owner before PR B

These are defaults in this plan; each is the owner's to change.

1. **The weights and dispositions in §5.2.** They are the game's statement of what each kind of
   promotion wants. Read the table and change anything that does not match how you see them.
2. **How fast rivals may act:** `MaxMovesPerWeek = 3`. Higher is livelier and noisier.
3. **The audience proposals in §2A** — staleness from sameness (per wrestler, per audience
   group, and of the show itself), the whole card counting, rival titles, and
   (as a second step) results and wear on rival shows. The owner has ruled that a gutted roster
   is never acceptable and that the reason it fails in real life is what must stop it in the
   game; §2A is the proposal for how. It changes the player's shows as well. Confirm the four
   mechanisms, or change them, before PR 0 starts.
4. **Whether to take on planner-built openings** (§10) after PR C, with the re-calibration it
   costs.
