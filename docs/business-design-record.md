# The business layer — design record

*C5 in [31-sim-mapping.md](wrestling-reference/31-sim-mapping.md): rate well vs draw well as
separate outputs. First slice.*

Doc 18 §7 is the brief: **story sells the ticket; quality sells the next ticket.** Before this,
everything the engine produced was a quality axis — technical, storytelling, crowd, the star
rating that bands them. There was no money in the model at all; attendance was a constant per
tier stamped on a show when it was scheduled, and `Show.AudienceSize` was read by nothing.

## The decisions the booker made

Four forks were the player's, not the engine's, and were settled before anything was built:

| Question | Answer | Why it mattered |
|---|---|---|
| How talent costs money | **Per appearance** | Every career starts with the same 76 WWE wrestlers whatever the tier. Real guarantees would bankrupt every tier below Global on day one for a roster the player never chose. |
| What happens at negative cash | **Report, don't stop** | No game over until the numbers are calibrated against play. The dashboard says *in the red*. |
| TV money | **Flat fee now, renewal later** | Without a rights fee every TV tier bleeds on production costs. With only a flat fee, booking moves a small share of TV-tier money — which is true, and is why renewal is the next slice. |
| Units | **Real dollars** | The reference is written in them, so its figures are a check on ours. |

## What it does

### 1. The draw is read before the bell — `BoxOffice.Draw`

Each item on the card gets a **pull**, the product of four factors, each one a row of doc 18
§7's "draws well when" column:

| Factor | Reads | Range |
|---|---|---|
| Star power | 60% the most over person on the best side, 40% the second side's | 0–1 |
| Build | Feud heat, saturating at Nuclear, × the feud's `Credibility` | 1.0–1.5 |
| Freshness | `Feud.Familiarity` — doc 20 §9.1's rematch curve, which was already written as relative draw | 0.5–1.0, 1.0 for a blow-off |
| Stakes | Title prestige × 0.35, × 1.10 for any stipulation | 1.0–~1.5 |

A promo or angle pulls at half a match's star power, a number drawing at 0.4, a battle royal
at full star power × 1.10 for the spectacle.

The **card pull** is 60% the top item, 25% the second, 15% the third. The fifth match on a
card sells nobody a ticket, and the report says so.

**Nothing in the draw reads a result.** `Career.RunShow` sells the house before
`ShowSimulator` runs, because running a match moves the overness, heat and familiarity the
ticket was bought on. So the booking screen's projection is not an estimate: it is exactly
what the card will draw (`TheHouseIsSoldOnTheCardAsAdvertised_NotOnHowItWent`).

### 2. The house — `BoxOffice.Demand`

```
demand = capacity × TypicalFill × (cardPull / TypicalCardPull)^1.5 × reputationFactor
```

- **`TypicalCardPull` = 0.7561 is measured**, not chosen: the median card pull of 2,000 cards
  of five same-division singles matches drawn at random from the shipped roster with
  `StableSeed`. It moves when the roster does, and
  `ATypicalCardFillsTypically_AndThatIsMeasuredNotAsserted` fails when it has — the same
  contract as `MatchEngine.TypicalInvestment`. It shipped measured at 0.7624; the fourteen
  champions added in #90 moved it to 0.7561, and the test caught it on rebase.
  *Corrected by slice 3:* it is now one figure **per tier**, measured on that tier's opening
  roster, because prices no longer depend on the buyer — see "Slice 3" below.
- **`TypicalFill` = 0.70.** Doc 29 §7 puts break-even at a 50–60% house; a booker who books
  whoever is free should clear it, not by much.
- **Elasticity 1.5**, because draw is top-heavy: a card twice as strong as typical sells out.
- **Reputation factor** ×0.75 at 0, ×1.0 at 50, ×1.25 at 100.

Capacity is the existing `ScheduledShow.Attendance`, which was always the tier's building
size and is now treated as that. Demand above capacity is reported as *turned away*.

### 3. The night's books — `BoxOffice.Night`

| Line | Source |
|---|---|
| Gate | attendance × the tier's net ticket price (×1.6 premium event, ×0.8 house show), in whole dollars so the report's "attendance × price" adds up |
| Merchandise | attendance × per-head spend × (0.5 + top overness) × 0.55 net — doc 09 §3.4's concentration in the most over name |
| Television rights | flat per episode at Established and above; a premium event is in the deal at 2× |
| Talent | per appearance: the tier's base × 1 / 1.5 / 3 / 8 / 20 by card position. *Replaced in slice 3* by one market price for every buyer, and nothing extra for an appearance a guarantee covers |
| Building and crew | per show, by tier and show type |
| Production | per televised episode or premium event, at TV tiers only |
| The road | a run of towns as house shows, one a night, its cast paired off best against next-best for the poster; talent paid for the nights actually worked |

