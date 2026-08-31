# 16 — Crowd Psychology

*What audiences respond to, what they reject, and why. The mechanics of a live crowd and a
television audience.*

This answers the user questions: **"What do fans respond to in terms of performance? What
don't they like?"**

---

## 1. The first principle

> **The audience does not react to what happens. It reacts to what it has been made to
> want, and whether that want is fed, denied, or insulted.**

A perfectly executed move in a match nobody cares about generates nothing. A punch in a
match the audience has been waiting six months for generates a riot. **Everything below is
downstream of this.**

Corollary: **crowd reaction is a measure of prior investment, not of present quality.**
This is why the sim's per-pairing crowd ceiling is correct and important — two people the
audience isn't invested in cannot generate a main-event reaction regardless of execution.

---

## 2. The taxonomy of crowd reaction

Not all noise is the same, and confusing the types is the most common analytical error.

| Reaction | Sound | Means |
|---|---|---|
| **Pop** | Sudden cheer | Something desired just happened |
| **Heat** | Sustained boo | The audience hates this person and wants them beaten |
| **X-Pac heat / go-away heat** | Boos with disengagement, "boring" chants | The audience wants this person *off my television*. **Fundamentally different from real heat and much worse.** |
| **Nuclear heat** | Genuine anger, sometimes uncomfortable | The line has been crossed; potentially valuable, potentially dangerous |
| **Silence** | Nothing | **The actual failure state.** No investment either way. |
| **Sympathetic quiet** | Hushed, attentive | The audience is invested and tense. This is *good* and is often misread as dead. |
| **Duelling chants** | Two competing chants | Maximum engagement; both characters are working |
| **"This is awesome"** | Chant | The match, not the story, has won them |
| **"Holy shit"** | Chant | A spot exceeded expectation |
| **Sarcastic chants** ("What?", counting along, "we want [someone else]") | | **The audience has disengaged and is entertaining itself.** A red alert. |
| **Hijacking** (beach balls, chanting for an unrelated wrestler, singing) | | Total loss of the room |
| **Standing ovation** | | Rare and earned; usually for a retirement, a return, or a performance |
| **Boos for a babyface** | | **Ambiguous and critical.** The audience cares and disagrees. Far better than silence. |
| **Cheers for a heel** | | The heel is *cool*, not hated. Manageable, sometimes desirable, but it breaks the standard match structure. |

### 2.1 The single most important distinction
**Boos vs silence.** A booed babyface is a solvable problem — turn them, repackage them,
or lean into it. A silent segment means the audience has no relationship with these people
at all, which takes months to fix. **Promotions consistently over-fear boos and
under-fear silence.**

### 2.2 The "cool heel" problem
When a heel is admired rather than hated, the standard face-in-peril structure breaks: the
crowd doesn't generate save-the-babyface tension during the heat segment, so the comeback
has nothing to release. This is modelled in this repo's engine as a low `tensionFactor` and
is the stated reason Roman vs Brock at WM34 rates as it does. **It is a genuine, structural
problem, not a booking preference** — the emotional machinery of a wrestling match requires
someone the audience wants to see get beaten.

Solutions used in practice: make the heel do something genuinely unforgivable; put them
against someone the crowd loves more; or accept it and book the match as a spectacle rather
than a morality play.

---

## 3. What audiences respond to — the reliable levers

### 3.1 Justice and injustice
**The oldest and strongest.** Someone is wronged; the audience wants it made right. Every
territory ran on this. It works because it engages a moral instinct rather than an
aesthetic judgment.

Required components: a clear wrong, a wronged party the audience likes, a wrongdoer with no
excuse, and a plausible path to restitution.

### 3.2 Struggle overcome
Sustained adversity followed by a comeback. Physiologically effective: the crowd holds
tension during the heat, and the comeback releases it. **This is the fundamental structure
of a wrestling match** and the reason "face in peril" has been the dominant formula for
seventy years.

The sim implements this directly: `earnedBonus` scaling with momentum deficit is exactly
this mechanism.

### 3.3 Authenticity
The audience is extremely good at detecting whether a performer means it. Not whether the
wrestling is real — whether the *person* is real. The most beloved acts in history are
almost all performers whose character was an amplification of themselves rather than an
imposition on them.

**This is the single most reliable predictor of whether a character will connect.**

### 3.4 Competence and difficulty
Athletic feats, precision, things that are visibly hard. Effective but shallow on its own —
it produces "this is awesome" (admiration) rather than investment. Admiration does not
sell tickets by itself, but it does build a reputation over time.

### 3.5 Surprise
Returns, debuts, turns, unexpected results. Enormously effective per instance and
non-renewable — see §5.1.

