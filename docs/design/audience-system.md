# WrestleSim — Audience, Gimmick & Character Design

**Status:** Vision agreed, not yet implemented. This document is the reference the
implementation should be built against.

---

## 1. The premise

The three intended USPs are the Gimmick System, the Audience System, and the
Storytelling Construction System. Treated as three separate features they become
three half-built systems competing for development time. They are better understood
as one system with three faces:

- The **gimmick** is a promise made to the audience.
- The **story** is the delivery on that promise.
- The **audience** is the judge of whether it was delivered.

The audience is therefore the layer everything else resolves through. It is not one
of three pillars; it is the substrate, and the other two are things that get
evaluated by it.

### Where the genre actually stands

None of the three is unoccupied ground, and it is worth being precise about the bar:

| Feature | Prior art | What it doesn't do |
|---|---|---|
| Gimmicks | TEW has had them ~20 years: a rating, staleness over time | Gimmick is a scalar modifier on performance. It doesn't shape available stories or interact with other gimmicks. |
| Audience | TEW models the *promotion's product profile*; Wrestling Spirit did in-match crowd response | Nobody models the crowd as factions with conflicting taste who remember what you did to them. |
| Storytelling | TEW has literal Storylines with accumulating heat | Accumulation, not construction. No setup/payoff, no structure, no cost to an unresolved thread. Widely experienced as bookkeeping. |

### Why nobody has "nailed" these

Not ambition — **legibility**. TEW's failure mode is that a segment scores 68 and the
player cannot tell why. The systems exist; the explanation doesn't.

**This is the moat.** A shallower simulation that explains itself will feel deeper
than a deeper one that doesn't. Every design decision below is subordinate to this:
*the crowd must be able to say, in words, why it reacted.*

---

## 2. Design principles

1. **Legibility over fidelity.** If the model can't explain a reaction in a sentence,
   the model is wrong, however accurate it is.
2. **Derived, not authored.** Numbers that a designer types by hand don't respond to
   change. Compute them from primitives instead.
3. **Emergence in the model, named categories in the UI.** Simulate the messy thing;
   present it through labels a player already understands.
4. **Nothing in the absolute layer names a role.** "Heel" and "underdog" are
   computed per matchup, never stored on a performer.
5. **The crowd is an input, not just an output.** Crowd state feeds the match engine;
   it isn't merely a verdict on it.

---

## 3. The player's seat

**You are Head of Creative.** You book the show. You do not own the company.

- You have a **budget** you work within, not a treasury you control.
- You **answer to an owner** who has their own taste and their own priorities.
- **Rival promotions exist** and compete for the same audience.

This is deliberately not a full management sim. The business layer exists to give
crowd reaction consequences — reaction converts to attendance, merch and ratings,
which fund your roster — not to be the game in itself.

### The boss is an audience of one

The owner has a taste vector like any fan group, plus a veto and the chequebook. No
new machinery is required. This produces the three-way squeeze that makes the job
real:

> **What the crowd wants. What the boss wants. What the budget allows.**
> Rarely the same thing.

Critically, the owner can *mandate* a push the crowd will reject, and your job becomes
making it survive. Defiance is often not your choice — which is exactly how the real
job works.

---

## 4. The audience model

### 4.1 Architecture: agent population, group lens

Two architectures were considered.

A **segment model** (a handful of named groups, each with a taste vector and state) is
cheap, fast and trivially explainable. But every member of a group reacts identically,
split crowds are impossible except along boundaries drawn in advance, chants have to be
faked with scripted rules, and the model can never surprise you with a movement you
didn't anticipate.

An **agent population** (many individual simulated fans, each appraising independently)
produces a *distribution* rather than an average, and gets split crowds, emergent
chants, unplanned fan movements and real churn for free.

**Decision: run the agent population as the model, and use named groups as a lens over
it.**

Groups are **queries, not containers**. "Hardcore" is not a bucket that holds agents;
it is `agents where workratePreference > 0.7`. The game measures that cluster on demand
and reports it in words. Because groups are queries, the game can also cluster the
population without being told what to look for, and surface things nobody named:

> *"A bloc that likes both comedy and extreme violence is forming around this guy —
> about 6% of your audience, and they're loud."*

That sentence is unobtainable from a segment model in principle. This is how we get
depth without incomprehensibility.

**Implementation notes:**

- **Cohort sampling.** Don't simulate 50,000 individuals. Simulate ~2,000–5,000
  *representative* agents, each carrying a weight ("this agent stands for 18 real
  people"). Statistically near-identical, an order of magnitude cheaper.
- **CPU is not the constraint.** It's a dot product per agent per segment — milliseconds
  in C#. The constraints are save size and legibility.
- **Agents hold decaying opinions, not event logs.** A sparse dictionary of "how I feel
  about this performer" that ages — not a diary. This is what keeps save files sane.

### 4.2 Taste vectors

The current `FanGroup` enum is `Casual, Hardcore, Online, International, Families,
Kids`. These are six different kinds of thing on four different axes — engagement
depth, consumption channel, geography, demographic. A 28-year-old German who watches
everything and posts on Reddit is Hardcore *and* Online *and* International. As
mutually-exclusive buckets he is unrepresentable, and any loop that sums across groups
is quietly double-counting.

**Replace boxes-of-people with taste.** Proposed axes (starting set, expected to move):

| Axis | Low ←→ High |
|---|---|
| Workrate ↔ Spectacle | technical quality vs. spectacle |
| Realism ↔ Fantasy | grounded vs. supernatural/larger-than-life |
| Continuity ↔ Novelty | rewards long-term payoff vs. wants new things |
| Violence tolerance | squeamish → bloodthirsty |
| Comedy tolerance | wants it serious → enjoys comedy |
| Nostalgia pull | indifferent to history → strongly moved by it |

A named group is a **preset**: a point in this space. "Hardcore" is high workrate, high
continuity, high violence tolerance, low comedy. "Online" is nearly the same, with more
novelty hunger — which the model *tells* you, rather than requiring you to author two
near-identical sets of numbers forever.

### 4.3 Appeal is derived, not authored

Currently `AppealRatings` is hand-written per gimmick per group in `Wrestlers.json`.
That doesn't scale, and worse, it doesn't respond: change a gimmick's tone and the
numbers don't move.

Give `GimmickType`, `GimmickTone` and `GimmickTraits` positions on the same axes, and
appeal becomes arithmetic — gimmick vector against fan vector. Author the gimmick once,
get every group's reaction computed, and a mid-career tone shift automatically
re-scores across the whole audience.

`AppealRatings` becomes a **cache**, not a source of truth.

### 4.4 Freshness is per-group

`Gimmick.Freshness` is currently a single double. But hardcore fans tire of an act years
before casuals do — which is *why* "so-and-so is stale" discourse always starts online
and reaches the general audience much later. One float cannot express that. Decay rates
come out of the taste model: novelty-hungry tastes decay fast, nostalgia-weighted tastes
decay slowly.

### 4.5 Reaction is two-dimensional

**Never collapse reaction to one number.**

- **Valence** — cheer ↔ boo
- **Intensity** — deafening ↔ silent

Conflating these is the mistake every wrestling game makes. Roman Reigns 2015–19 was
rejected as a top babyface *and* the most reacted-to man on the show. A rejected push
and an ignored push are opposite outcomes.

> **Boos are not failure. Silence is failure.**

### 4.6 Crowd composition and contagion

The building is **sampled fresh for each show** from the region's population, weighted
by event type. Hardcore fans travel and concentrate at big events — which is why the
post-WrestleMania crowd is reliably the most hostile of the year with the same roster
and the same booking as the week before.

**The loud minority sets the tone.** 500 committed fans in a 15,000-seat arena determine
what the broadcast sounds like. Agents influence nearby agents; a committed minority gets
loud, the loudness recruits the ambivalent, and a small bloc ends up defining the
building. This falls out of the agent model for free and is unobtainable from segments.

### 4.7 Desire, pressure and revolt

The crowd holds **live desires** — wants this person pushed, wants that title change,
wants a payoff you keep deferring. Denied desires accumulate **pressure**, and pressure
converts into organised action at thresholds:

```
chants → hijacking unrelated segments → open revolt
```

Denial converts a preference into a *cause*. The more Daniel Bryan was denied, the
stronger the want got, until fans occupied the ring on live television. Note also that
WWE capitulated *late* and it still worked — giving in is not only viable early.

---

## 5. Opinion has three layers

"How the crowd feels about you" is not one number. It is three, and they behave
completely differently.

| Layer | What it is | Behaviour |
|---|---|---|
| **Heat** | How much they care right now | Volatile, fast decay, moves week to week |
| **Investment** | "I have decided to support this person" | Slow to build, *very* slow to lose. Asymmetric decay — near-immovable when high |
| **Character contract** | What they believe you *are* | Doesn't decay on a timer. Only moves when booking contradicts it |

Only Heat uses simple time decay. Investment is what survives a heel turn and why a
megastar can leave for a decade and return beloved. The contract is what decides
credibility.

### 5.1 Investment inverts moral valence

Above an investment threshold, **the valence of a moral violation flips sign** for that
agent. This is *reinterpretation*, not suppression — Rock's heel promos weren't
tolerated, they were cheered. The crowd actively wanted to see him do that.

Because reactions are computed per agent, the interesting cases come free:

- **Moderate stardom** → some agents flip, some don't → a genuinely split building.
  This is what a messy real turn looks like.
- **Megastar** → nearly everyone flips → **you cannot generate boos.** Not because the
  game forbids the turn, but because the population won't produce them.

That is an *emergent* booking constraint, not a rule. It produces a strategic problem
nobody has simulated: **a roster of beloved stars means you have no credible villains.**

### 5.2 Two kinds of violation

| | What it breaks | Effect of high investment |
|---|---|---|
| **Moral violation** | Did something villainous | **Protects** — above threshold it inverts into approval |
| **Identity violation** | Stopped being who they think you are | **Amplifies** — the more invested, the more specific the expectations, the harder the betrayal lands |

The governing rule for the entire system:

> **Check a proposed booking against the character contract, not against alignment.**
> Immoral-but-in-character is safe. Out-of-character is dangerous regardless of morality.

There is a third, quieter failure: **boredom.** Beloved plus nothing happening gives high
valence with collapsing intensity. Being loved does not exempt you from needing something
to do.

---

## 6. Character contracts

A contract is the set of traits the audience believes about a performer, each with a
**conviction** level, accumulated from booking history.

```
Becky:  Underdog 0.9 | GreatTalker 0.9 | Resilient 0.8 | Dominant 0.1
Rhea:   Dominant 0.95 | Imposing 0.95 (anchored) | Underdog 0.05
```

### 6.1 Physical anchors

**Traits backed by physical attributes have a conviction floor that booking cannot
lower.** You can book a large performer to lose a hundred times; you cannot make the
audience believe they are small.

This is why cowardice works differently for different people:

- **Becky** — cowardice contradicts `Resilient`, but that isn't load-bearing, and it is
  *compatible* with `Underdog`, because underdogs taking shortcuts is a recognised story.
  It has somewhere to land, so it reads as character development.
- **Rhea** — cowardice contradicts her highest-conviction trait, and that trait is backed
  by something visible every week. The contradiction never becomes credible, so it never
  reads as story. It reads as incompetence.

`PhysicalAttributes.Size` and `.Strength` already exist on `Wrestler`. They are the anchor.

### 6.2 Rewriting a contract

Contracts can change, slowly, and **the contradiction has to be the story.** Repeated
contradiction *with acknowledgement* lowers conviction over time — Cody Rhodes going from
"Dashing" to a serious main eventer over years. Each contradiction costs credibility up
front and pays back only if you commit.

Physical anchors never move, unless the performer physically changes — which is also real
and should be modelled.

### 6.3 Contradiction vs re-emphasis

The Rock's heel turn worked because arrogance was **compatible** with his existing
contract — it wasn't a contradiction, it was an emphasis. The system must distinguish
these, and reward players who find the second one. That is the difference between
breaking the contract and reading the fine print.

---

## 7. The relational layer

**This is the load-bearing architectural decision.** Roles are not properties of a
performer. They are properties of a **pairing**.

### 7.1 Threat is absolute; role is relative

What's absolute is **Threat** — how dangerous the audience believes you are. What's
relative is everything the player actually cares about:

```
UnderdogFactor(A vs B) = Threat(B) − Threat(A)
```

A performer at 0.85 against a rookie at 0.30 is a monster and the crowd expects
annihilation — the sympathetic register is unreachable. The same performer against a
properly built giant at 0.95 is beneath, and the underdog role unlocks. Same person,
same contract, same week, different opponent.

So the "what can this person credibly be?" screen needs an **opponent axis**. It's a
matrix, and finding the pairing that unlocks the story you want *is the booking puzzle*.

### 7.2 What builds Threat

Aura is mostly presentation, not results. The Undertaker's threat came from aesthetic,
protection and scarcity far more than from a win-loss record. Inputs:

- **Physical anchors** — size, strength. Free, immovable, unearned.
- **Protection** — how rarely they've been made to look vulnerable. The big one, and it
  is *spendable*.
- **Presentation** — entrance, lighting, how others react to them, whether they sell.
- **Scarcity** — appearing less makes you bigger. Nobody models this; it's very real.
- **Quality of victims** — beating credible people, not merely beating people.

### 7.3 Monsters are infrastructure

Threat is a **shared roster resource**. Building a monster creates underdog stories for
everyone beneath them — the whole card gains access to a register it didn't have.
Destroying one, by booking them to lose badly or too often, destroys value across the
entire roster at once, not just for that performer.

A monster is an asset with depreciation and a real cost of misuse. No wrestling game
treats them that way.

### 7.4 Heel heat routes through the victim

You cannot be a heel in the abstract, only relative to someone. If nobody in the match is
more beloved than you, you cannot draw boos.

**Heat is generated by the audience's investment in the person being wronged, not by the
aggressor's heel rating.** Every agent asks one question:

> *Do I care more about who's being hurt than I enjoy watching who's doing it?*

Cheer if no, boo if yes, scaled by transgression severity. This single comparison
produces everything observed:

- Heel acts against a nobody → cheered. No sympathy to route through.
- Identical acts against a beloved young face → booed. Investment in the victim overrides
  enjoyment of the aggressor.
- Therefore **you must build babyfaces before you can build heels.** Babyface investment
  is the raw material heel heat is made of. A roster of heels is exactly as broken as a
  roster of megastars, for the same structural reason.

This is also the one lever that reliably works on a performer the crowd otherwise refuses
to boo.

### 7.5 Consequence: character space is a function of the roster

"This person is hard to book as anything but a powerhouse anti-hero" is not a fact about
that person. It is a fact about the roster around them. Build someone above them, or
someone beloved beneath them, and their viable space widens.

Roster construction and character viability are coupled.

---

## 8. Defying the crowd

Modelled on how it actually works.

**What fails:** persisting with the same framing against rejection. Rejection deepens and
hardens into identity — it acquires a name, a chant, a movement.

**What works:**

- **Realignment** — stop insisting on the label they've rejected, adopt the one they've
  assigned. Rocky Maivia → The Rock. Reigns → Tribal Chief. Works fast, works nearly always.
- **Acknowledgement** — the performer visibly hears the reaction. The single most reliable
  tool, because *the crowd forgives being beaten but never forgives being ignored.* Most of
  the venom in a rejected push is the sense that nobody is listening.

**Defiance can also be a stable, profitable end state.** John Cena was booed by adults and
cheered by children for over a decade and was never turned. The split *became* the product.
Which gives the most important business mechanic in the design:

> **Noise and money can point in opposite directions.**

*Which* segment is booing matters enormously more than how many. A small, loud, hostile
bloc can make the show sound like a disaster while the business is fine — and the owner,
who reads the business, will tell you to keep going. Track who is reacting separately from
how many.

**Investment is a resource you can spend.** A megastar absorbs bad booking that would kill
anyone else, so they can lose without damage — meaning you can spend their protection to
elevate someone. That's the actual function of a megastar, and it's a decision with a cost.

---

## 9. State summary

**Absolute, stored per performer:**

- Character contract — traits, conviction levels, which are physically anchored
- Threat, and its component inputs
- Per-agent Heat and Investment (sparse; most agents have no opinion on most performers)

**Derived, computed per matchup — never stored:**

- Threat differential → which roles are available to each participant
- Investment differential → who the crowd sides with
- Transgression severity × victim investment → heel heat generated

Nothing in the absolute layer says "heel" or "underdog". Those words exist only in the
derived layer.

### Shape

```csharp
class CharacterContract {
    Dictionary<Trait, double> Conviction;       // what they believe
    HashSet<Trait> PhysicallyAnchored;          // conviction floor, immovable
    double CredibilityCost(ProposedRole role);  // 0 = free, 1 = blocked
}

class AudienceOpinion {          // per agent, per performer — sparse
    double Heat;                 // fast decay
    double Investment;           // asymmetric decay, sticky when high
    bool FlipsMoralValence => Investment > FlipThreshold;
}

class Reaction {
    double Valence;              // cheer ↔ boo
    double Intensity;            // deafening ↔ silent
    string Explanation;          // MANDATORY. If this can't be written, the model is wrong.
}
```

---

## 10. Impact on the current codebase

| Current state | Required change |
|---|---|
| `Match.CalculateMatchRating()` is `RingSkills` + random charisma nudge, no crowd input | Crowd state becomes an **input**. A hot building elevates a mediocre match; a dead one buries a great one. |
| `CalculateWinner()` is a raw popularity roll | Outcome is a booking *decision* with consequences, not a dice roll on popularity. |
| `Gimmick.Freshness`, `PopularityModifier`, `AppealRatings`, `Tone`, `Durability`, `NaturalAlignment` are all write-only | All become live inputs to appraisal. Appeal derived from taste vectors. |
| `FanGroup` enum mixes four category axes | Becomes taste-vector presets used as queries over the agent population. |
| `Match.PsychologyRating` declared, never assigned | Superseded by the reaction model. |
| No audience entity exists | The agent population is the core new subsystem. |
| `RingSkills.GetStandardScore()` is the most developed code in the project | This is the *least* differentiating layer — Fire Pro solved match sim decades ago. Effort should move up a floor. |

---

## 11. Open questions

1. **The Storytelling Construction System is not yet designed.** The audience model
   constrains it heavily — stories must produce contract changes, investment changes and
   desire/pressure — but the construction grammar itself (beats, setup/payoff, structure,
   the cost of an unresolved thread) is undecided. This is the next design conversation.
2. **Competition mechanics.** Rival promotions compete over taste-space regions, but talent
   movement, counter-programming and audience switching costs are unspecified.
3. **Business layer detail.** Reaction → attendance/merch/ratings → budget is agreed in
   principle; the conversion curves and what the budget actually buys are not.
4. **Trait taxonomy.** The contract needs a concrete trait list. Should be small and
   composable rather than a long enum.
5. **Owner model.** How much the owner's taste drifts, whether it can be influenced, and
   what happens when you succeed while defying them.
6. **Time granularity.** Per-show? Per-week? What decays between what.

---

## 12. Verification note

There is currently **no working build in CI**, and `DataLoaders` hardcodes absolute
Windows paths for both JSON loads, so the project only runs on one machine. Before any of
this is built, that should be fixed and a test harness should exist — an audience model
without tests will be impossible to tune.