Every coefficient is in `TierEconomics` with the doc line it came from.

### 4. The office — `TierEconomics.DailyOverhead`

The first measurement of the tier tables put typical-card margins at 57–78% at the TV tiers,
against doc 09 §4's 30–40% for Tier 0 and roughly break-even below it. Per-appearance pay
leaves out offices, staff, insurance and the guarantees of everybody not booked tonight. That
is doc 09 §2's SG&A line, and it is charged **by the clock**, every day, show or no show —
not per show, because a promotion that stops running does not stop paying for its office.
*Corrected by slice 3:* the guarantees came out of it and are now their own line on the clock,
so it is running costs only, re-solved per tier.

### 5. Reputation — the next ticket

`CareerBooks.Reputation` (0–100, opens at 50) steps toward each show's overall rating by 10%
for a weekly episode, 20% for a premium event and 5% for a house show. It is what the next
card's demand is multiplied by. A night that is only a run of towns has no rating and moves
nothing.

The report's verdict reads the two outputs against each other, relative to the promotion's
own reputation rather than an absolute bar: *"A good show for a room that didn't come.
Quality sells the next ticket, not this one."*

## Measurements

Typical card at the tier's usual cadence, overhead included, 400 cards, opening reputation,
re-measured on the roster after #90
(`ATypicalCardLandsWhereTheReferencePutsTheTier`):

| Tier | Doc 09 §4 | Median margin | 10th–90th |
|---|---|---|---|
| Local | thin, often subsidised | 11.1% | −0.9% to 21.9% |
| Independent | thin | 21.2% | 8.8% to 29.0% |
| SuperIndie | real but small profit | 17.2% | 1.2% to 27.5% |
| Established | near break-even or modest | 10.5% | 8.1% to 12.6% |
| National | costs that can exceed revenue | 6.3% | 4.7% to 7.6% |
| Global | 30–40% in strong years | 35.1% | 34.4% to 35.7% |

*Superseded by slice 3:* these were measured with per-appearance pay scaled to the buyer and on
cards drawn from all ninety. The current figures, per tier on each tier's opening roster with
payroll included, are in the slice 3 table below.

The TV tiers' bands are narrow because the flat rights fee dominates their revenue. That is
the honest consequence of decision 3 and the reason renewal is next.

## What review of the premise found: the star rating already contains draw

The test meant to pin doc 18 §7's extreme case — *a five-star match between nobodies draws
nothing* — could not be written, because the engine cannot produce that match.

Measured over forty seeds on `TV Formula`, the star rating moves about as far with overness as
with ring skill:

| | skill 1.8 | skill 4.8 |
|---|---|---|
| overness 25 | 1.30★ | 2.21★ |
| overness 60 | — | 2.81★ |
| overness 92 | 2.46★ | 3.47★ |

Going from unknown to star at fixed skill is worth ≈1.2★; going from the worst workers to the
best at fixed overness is worth ≈1.0★. The crowd component reads investment, investment reads
overness, and the result is that **the best possible workers who nobody knows rate below two
stars who can barely work** — 2.21★ against 2.59★ once the stars have a hot feud.

That is partly right: a hot crowd makes a match better, and doc 16 says so. But it means the
divergence C5 exists to model is capped from the rating side. The box office separates the
outputs cleanly (the stars draw 5.6× the workers); the rating does not separate them much.
What is pinned is what holds:
`DrawAndRatingAreDifferentOutputs_TheDrawGapDwarfsTheRatingGap` asserts the draw ratio is
above 4× while the rating ratio stays under 1.3×.

**Not changed in this slice**, because the crowd component's dependence on investment is
calibrated across a large part of the existing suite, and re-weighting it is its own piece of
work with its own review. Logged as a follow-up.

## Slice 2: the television deal and its renewal

*Doc 08. Built after the first slice; it replaces the flat fee.*

At Established and above the rights fee is most of the money, and in the first slice it was
flat: booking moved the gate and the merch and nothing else. Doc 08 §3 calls the renewal the
most important 18 months in a promotion's life, and that is what was missing.