### 3.6 Escalation
Things getting bigger, more personal, more dangerous. The audience wants the stakes to rise.
A feud that stops escalating dies.

### 3.7 Recognition and callbacks
Rewarding the audience for paying attention. A returning finisher, a callback to an old
feud, a spot from a famous match. Extremely powerful with a long-tenured audience and
invisible to a new one.

### 3.8 Participation
Chants, call-and-response, catchphrases, "yes"/"woo"/"what". Giving an audience something
to *do* converts passive watching into active investment. The most successful gimmicks in
history nearly all have a participation hook.

### 3.9 Aspiration and identification
"I want to be that" or "that's me". Underneath every major babyface run is a group of
people who see themselves in the performer — the working man (Austin, Dusty), the outsider
(Bryan, Punk), the underdog, the person who won't quit.

### 3.10 Genuine emotion
Real tears, real rage, real vulnerability. Rare, unfakeable, and the moments that get
remembered for decades.

---

## 4. What audiences reject

### 4.1 Being talked down to
The single most damaging thing a promotion can do. Symptoms: commentary telling the
audience what they just felt, storylines that explain themselves repeatedly, characters
that behave as if the audience is stupid.

### 4.2 Being overruled
When the audience makes its preference clear and the promotion proceeds anyway.
**The reaction escalates rather than fading**, because it becomes a contest. This is the
central mechanism of the most famous audience revolts in wrestling history.

### 4.3 Inconsistency
Rules that change, characters who behave differently week to week, results that contradict
the last three months. The audience is keeping score even when it says it isn't. Once the
audience concludes that the fiction has no rules, it stops predicting, and prediction is
the substrate of anticipation.

### 4.4 Wasted time
Recap packages, repeated video, long entrances with nothing after them, matches with no
stakes, promos that say nothing. Live crowds punish this immediately; TV audiences punish
it by leaving.

### 4.5 Unearned outcomes
Someone winning who hasn't done anything. The audience will accept almost any result if the
path to it made sense.

### 4.6 Broken promises
Advertised matches that don't happen. Builds with no payoff. Stories that stop rather than
end. This is the most corrosive because it teaches the audience not to invest **next time**,
which is a compounding cost.

### 4.7 Comedy that undercuts threat
A monster who gets laughed at cannot be a monster again. Comedy is a one-way door for most
characters.

### 4.8 Excessive interference and non-finishes
See [04-booking-philosophy.md](04-booking-philosophy.md) §5.1. Three DQ finishes in a
month and the audience stops reacting to near-falls, permanently, for the whole card.

### 4.9 Predictability
Not the same as consistency. Consistency is the rules being stable; predictability is the
*outcome* being known. The audience wants to be able to predict the logic and not the
result.

### 4.10 Overexposure
Too much of anything. See [17](17-heat-and-getting-over.md) on freshness decay.

### 4.11 Visible effort to make them care
Video packages telling you someone is a big deal, commentary insisting, a big entrance
production, without the underlying substance. The audience reads this as compensation and
resists on principle.

---

## 5. The mechanics of a live crowd

### 5.1 The energy budget
A live crowd has a **finite physical capacity**. They can be loud for perhaps 40–60 minutes
of a three-hour show. This produces hard structural rules:
- **The opener should raise them, not peak them**
- **Cool-down segments are functional**, not filler — the crowd must recover to react to
  the main event
- **A hot crowd made hotter is cheaper than a cold crowd warmed up** — sequencing matters
  enormously
- **By hour three, the crowd is physically depleted** regardless of quality

The sim already models this well: crowd energy carries between items, decays naturally, and
gains compress near the ceiling. That compression is correct — the last 20 points of a
reaction genuinely are much harder to buy than the first 20.

### 5.2 The contagion effect
Crowds are herds. A section that starts a chant recruits neighbours within seconds.
Practical consequences:
- **Crowd density matters more than crowd size.** 2,000 people packed tight are louder and
  more responsive than 5,000 spread through a half-empty arena.
- **Empty seats are actively harmful**, not just optically — sound doesn't propagate, and
  individuals feel less licence to shout.
- **A small group of loud fans can define the whole building's reaction**, for good
  (a hot section starting a chant) or ill (a hijacking).

