# 20 — Storylines and Feuds

*Arc structure, angles, escalation, payoff, and the shapes that reliably work.*

---

## 1. What a feud is

A **feud** is a sustained conflict between two (or more) characters that gives their matches
stakes. It is the fundamental narrative unit of wrestling — larger than a match, smaller
than a career.

Structurally, a feud has:
- **A cause** — why these two are fighting
- **An escalation** — it gets worse
- **A cost** — someone loses something
- **A resolution** — someone wins, definitively

Remove any one and the feud fails. The most common omission is the last: modern wrestling
is full of feuds that **stop** rather than **end**.

---

## 2. The causes — why two people fight

Ranked by how reliably they generate audience investment:

| Cause | Strength | Notes |
|---|---|---|
| **Betrayal** | Highest | A friend/partner turns. The strongest single motivator in wrestling. |
| **Personal violation** | Very high | Family, a partner, humiliation, injury caused deliberately |
| **Theft** | High | The title, a win, an opportunity, a moment |
| **Disrespect** | High | Simple, universal, always available |
| **Jealousy / ambition** | High | Someone wants what the other has |
| **History** | High | They have fought before; the record is unsettled |
| **Ideological conflict** | Medium-high | Two philosophies; works when both are clear |
| **Championship pursuit** | Medium | The default; adequate but generic on its own |
| **Ranking / tournament** | Medium | Sport-logic; strong in Japan, weaker in the US |
| **Random assignment** | Lowest | "You're facing each other because it's Tuesday" |

**The reliable rule:** the more *personal* the cause, the higher the ceiling. A title match
between two people who don't dislike each other is a sporting contest; a title match between
two people where one crippled the other's brother is an event.

---

## 3. The arc structures

### 3.1 The classic three-act feud
```
  ACT I — THE INCITING INCIDENT
     The cause. Usually an attack, a betrayal, a theft, a humiliation.
     The audience's sympathy is assigned here.
                    │
  ACT II — ESCALATION
     Match 1: the heel wins with help. Injustice compounds.
     Angles between: the heel goes further. Attacks a friend, steals the belt,
     costs the babyface something else.
     Match 2: the babyface wins but without resolution (DQ, count-out).
     The audience now needs a definitive answer.
                    │
  ACT III — THE BLOW-OFF
     A stipulation that removes the escape routes: cage, no-DQ, last man
     standing, I Quit, career/mask/hair on the line.
     Decisive result. The debt is paid.
```

**Duration:** 6–16 weeks typically, 3–6 months for a major program, up to a year for a
company-defining one.

### 3.2 The long-form epic
A feud that runs 9–18 months with multiple phases, usually involving the world title and the
promotion's identity. Requires: two performers who can carry it, a booker with the patience,
and a promotion whose schedule allows a slow build.

Structure: several complete three-act cycles, each ending in a partial resolution that
raises the stakes for the next.

### 3.3 The chase
A babyface pursuing a dominant champion over months. Simple, powerful, and dependent on the
champion being credible enough that the chase matters. Ends with the title change, which is
one of the biggest payoffs available.

**The failure mode:** the chase goes too long, the audience stops believing it will ever
happen, and the eventual win is met with relief rather than joy.

### 3.4 The tournament / league arc
Structure generates story. A round-robin (G1) or bracket produces:
- Meaningful matches with no angle required
- Natural contenders
- Upsets that create stars automatically
- A legible, sport-like narrative

**The most efficient story engine in wrestling** and the least used outside Japan.

### 3.5 The faction war
Two groups in sustained conflict. Efficient — it elevates 6–12 people at once, creates
numbers-game drama, and generates endless permutations of matches.

**The lifecycle:** formation → dominance → the babyface resistance forms → the war →
internal tension → the split → the split becomes the next feuds. The split is the payoff and
should be planned from the start.

### 3.6 The authority feud
A performer against the person who controls their opportunities. Extremely reliable heat
(everyone has a boss), and it is how the biggest boom period in US wrestling history was
built (Austin vs McMahon).

**The failure mode:** the authority figure becomes the show. When the general manager's
segments are the main narrative, the wrestlers become supporting cast.

### 3.7 The invasion
An outside group arrives. Enormous potential (nWo 1996 is the highest-drawing angle in
American wrestling history), enormous risk. Requires:
- The invaders to be **genuinely credible** and to win
- The home side to be **worth defending**
- A clear endpoint

