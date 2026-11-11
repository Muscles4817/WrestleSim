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
| Talent | per appearance: the tier's base × 1 / 1.5 / 3 / 8 / 20 by card position |
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

## Not built, and written down

- **TV renewal and viewership.** The rights fee is flat, so at Established and above booking
  moves only the gate and merch. A viewership output that matters at renewal is what makes
  booking matter there.
- **Contracts and signings.** Per-appearance pay undercounts a TV promotion's payroll; the
  daily overhead stands in for the guarantees. The oversized-roster trap (doc 09 §2.2) needs a
  roster the player chooses.
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
