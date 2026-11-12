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

## Not built, and written down

- ~~**TV renewal and viewership.**~~ Built in slice 2, above.
- ~~**Contracts and signings.**~~ Built in slice 3, above.
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