**Why invasions usually fail:** the home promotion cannot bring itself to let the invaders
win enough, so the threat evaporates. The 2001 WWF/WCW Invasion is the definitive
cautionary example — the acquiring promotion had the biggest angle in history handed to it
and booked its own roster to dominate, killing the premise within months.

---

## 4. Angles — the units of storytelling between matches

An **angle** is a non-match segment that advances a story: an attack, a promo, a betrayal, a
contract signing, an interruption.

### 4.1 The standard angle vocabulary
| Angle | Function | Heat generated |
|---|---|---|
| **The beatdown** | Establish dominance, generate sympathy | High |
| **The betrayal/turn** | Recontextualise a relationship | **Highest** |
| **The interruption** | Create a confrontation | Medium |
| **The face-to-face** | Escalate verbally | Medium-high |
| **The contract signing** | Formalise, then brawl | Medium-high |
| **The challenge** | Set up the match | Low-medium |
| **The injury angle** | Write someone out, create a revenge motive | High |
| **The stolen title** | Personal, tangible, motivating | High |
| **The run-in** | Escalate a feud during another match | Medium |
| **The surprise return** | Attention, a new player | High, one-shot |
| **The backstage attack** | Cheap, effective, no ring needed | Medium |
| **The celebration interrupted** | The most reused structure in wrestling | Medium |

### 4.2 The rules of a good angle
1. **Something must change.** An angle where the situation is identical afterwards is filler.
2. **Someone must gain and someone must lose.** Symmetry is death.
3. **It must escalate from the last one.**
4. **It should end with a clear image** the audience remembers.
5. **It must not resolve anything the match should resolve.**

### 4.3 This repo's model
The `Segment` system with history tags (Betrayal, InjuryAngle, TitleStolen, FamilyInvolved,
ManagerConflict, FactionConflict, PersonalInsult, ChampionshipRivalry) maps very well onto
the real angle vocabulary. The tags gating specific match beats is exactly how real feuds
work — you cannot do a "revenge for what you did to my brother" spot without first
establishing the brother.

---

## 5. Escalation — the load-bearing mechanic

A feud must get worse. The escalation ladder, roughly:

```
   1. Words                     ── promos, insults
   2. Physical provocation      ── a shove, a slap
   3. An attack                 ── a beatdown after a match
   4. A cost                    ── costing them a match, a title shot
   5. A violation               ── attacking a friend, family, a manager
   6. A weapon                  ── escalation to real danger
   7. An injury                 ── someone is written off television
   8. The unforgivable          ── whatever this feud's version of the line is
   ───────────────────────────────────────────────────────────────
   BLOW-OFF: a stipulation matching the level of escalation reached
```

**The two errors:**
- **Escalating too fast** — you reach the top of the ladder in week two and have nowhere to
  go for the remaining six weeks
- **Not escalating** — repeating the same beat, at which point the audience concludes
  nothing is at stake

**The stipulation must match the escalation.** A cage match for a feud that never got past
words is unearned. A regular singles match to blow off a feud where someone was put through
a table and stretchered out is anticlimactic.

---

## 6. The blow-off

### 6.1 What it must do
- **Resolve** — someone definitively wins
- **Be proportional** to the escalation
- **Be memorable** — this is the image the feud is remembered by
- **Set up what's next**, ideally

### 6.2 The stipulation ladder
| Stipulation | Says |
|---|---|
| **No DQ / Street Fight** | Rules can't contain this |
| **Falls Count Anywhere** | It won't stay in the ring |
| **Cage** | Nobody escapes, nobody interferes |
| **Hell in a Cell / War Games** | The maximum containment structure |
| **Last Man Standing** | Only unconsciousness ends it |
| **I Quit** | Submission of will, not just body — the most personal |
| **Ladder / TLC** | Object-based, spectacle-forward |
| **Career vs Career** | The highest stake in American wrestling |
| **Mask vs Mask / Hair vs Hair** | The highest stake in lucha; genuinely permanent |
| **Loser Leaves Town** | Writes someone off with a story |

**Scarcity is everything.** A stipulation used monthly means nothing. The lucha *apuesta*
system works precisely because a mask loss is permanent and irreversible — it is the only
stipulation in wrestling with a genuinely unrepeatable stake.

### 6.3 Who wins
The default and usually correct answer: **the babyface, decisively, after suffering**. The
audience has been paying for this. Denying it is possible but expensive — it must serve a
larger arc, and it can only be done occasionally before the audience stops investing.

