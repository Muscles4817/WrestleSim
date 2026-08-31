# The Wrestling Operations Reference

A top-to-bottom description of **how the professional wrestling business actually works** —
how promotions are run, who does what, what makes them good or bad at it, who gets hired
and fired and when, how many shows get run and why, what audiences respond to, why people
get hot and cold, why brand splits work or fail, and where a company stops succeeding on
merit and starts coasting on cultural inertia.

## Why this exists

WrestleSim is a simulation of a real industry. Every time a design decision comes up, the
question "how does this work in real life?" is the first question worth answering, and the
answer is usually more specific and more interesting than intuition suggests. This directory
is the standing answer, so that neither a human nor an AI session has to go re-derive it.

**When someone says "X should work like real life," this is what they mean by real life.**

## How to read it

Each document answers four questions about its subject, in roughly this order:

- **What** — what the thing actually is, mechanically
- **How** — how it operates day to day, the process
- **Why** — the incentives that produce that behaviour
- **When** — the timing, cadence, and cycles

Most documents end with a **Sim implications** section: what this means for WrestleSim's
models, what the engine already captures, and what it doesn't. Those sections are opinions
about design; everything above them is description of the industry.

## Ground rules for this reference

1. **Descriptive, not aspirational.** This describes what promotions do, including the
   things they do badly and repeatedly. "This is dumb and they keep doing it" is a
   legitimate and important entry.
2. **Ranges, not single numbers.** The business is wildly heterogeneous. A number given as
   "typical" is a central tendency for a tier, not a law.
3. **Era-tagged.** Wrestling economics changed fundamentally in 1984, 1997, 2001, 2014 and
   2019. Any claim about money, schedule, or media has an era attached.
4. **Confidence flagged.** Public financials (WWE/TKO as a public company, AEW through
   Warner disclosures, NJPW through Bushiroad) are solid. Private-company numbers,
   backstage motive, and anything sourced to reporters are flagged as such.

---

## The documents

### Part I — The shape of the industry
| # | Document | Covers |
|---|---|---|
| 01 | [The industry map](01-industry-map.md) | Tiers of promotion, what separates them, the global landscape |
| 02 | [Anatomy of a promotion](02-promotion-anatomy.md) | Ownership models, org chart, departments, what a company physically is |
| 03 | [Roles and competence](03-roles-and-competence.md) | Every job, what it does, and the several distinct ways to be good or bad at it |

### Part II — Running the company
| # | Document | Covers |
|---|---|---|
| 04 | [Booking philosophy](04-booking-philosophy.md) | Schools of booking, long-term vs episodic, the actual craft of the job |
| 05 | [The creative process](05-creative-process.md) | How a week of television is made, pitch to air, the writers' room, the day of show |
| 06 | [Schedule and cadence](06-schedule-and-cadence.md) | How many shows, how often, by promotion size; the annual calendar |
| 07 | [Live events and touring](07-live-events-and-touring.md) | Routing, markets, buildings, attendance, the loop economy |
| 08 | [Television and media rights](08-television-and-media.md) | The TV business, ratings, negotiation cycles, streaming |
| 09 | [Revenue and cost model](09-revenue-and-costs.md) | Where money comes from and goes, by tier |

### Part III — People
| # | Document | Covers |
|---|---|---|
| 10 | [Talent acquisition](10-talent-acquisition.md) | Scouting, tryouts, developmental, who decides, how they decide, when they hire |
| 11 | [Contracts and talent money](11-contracts-and-talent-money.md) | Deal structures, downsides, per-date, merch splits, leverage |
| 12 | [Pushes and positioning](12-pushes-and-positioning.md) | How the card hierarchy actually forms and moves |
| 13 | [Releases and firings](13-releases-and-firings.md) | Why and when people get cut; budget season; the release cycle |
| 14 | [The locker room](14-locker-room.md) | Politics, veterans, agents, morale, leverage, the informal hierarchy |
| 15 | [Injuries and attrition](15-injuries-and-attrition.md) | Injury rates, schedule wear, career arcs, the aging curve |

### Part IV — The product
| # | Document | Covers |
|---|---|---|
| 16 | [Crowd psychology](16-crowd-psychology.md) | What audiences respond to and what they reject |
| 17 | [Heat, getting over, getting cold](17-heat-and-getting-over.md) | The mechanics of momentum, why people get hot and cold |
| 18 | [Match craft](18-match-craft.md) | Ring psychology, structure, styles, workrate vs drawing |
| 19 | [Characters and promos](19-characters-and-promos.md) | Gimmicks, mic work, alignment, turns, freshness |
| 20 | [Storylines and feuds](20-storylines-and-feuds.md) | Arc structure, angles, payoffs, blow-offs, factions |
| 21 | [Championships](21-championships.md) | Belt design, prestige economics, reign length, lineage |

### Part V — Structures and strategy
| # | Document | Covers |
|---|---|---|
| 22 | [Brand splits](22-brand-splits.md) | What motivates them, what sustains them, why they work, why they fail |
| 23 | [Fanbase segments](23-fanbase-segments.md) | Who watches, what each group wants, how they conflict |
| 24 | [The independent scene](24-independent-scene.md) | Indie economics, per-date work, the ecosystem underneath the majors |
| 25 | [International models](25-international-models.md) | Japan, Mexico, UK, Europe, elsewhere — genuinely different operating systems |

### Part VI — Time
| # | Document | Covers |
|---|---|---|
| 26 | [Eras and history](26-eras-and-history.md) | The periods, what changed, what each teaches |
| 27 | [Case studies](27-case-studies.md) | Specific successes and failures, examined for mechanism |
| 28 | [Cultural inertia and decline](28-cultural-inertia.md) | When merit stops mattering, decay curves, warning signs, death spirals |

### Part VII — Reference
| # | Document | Covers |
|---|---|---|
| 29 | [Benchmarks and numbers](29-benchmarks-and-numbers.md) | The numeric appendix: ranges for everything |
| 30 | [Glossary](30-glossary.md) | Industry vocabulary, including the words that mean two things |
| 31 | [Mapping to WrestleSim](31-sim-mapping.md) | What the engine models, what it doesn't, what to build next |

---

## The ten-line version

If you read nothing else:

1. Wrestling is a **live-event and television business** that sells recurring emotional
   investment in people. Everything else is downstream of that.
2. The **product is the character, not the match**. Matches are where characters pay off.
3. Money follows **eyeballs, then engagement, then loyalty** — in that order of size and
   inverse order of durability.
4. A promotion is a **schedule with a story engine attached**. The schedule is the hard
   constraint; creative fills it.
5. Talent value is **drawing power**, which is only loosely correlated with in-ring quality
   and strongly correlated with how much the audience cares.
6. Almost every catastrophic decision in wrestling history came from **one person with
   unchecked authority and no feedback loop** — usually an owner, usually late in a run.
7. Audiences **punish being lied to** more than they punish being bored, and they can tell.
8. **Nothing stays hot.** Freshness decays; the job is managing the decay, not preventing it.
9. Companies survive long past the point of being good because of **habit, nostalgia, and
   lack of alternatives** — that is cultural inertia, and it is a real, measurable asset
   that depletes.
10. The business is **cyclical, not progressive**. Every era's innovations become the next
    era's stale conventions.