- **`TvDeal`** on the career: a fee per episode fixed for a term — three years at Established,
  four at National, five at Global (doc 08 §2.1's three to five). Opened with the books.
- **Viewers per televised episode and premium event**, with the same two halves as the gate:
  - *Tune-in* from the card as advertised, before the bell, scaled by reputation. Less
    elastic than tickets (exponent 0.8 against 1.5): a television audience is a habit.
  - *Retention* from the show's rating: 0.92 at 50, 1.04 at 80, 0.80 at 20. A bad episode
    loses its audience across the night — doc 08 §4.2's quarter-hours, reduced to one figure.
  
  So at a television tier **quality reaches money directly** for the first time, not only
  through reputation.
- **Baselines** are the typical card's average audience: 2.0M Global (doc 29 §8: Raw 1.3–1.8M,
  SmackDown 2.0–2.5M), 800k National (Dynamite 600–900k), 250k Established (no reference row).
- **Renewal on the clock at expiry.** The next fee is this one ×
  `1.10 × audienceIndex^1.2 × consistency`, clamped to 0.5–2.0, rounded to the thousand:
  - *audience index* — the term's viewers against the baseline, **final year weighted double**
    (doc 08 §3's T−12 months);
  - *consistency* — up to a fifth off for a swinging audience, measured as the mean
    week-to-week change against the mean audience (doc 08 §2.2: the network buys a number it
    can sell against). Review corrected the first version, which used the coefficient of
    variation and so charged a steadily *growing* audience as a swinging one;
  - *1.10* — the market's drift; doc 08 §1.1's rights fees rising while ratings fell.
  
  Holding the baseline renews at ×1.10; 1.4× the baseline at ×1.65; 0.7× at ×0.72.
- **On screen:** the booking screen shows tune-in; the report shows how many watched and what
  share the show kept; a dashboard tile shows the fee, days left, a *final year — counts
  double* badge, and **what the deal would renew at today** — the number the booking is
  actually moving.

Found while building it: `Math.Round(decimal, -3)` throws — decimal rounding takes no negative
digits — so the first version crashed at the first renewal of every career. The clock test
caught it; the fee now rounds by dividing.

### Not built in slice 2

- **The network walking away.** The floor is half the fee; a promotion is never dropped.
- **Negotiation.** Renewal is automatic. Doc 08 §3's stunts — signings, returns — have nothing
  to bid against, and there is no rival network.
- **The network's other wants** (doc 08 §2.2): demographics, brand safety, specific stars.
- **Inventory obligations.** Nothing makes a promotion produce the episodes it signed for; a
  term with none renews at the floor, which is the only cost.
- **Quarter-hour ratings per segment.** Retention is one figure per episode.
- ~~**Premium events in the ratings.**~~ *Corrected in review:* premium events are rated
  like episodes. Unrated, a promotion running only premium events renewed at the floor every
  term and its fee halved each time.

## Slice 3: contracts, the market, and the opening roster

*Docs 10, 11 and 13.* Until this slice every career owned all ninety shipped wrestlers, paid
each one per appearance at a rate scaled to the buyer's tier, and the daily office cost hid
everybody else's guarantees.

### The decisions the booker made

| Question | Answer |
|---|---|
| How small promotions work | **An open per-date market.** Doc 11 §2.4: below television nobody signs; everybody works by the night. |
| Does price depend on the buyer | **No.** A star costs what a star costs; a small promotion cannot afford one on a guarantee. |
| What a career opens with | **A source.** The default is sized and budgeted for the tier; a custom database (WWE opens with WWE's people) will be another `IOpeningRoster`. |

### What it does

- **`TalentMarket.PerDateAsk`** — one price per wrestler, exponential in effective overness:
  ~$100 a night at 25, $800 at 50, $4k at 70, $13k at 85, $30k at 95. Momentum moves it, so
  a hot run raises the price (doc 11 §7.1).
- **Guarantees** (television tiers only): ~120 nights of the asking price a year, less for a
  longer term (doc 10 §6.1). A top star asks ~$3.6M, a mid-carder ~$470k — inside doc 29 §4.1.
  Paid daily by the clock; appearances cost nothing extra. The rate is fixed for the term, so
  somebody who gets hotter is a bargain until it runs out, and the Contracts screen says so.
- **Regulars** (below television): no contract and no cost until booked. Anybody else is
  bookable too, at their price.
- **Tolling:** an injured wrestler's deal extends a day for every day out (doc 11 §2.1).
- **Expiry:** a deal that runs out goes to the market — still bookable by the night. Warned
  sixty days ahead on the dashboard and the Contracts screen.
- **Release:** pays out ninety days of the guarantee (doc 13 §3.2's non-compete); dropping a
  regular costs nothing (doc 13 §4: at the bottom of the business you just stop being booked).
- **`TierOpeningRoster`:** at television tiers, guaranteed deals inside a payroll budget (doc 09
  §2's 25–35% of revenue), the budget spread so each pick takes at most three times its fair
  share of what is left — a few stars and a full card under them — divisions taken alternately,
  terms staggered one to four years. Below television, the best regulars whose nightly price is
  under the tier's indie ceiling (doc 29 §4.2). Quality is not chosen; it falls out of the
  budget: average overness 30 at Local, 54 at Super Indie, 61 at Global (everyone).
- **Old saves** open their roster from the source on load. Nobody leaves the world — everybody
  else is on the market — but the bill is no longer all ninety.

### Re-calibration

Because prices no longer scale with the buyer, a Local promotion can afford the least over end
of a major-league roster, and its audience is the audience for that card. One typical-card
figure measured over all ninety put every indie card at a third of a house, so the figure is
now **per tier**, measured on the tier's opening roster:

| Tier | Opening roster | Payroll a year | Typical card pull | Daily running cost | Median margin |
|---|---|---|---|---|---|
| Local | 11 regulars | — | 0.3312 | $25 | 9.3% |
| Independent | 16 regulars | — | 0.4451 | $245 | 11.6% |
| SuperIndie | 22 regulars | — | 0.5757 | $1,650 | 15.3% |
| Established | 40 signed | $6.9M | 0.6186 | $14,300 | 10.0% |
| National | 65 signed | $29.9M | 0.7237 | $217,000 | 3.2% |
| Global | 90 signed | $61.1M | 0.7562 | $485,000 | 35.0% |

Running costs were solved from the measured median night for each tier's target in doc 09 §4,
then confirmed by `ATypicalCardLandsWhereTheReferencePutsTheTier`.

One test's premise changed rather than broke: a road of upper-card stars at a Super Indie
used to pay. At one market price they ask ~$10k a night each, so the same towns now sell more
tickets and lose money, and the test pins both halves.

### Not built in slice 3

- **Rival promotions** — and so no competing offers (doc 11 §7.1's only real leverage), no
  poaching, no non-compete that matters, and no market-wide wage effect (doc 11 §8). Without a
  rival, a wrestler whose deal runs out goes to a market that only you buy from, and nobody holds
  out for more than their asking price.
- **Availability.** A free agent is always available on any date. In reality an unsigned name
  is booked elsewhere some nights, which is half of what a guarantee buys (doc 11 §6.1:
  "planning").
- **Morale** weighted as doc 11 §9 ranks it — creative use and honesty above money — and so no
  cost to warehousing beyond the salary and the existing off-screen decay.
- **Release waves and budget pressure** (doc 13 §2.1), **merch royalties**, **creative-control
  clauses**, **scouting with hidden charisma**, **developmental**.
- **Loading a custom database.** The seam exists (`IOpeningRoster`, plain-data contracts); the
  loader does not.

## Slice 4: competing promotions

*Docs 01, 10 §2.6, 11 §7–8, 13 §3.2, 26 §3.2, 28.* Until this slice the player was alone in the
world: nobody bid for anybody, nobody else's roster existed, and a guarantee bought nothing a
free agent's availability did not.

### The decisions the booker made

| Question | Answer |
|---|---|
| The default world | **A fixed ecosystem**: a global major, a national challenger, a super-indie and a regional indie — fictional names, the player taking the place of the one at their tier. |
| Who opens with whom | **Top-down by budget**: the biggest promotion signs first from the shared ninety, then the next. |
| How rivals' shows run | **Abstractly**: no beats, but the same box office, prices and TV rules as the player's. |

### What it does

- **`RivalPromotion`** — a tier, a founder motive (doc 01 §7), books, a TV deal at TV tiers, a
  roster on contracts, a show schedule on the tier's cadence, and a history of nights.
  The four defaults: Vantage Pro Wrestling (Global, a media company's), Ironclad Wrestling
  Federation (National, a wealthy fan's money), Rust Belt Pro (Super Indie, ideology), Harbor
  City Wrestling (Independent, business).
- **Motives are behaviour, not flavour** (`World.Profile`): budget, running costs, a rating
  bias for a coherent product, the premium over asking it bids, and whether it buys stars or
  value.
- **The rivals' day** (`World.Tick`): running costs and guarantees paid; deals tolled, re-signed
  or let go; TV renewed; a show on the cadence — the top of its roster paired off, sold through
  `BoxOffice.Night`, rated from skill, overness, motive and a seeded night's variance, its viewers
  counted for its own renewal; and once a week, talent business — budget releases when short of
  money, bids for the player's expiring stars, signings while flush.
- **Exclusivity.** A rival's guaranteed wrestler cannot work for the player; a card holding one
  will not run, and says why. Regulars are nobody's exclusive.
- **Non-competes** (doc 13 §3.2): every guaranteed release, the player's or a rival's, keeps
  that wrestler out of the business for ninety days. A closed promotion has none to enforce.
- **Competing offers** (doc 10 §6.2): in a deal's last sixty days a rival may bid. Re-signing
  means matching the bid; not matching it means losing them to the bidder at expiry, with any
  belt vacated on the way out. The player can lodge offers for a rival's wrestlers in the same
  window, and wins them if the offer beats what the rival will pay to keep them.
- **Competition moves wages and audiences** (doc 11 §8, doc 26 §3.2): every price and every
  crowd and television audience is multiplied by the count of credible employers — promotions at
  National or above still running, the player's included — against a baseline of two (WWE and
  AEW, when every figure in this game was taken). A third bidder is +12% on wages and +8% on
  audiences; a monopoly is the reverse. A career with no world modelled reads 1.0, not monopoly.
- **Folding**: a rival whose cash falls below minus half its opening cash closes, its roster to
  the market at once.

### What it measured

Three years of the default world, run with no player input:

- **Ironclad, the wealthy fan's National promotion, folds after about 29 months** in every
  player-tier scenario — inside doc 01 §7's "typically 18–36 months". Nothing scripts it; it is
  the motive's spending against the tier's income (`TheWealthyFansPromotionBurnsOut_TheMajorDoesNot`).
- **Vantage, the global major, runs at about 35%** — doc 09 §4's strong-year Tier 0 margin —
  and roughly triples its cash.
- **The indies survive** on the pool the television promotions leave unsigned.
- **When Ironclad closes, wages fall to ×0.88 and audiences to ×0.92** for everybody, the
  player included: winning the war quietly costs the winner (doc 26, sim implications).

Two things this run caught before they shipped:

- **The television promotions signed all ninety** at the slice 3 sizes (45/30/20 between them
  and the player), and both indies closed within months with nobody to book. The television
  opening rosters are now 40/25/15, which leaves a pool nobody holds exclusively.
- **A broke rival churned names**: it released somebody on Sunday and signed them again on
  Monday. Rivals no longer sign while their cash is under a tenth of what they opened with.

### Re-calibration

Opening rosters now come from the shared world, so each tier's typical card was re-measured on
the roster that tier opens with in it, and the running costs re-solved:

| Tier | Opening roster | Payroll a year | Typical card pull | Daily running cost | Median margin |
|---|---|---|---|---|---|
| Local | 11 regulars | — | 0.3312 | $25 | 9.3% |
| Independent | 16 regulars | — | 0.4165 | $270 | 12.6% |
| SuperIndie | 22 regulars | — | 0.4508 | $1,940 | 14.9% |
| Established | 15 signed | $0.8M | 0.4934 | $30,800 | 10.0% |
| National | 25 signed | $3.6M | 0.6040 | $289,200 | 3.0% |
| Global | 40 signed | $56.7M | 0.8699 | $497,700 | 35.0% |

*Correcting slice 3's table:* those rosters and figures were measured with the player alone in
the world and are superseded by these.

The consequence worth saying plainly: **ninety wrestlers do not go far.** A National player opens
with the forty-first to sixty-fifth most over people on the roster, because a global major signed
the forty above them; an Established player opens with what both left. That is the industry's
shape — the major holds the stars — and it is also a roster problem a custom database solves by
having more wrestlers in it.

### Not built in slice 4

- **Head-to-head nights.** Two televised shows on the same night do not split an audience.
- **Rivals' storylines.** Their shows are abstract: nobody's overness, momentum or feuds move
  from working for a rival; only being on screen is recorded.
- **Poaching mid-contract, buyouts, and acquisitions** (doc 27's Invasion pattern).
- **Imitation** (doc 27's TNA pattern: copy the leader, buy veterans, fail visibly).
- **Release waves timed to events** (doc 13 §3.1) — releases are budget-driven only.
- **Released-talent heat** on debut elsewhere (doc 13 §6.1).
- **New promotions forming** after one folds; the world only shrinks.
- **Era settings** — the baseline of two credible employers is the mid-2020s, not a parameter.
- **A custom database** naming the promotions and who holds whom.

## Slice 5: the audience — sameness, the whole card, and who is watching

*Docs 16 §4.10, 17 §3–4, 23 §2–3, 29 §6. Plan: `docs/rival-ai-plan.md` §2A. This is the first
half (the player's shows and the shared model); rivals' real cards, titles and audiences are the
second half and are not built.*

The owner's test case started it: a national promotion on six wrestlers. The game allowed it and
charged only fatigue. The ruling was that whatever stops it in real life must stop it here, and
that nothing already built was off limits.

### What research changed

The first design counted appearances ("exposure"). Research overturned it before it was built —
liking for a character *rises* with exposure, advertising barely wears out from repetition, and
boredom follows low novelty; the wrestling cases agree (constant screen time with a moving story
holds; the same act for years does not). The sources and the full argument are in the plan,
§2A.0. **The model is sameness, not screen time**, and
`BeingOnEveryWeekIsNotTheProblem_DoingTheSameThingIs` is the test that keeps it so: two
wrestlers on television every week for six months, one against a rotation of opponents
(sameness 5.7), one in the same match (63.7, response ×0.80).

### What it does

Three separate things, plus the card:

- **A wrestler's act goes stale when nothing about it changes** (`Audience.Acts`,
  `AudienceModel`). An appearance adds sameness in proportion to how little was new, and takes
  it off in proportion to how much was: a first-time or long-rested opponent, a different kind of
  match, a new gimmick, a feud that escalated, a title that changed hands. Off screen it halves
  in ten weeks.
- **The rate is the wrestler's own** (`AudienceModel.Rate`): charisma 5 wears at 0.6×, charisma
  0 at 1.5×; `Gimmick.Durability` — in the roster data all along, read by nothing — makes a
  short-term gimmick 1.3× and a long-term one 0.7×; a high-flyer 1.15× (doc 17 §3.4, athletic
  novelty). Measured: a low-charisma short-term high-flyer in the same match loses 5% of his
  reaction in ten weeks; a 4.8-charisma long-term act never does in forty.
- **Each part of the audience feels it differently** (`Taste`, `Mix`, `Response`). A new
  `FanGroup.Regular` — the weekly viewer, reading the roster's Casual appeal, so no data
  changed — tires first of sameness; casual fans barely tire; hardcore fans tire soonest and
  also tire of a winning streak past five (doc 17 §3.11). Television leans regular, premium
  events casual, indies hardcore. A wrestler's response is each group's, weighted by how much
  of the room it is and how much it likes them — so one act is hot with one crowd and stale
  with another. The roster and booking rail say which: *"9 wins in a row — the hardcore fans are
  tiring of it; everybody else is fine."*
- **The show itself goes stale** (`Audience.ShowSameness`): how many of tonight's faces were on
  the recent shows, and how alike the card's make-up is, recent shows counting most. Up to 20%
  off the overall rating; halves in four weeks, so a promotion that runs rarely never builds it.
- **The whole card counts.** A card that leaves the slot under-filled sells fewer tickets and
  draws fewer viewers (`CardDepth`, ×0.5–1.0, read before the bell like everything that sells a
  ticket) and is a worse show (up to 30% off). Full credit at half the runtime filled — which is
  what the game's own `TypicalCardSizeFor` × a television-length match comes to.

Where it reaches: star power in the box office (so the projection shows it while the card can
still change), each item's score in the show, momentum cooling for an act past 50, and through
the rating into reputation and television retention.

### What it measured

`SixWrestlersCannotCarryANationalPromotion_ADeepRosterCan` — the six most over wrestlers in the
game on weekly national television, booked as well as six can be, against thirty booked the same
way:

| Week | Six: house | Six: viewers | Six: rating | Thirty: house | Thirty: viewers |
|---|---|---|---|---|---|
| 1 | 100% | 1.18M | 66 | 100% | 1.23M |
| 4 | 92% | 0.90M | 47 | 100% | 1.22M |
| 7 | 61% | 0.69M | 38 | 100% | 1.22M |
| 12 | 52% | 0.63M | 38 | 100% | 1.16M |

(Table from the first measured run, with full credit at 60% of the slot; the shipped threshold is
50% and the test asserts the house is below 80% between weeks 2 and 8 — it measures week 6.)
The house is gone in about six weeks. No line of code mentions roster size.

`TheSameSixLastFarLongerAtALocalPromotion` — the same six every 45 days hold 90% or better for
six shows (270 days). **They do not hold for ever:** with everybody working everybody every six
weeks, match-ups never get the sixty idle days they need to be forgotten, and the house slides in
the second half of the year. That is the existing `Feud` freshness model at a local cadence, not
anything added here; whether a local audience should be more forgiving is the owner's call.

**An arithmetic claim of mine this corrected:** the plan said six people "have spent every match
they can offer in about a month". With two matches a night it is nine weeks; it is a month only
because filling two hours makes everybody work twice. The collapse comes from both.

### A bug this found in slice 1

`BoxOffice` looked for the story behind a match among *live* feuds only. A rematch between two
people with no heat between them is not a live feud, so it was sold as a first meeting however
many times it had run. Fixed in `BoxOffice.StoryBetween` (dormant feuds count); pinned by
`ARematchWithNoHeatIsNotSoldAsAFirstMeeting` — sixth meeting, ×0.50.

### Re-calibration

A typical card now has to be a full card, so the calibration corpus books `TypicalCardSizeFor`
matches in `DefaultRuntimeFor` minutes instead of five in a default slot. More random matches
means a stronger top three:

| Tier | Typical card pull (was → is) | Median margin |
|---|---|---|
| Local | 0.3312 → 0.3379 | 4.0% |
| Independent | 0.4165 → 0.4236 | 9.7% |
| SuperIndie | 0.4508 → 0.4667 | 13.3% |
| Established | 0.4934 (unchanged) | 10.0% |
| National | 0.6040 → 0.6161 | 3.0% |
| Global | 0.8699 → 0.8842 | 35.0% |

All inside doc 09 §4's bands; running costs were left alone. Rivals book a tier-sized card in a
tier-sized slot for the same reason. `TheWealthyFansPromotionBurnsOut_TheMajorDoesNot` still
passes its 18–40 month band.

### The second half: a rival's show is a real show

*Plan §2A.2a, §2A.3. Built after the first half.*

Rival shows were five plain singles pairings — a shortcut taken under the word "abstract", which
the owner ruled out. Now:

- **A rival's card is made of what the player books** (`World.BookCard`): title matches, a tag
  one week and a three-way now and then, a promo or confrontation on television, and singles
  until half the slot is filled — each pairing chosen by what it would draw *as that rival's
  audience now sees those people and that match-up*. If the roster runs out before the slot is
  filled, people work twice. "Abstract" now means one thing: matches are not worked beat by beat;
  an item's rating comes from who is in it and what kind of item it is
  (`World.ExpectedItemRating`).
- **Its own audience** (`RivalPromotion.Audience`): the same sameness model, per promotion. A
  wrestler stale to a rival's crowd is fresh to the player's, so a signing arrives fresh.
- **Its own match-up memory** (`RivalPromotion.Feuds`, an ordinary `FeudBook`), so the box office
  reads a rival's rematch exactly as it reads the player's.
- **Its own belts** (`RivalPromotion.Titles`), seeded with the default slate and run on the
  existing `TitleEconomy`: vacant belts are filled on any night, champions defend on the big
  night against the freshest draw in the division, title stakes feed the draw, and a wrestler who
  leaves vacates.
- The Rivals screen lists each rival's champions with prestige and its last card item by item.

**Two things the three-year probe corrected:**

- *Premium events were every fourth show.* At a show every three days that is one every twelve
  days and thirty title defences a year; with upsets a belt changed hands so often that the
  global major's world title fell from 57 prestige to 0 in eighteen months. The big night is now
  about monthly (doc 29 §1: 12–16 major events a year), and a champion holds 85% of defences.
  After a year: 82 / 74 / 87 prestige, two to four reigns each.
- *The wealthy fan stopped folding on time.* Fewer premium events means less production cost,
  and Ironclad lasted 38 months — past doc 01 §7's 18–36. Its running-cost trait was re-solved
  from 1.2× to 1.3×: **22 months**. This is sensitive (1.4× gives 15), and it is the last time a
  trait should be solved to hit this band: under the planner the burn-out is supposed to come
  from the objective.

  **Withdrawn — the owner ruled this out, and was right.** Solving a constant until a promotion
  folds on schedule is scripting its fate indirectly; a breach of the reference's range is a
  thing to look at, not to tune away. The trait is back at 1.2×, the test no longer asserts any
  fate (`WhatBecomesOfTheWealthyFansPromotion_IsMeasuredNotForced`), and looking at it found the
  real story — see "The schedule, and what the fold really was" below.

**Measured** (`RivalShowTests`): a national rival cut to its six most over loses its house —
75% to 19% in twelve weeks, rating 53 to 31. A rival with two wrestlers a division draws 8% on
its big nights after a year, against 64% with a full roster.

**The finding worth the owner's attention: the top tiers are under-rostered for their
schedule.** *(Corrected below: the schedule was the bug.)* A national rival with its *full* roster of 25 also slides, 79% to 60% in twelve
weeks, and the global major with 40 runs at about two-thirds of a house. They run a show every
four and every three days, so each star works every other show. That is the six-wrestler
argument at scale, and it is honest: ninety wrestlers shared between five promotions cannot
staff a major's calendar. The remedies are real ones — a bigger database, a brand split (which
the player has and rivals do not), or a slower schedule — not a tweak to the rates.
The calibration constants (`TypicalCardPullFor`) describe a *fresh* typical card; steady-state
houses now sit below `TypicalFill` at the television tiers for this reason.

### The schedule, and what the fold really was

`Promotion.TypicalShowIntervalDays` had National at a show every four days and Global at every
three. The owner's correction: one brand runs one television show a week at every level; a show
every three days is what a company with a brand split does, across two rosters. Both are now
weekly. The daily running cost of those two tiers was re-solved for a weekly calendar against
the same doc 09 §4 margins — National $289,200 → $161,000 a day (3.0%), Global $497,700 →
$124,800 (35.0%). That is calibrating the economy to the reference, not steering an outcome.

What weekly did:

- A national rival's *full* roster now holds its house — 79% to 72% over twelve weeks, where it
  slid to 60% — and its big nights draw 81% after a year. The under-rostering was mostly the
  schedule. Six still cannot carry it: 75% to 22%.
- **The wealthy fan's promotion does not fold.** Five years, measured: cash 25M → 33M → 36.5M
  → 38.6M → 39.9M, then down to 12.6M in the fifth year.

Looking at why, as the ruling asks:

1. *It was never overspending.* Its "wealthy fan" budget is 1.5× the tier's, but the global
   major signs first and holds every star; Ironclad opens with the 41st–65th most over
   wrestlers on $3.6M a year against a $45M budget, and the rule-based AI can only sign free
   agents, of whom none are stars. A wealthy fan with nobody to overpay is a cheaply-run
   promotion.
2. *So the earlier folds were not the motive at all.* At 29 months, and at 22, it was the
   running cost — set for a show every four days — against a thin house. The burn-out that
   was celebrated as emergent in slice 4 was an artefact of the schedule and a dial.
3. *The fifth-year drop is the one emergent thing here:* its television deal comes up after
   four years of a slowly declining audience (reputation 48.6 → 43.5), renews lower, and the
   books turn. Nothing scripts that.

What this says about the design: doc 01 §7's burn-out is about a promotion that *can* buy
names and does. To see it, the wealthy fan needs stars it can reach — poaching, a deeper
database — and a planner that values them (plan §5). Until then the reference's 18–36 months
is simply not reproduced, and that is recorded rather than forced.

### Not built in this slice

- **Results and wear on rival shows** (plan §2A.4): working for a rival still moves nobody's
  overness or momentum and costs no fatigue; only being on screen, the match-up memory and the
  audience's patience are recorded.
- **Choosing the card's make-up.** The recipe is fixed (tag on even shows, three-way every
  third, one talking segment). That choice belongs to the planner (plan §8).
- **Stipulations and standing tag teams for rivals**; tag belts are in nobody's default slate.
- **A brand split for rivals**, which is what a real major does about the schedule.
- **Rejection of a push** (the Reigns case) — its own slice, agreed with the owner.
- **Gimmick depth beyond `Durability`**: doc 31 §3's *Motivation* and *Fit* are still missing.
- **A turn** as something new: there is no alignment-change mechanic to read.
- **Segments move an act only a little** (a flat 0.15): what a promo *did* is not read.
- **Per-group satisfaction and revenue lines** (B3 proper). Groups shape response and nothing
  else.
- **The roster table on desktop** does not show the staleness reading; the roster cards (phone)
  and the booking rail (desktop) do.
- **Every rate here is a design estimate.** Nobody has measured these for wrestling.

## Not built, and written down

- ~~**TV renewal and viewership.**~~ Built in slice 2, above.
- ~~**Contracts and signings.**~~ Built in slice 3, above.
- ~~**Rival promotions.**~~ Built in slice 4, above.
- **Consequences of going broke.** Cash goes negative and is reported. Owner funding, or an
  end, comes once the numbers have been played against.
- **Markets (B5).** Every town on a road run is the same town; the fifth night sells what the
  first did.
- **Merch per performer.** Merch follows the most over name on the card, as a total. Doc 09
  §3.4's point — that merch is the least deniable evidence of who is over — needs it per person.
- **Sponsorship, licensing, PPV/streaming for non-TV tiers, travel.**
- **Exhibition has no money.** It has no promotion to bank anything, so the box office does not
  appear there.
- **The star rating's overness dependence**, above.