### 5.3 Crowd composition varies by market and by show type
| Crowd type | Characteristics |
|---|---|
| **Hardcore market** (Chicago, Philadelphia, Brooklyn, Toronto, Osaka) | Knowledgeable, opinionated, will boo the "correct" babyface, will chant for whoever they decide |
| **Family/casual market** | Cheers babyfaces, boos heels, reacts as intended. What the promotion is actually booking for. |
| **Post-tentpole crowd** (the night after WrestleMania) | Notoriously the rowdiest, most self-aware, most hijack-prone crowd of the year |
| **International crowd** | Enthusiastic, less jaded, huge reactions for anything |
| **Tourist crowd** (a stadium show) | Broad, casual, reacts to spectacle rather than continuity |
| **Indie crowd** | Small, extremely engaged, participatory, in on the joke |
| **Japanese crowd** | Quiet during holds and attentive; explosive on near-falls. **The silence is respect, not boredom** — a critical calibration point for anyone modelling international crowds. |

### 5.4 The hard camera side
The side of the arena the main camera faces is dressed to look full and is where the loudest
fans are placed. It is also why crowd noise on TV is not a reliable measure of the actual
room.

### 5.5 Sweetening
Promotions mix crowd audio. Reactions are enhanced in post, and canned noise has been used.
This is standard practice and means **televised crowd reaction is not raw data**.

---

## 6. The television audience is a different animal

| | **Live crowd** | **TV audience** |
|---|---|---|
| Commitment | Paid, travelled, present all night | Can leave in one second |
| Attention | Captive | Competing with a phone |
| What they reward | Spectacle, participation, big moments | Story, characters, a reason to stay |
| What they punish | Slow segments, talking | Anything they've seen before |
| Feedback | Instant and audible | Quarter-hour ratings, next day |
| Bias | Self-selected, hardcore-skewed | Broader, more casual |

**The critical implication:** the people in the building are **not representative** of the
people watching. A live crowd that boos a babyface may represent 5,000 hardcore fans while
1.5 million viewers at home have no objection. Booking to the live crowd's preference is a
real and common error — and so is ignoring it entirely, because the live crowd is the only
real-time signal that exists.

---

## 7. What makes a crowd hot before the bell

Controllable factors, in rough order of impact:
1. **The build** — did they arrive wanting to see this?
2. **The market** — some towns are simply hotter
3. **The show type** — a PPV crowd arrives at a higher baseline than a house-show crowd
4. **The preceding segment** — mood carries (the sim models this)
5. **Density and house size** — a full room
6. **The pre-show** — a dark match to warm them up is standard and works
7. **Alcohol** — genuinely a factor and openly acknowledged in the business
8. **Entrance production** — music, lighting, pyro; a great entrance is worth a real
   percentage of a reaction
9. **Time of night** — hour one > hour three
10. **The commentary framing** — telling them this matters, before it starts

---

## 8. The four crowd states, and how to move between them

```
   DEAD  ──(a hot opening, a surprise, a big name)──►  WARM
   WARM  ──(a good match, escalation, participation)──►  HOT
   HOT   ──(a payoff, a moment, an upset)──►  PEAK
   PEAK  ──(30–90 seconds)──►  falls back to HOT, then decays
   ANY   ──(wasted time, a non-finish, an insult)──►  HOSTILE or DEAD
```

**HOSTILE is not DEAD.** A hostile crowd is engaged and can be converted; a dead crowd
cannot, within one night. The tools are different: a hostile crowd responds to
acknowledgement and to giving them what they want; a dead crowd needs stimulus — a big
name, a surprise, or a spectacle.

---

## Sim implications

The engine already implements a lot of this well. The gaps worth closing:

- **Reaction type, not just magnitude.** Crowd energy is currently a scalar. The real model
  is a **vector**: pop, heat, go-away heat, silence, hostility. A match generating
  "hostile, engaged" is a completely different outcome from "quiet, uninvested" and should
  produce different downstream effects on popularity and feud heat.
- **Silence as the failure state**, not booing. A booked babyface being booed should
  *increase* their heat metric while flagging a misalignment — an opportunity, not a
  penalty.
- **The crowd energy budget across a show** is already partly modelled (mood carries,
  natural decay). Adding a **cumulative fatigue** that makes hour three structurally harder
  would complete it.
- **Crowd composition per market/show type** as a modifier set: a hardcore crowd rewards
  workrate and punishes formula; a casual crowd rewards spectacle and story clarity. Same
  match, different rating. This is the single most flavourful addition available.
- **Density** — a half-full building should measurably reduce reaction, independent of
  attendance revenue.
- **Live-crowd vs TV-audience divergence:** two separate reaction scores from the same
  show, which can disagree. That disagreement is the core of the modern booking dilemma and
  would be genuinely novel in a wrestling sim.
- **The participation hook** as a gimmick property — a character with a call-and-response
  gets a crowd-energy bonus that scales with how long the audience has known them.