**When the heel should win the blow-off:**
- The feud is a chapter in a longer story
- The heel needs to be established as a bigger threat for someone else
- The babyface's story is about failure and eventual redemption (a long, risky arc)

---

## 7. Multi-person and division-level storytelling

### 7.1 Tag teams
The most efficient and most wasted format. A great tag team:
- Elevates two people simultaneously
- Has a defined structure (face-in-peril works perfectly with a hot tag)
- Creates a built-in future story (the split)
- Provides a home for performers who don't work as singles

**The hot tag** — the moment the beaten partner reaches their fresh teammate — is one of the
most reliable crowd-pop mechanisms in wrestling and doesn't exist in singles.

### 7.2 Factions
See §3.5. Additional notes:
- **Optimal size 3–5.** Larger and members become invisible.
- **Needs a clear hierarchy** — a leader, an enforcer, a wildcard
- **Needs a purpose** beyond "we're friends"
- **The split is the payoff.** A faction that dissolves without a story wastes its entire
  investment.

### 7.3 Divisions
A **division** (tag, women's, cruiserweight, TV title) needs:
- A title
- 6–10 credible performers
- Its own storylines, not just crumbs from the main event
- Enough television time for the audience to learn who these people are

**The most common failure:** a division exists on paper but gets 4 minutes a week, so the
audience never learns the names, so nobody in it can get over, so it gets less time. This
is a death spiral and it explains why most secondary divisions fail.

---

## 8. Continuity and long-term payoff

- **The audience remembers more than promotions assume**, especially the invested core
- **Callbacks are cheap and enormously effective**: a returning move, a reference to a
  three-year-old betrayal, a rematch of a famous match
- **Unresolved threads are debts.** They accumulate, and an audience that has watched five
  stories evaporate stops investing in the sixth.
- **The long payoff** — a story that resolves after a year or more — is the most satisfying
  thing wrestling can do and the rarest, because it requires a stable creative regime and a
  roster that stays put.

---

## 9. What kills feuds

| Killer | Mechanism |
|---|---|
| **Too many matches** | The 4th match between two people draws less than the 1st, always |
| **No escalation** | Repetition without progression |
| **Comedic deflation** | One comedy segment can undo six weeks of menace |
| **The interference loop** | Every match ends in a run-in; nothing resolves |
| **Distraction by other stories** | A feud left off TV for three weeks loses its heat |
| **An unearned resolution** | The blow-off doesn't match the build |
| **A departure or injury** | Unavoidable; handle by writing it in |
| **The audience picking the wrong side** | The story is now about something else; adapt or lose |
| **A booker change mid-feud** | The new booker doesn't care about the old plan |

### 9.1 The match-count decay
| Match | Typical relative draw |
|---|---|
| 1st | 100% |
| 2nd | 85–95% (if the 1st ended non-decisively) |
| 3rd (blow-off) | 90–110% (the stipulation adds value) |
| 4th+ | 50–70% and falling |

**Three matches is the natural life of a feud.** Beyond that requires a genuine reinvention
(a new stipulation, a new stake, a long gap, a changed relationship).

---

## Sim implications

The `FeudBook` system is already a strong model of this. Refinements the reference suggests:

- **Feud heat should decay** when the feud isn't advanced. Currently heat accumulates; the
  cost of neglect is the missing half.
- **An escalation level** separate from heat: a feud that has reached "injury" should unlock
  different beats and stipulations than one at "words", and skipping levels should be
  penalised. The existing history tags are a good foundation for this.
- **Match-count decay per pairing** (§9.1) — the 4th match between the same two people
  should be worth substantially less. This is the single most impactful addition, because it
  forces roster rotation and makes fresh pairings valuable.
- **The blow-off as a distinct, terminal event** that pays out accumulated heat and *ends*
  the feud — with a large penalty for continuing past it, and a large penalty for
  never reaching it.
- **Stipulation scarcity** as a promotion-level counter: each stipulation type has a
  cooldown, and using a cage match monthly should devalue all cage matches.
- **Unresolved-feud debt** as a promotion-wide trust variable that reduces the rate at which
  new feuds accumulate heat. This models the real compounding cost of never paying things
  off.
- **Divisions** with minimum-TV-time requirements to remain viable would model the secondary
  division death spiral, which is one of the most recognisable real dynamics in the business.
