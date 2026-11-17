# Characters — a plan to talk through

**Status: a draft for the planning session. Nothing here is built. Only the items marked
"decided" are settled.** This plan treats the gimmick as a whole system, designed once, instead of
adding fields one at a time.

## 0. Why this document exists

The owner's complaint (16 Nov 2026) is that gimmicks feel flat, and that the roster feels
**interchangeable**. Doc 19 opens with the thesis this game is meant to rest on: *"Wrestling sells
characters. Matches are where characters pay off."* The game currently works the other way round.
It sells matches, and a character is a fixed modifier on how much somebody draws. Two wrestlers
with the same numbers are the same wrestler, whatever their gimmick says.

What the code does today, checked against the source on 16–17 Nov 2026:

| `Gimmick` field | Read by | Effect |
|---|---|---|
| `NaturalAlignment` | `PerformerProfile` | Sets what the booker intends the crowd to do: cheer or boo. The crowd's opinion can override it. |
| `AppealRatings` | `PerformerProfile`, `AudienceModel` | How much each fan group cares about the wrestler. Feeds their connection with the crowd and how fast each group tires of them. |
| `Durability` | `AudienceModel.Rate` | How fast the act goes stale: short-term ×1.3, long-term ×0.7. |
| `Name` | `AudienceModel` | A new name counts as a new act and resets how samey the crowd finds them. |
| `Type` | Talent screen label | **Nothing in the engine.** |
| `Tone` | — | **Never read.** |
| `PopularityModifier` | — | **Never read.** Set on all 124 entries. |
| `Freshness` | — | **Never read.** (Every other `Freshness` in the code belongs to stipulations.) |
| `GimmickTraits` | — | **Never read.** |
| `PersonaDescriptor` | Profile text, search | Flavour only. |
| Appeal to Online, International, Families, Kids | — | About 40 entries have these ratings. `AudienceModel.Mix` never puts those groups in a crowd. |

What the booker cannot do:

- **Turn anybody.** Nothing writes `NaturalAlignment` after load. `SegmentFactory.CreateBetrayal`
  exists and moves heat, but the betrayer stays the alignment they were.
- **Repackage anybody.** `Wrestler.ChangeGimmick` is never called, and it is broken: it adds the
  *new* gimmick to `PreviousGimmicks` and never makes it the current one. `ChangeName` is called
  only from a test.

How promos work (`SegmentSimulator`):

- Quality is base impact + average charisma × 0.5, times a location factor. Gimmick, tone, type
  and the performer's mic ability do not enter it. Charisma is the only input that belongs to the
  person.
- **Unscripted gets a flat ×1.15 for everybody.** A scripted segment can never botch (it carries
  zero risk). An unscripted one botches 10% of the time below 70 psychology and 2% at 70 or above.
  So the rule picks the winning form, not the people involved. That is the "forcing outcomes"
  the owner objected to, and it is in the code as well as in doc 19.

## 0A. Decided in the planning session

**16–17 Nov — what the work is for.**

- **The main problem is that wrestlers feel interchangeable.** Different characters must play
  differently: they want different booking, are hurt by different booking, and talk differently.
  That comes first. The booker's character actions (turns, repackages) come second, but they are
  in scope.
- **The booked alignment is the player's decision.** The engine never flips it on its own. The
  crowd's actual reaction is computed and *shown*, and the gap between the two is information the
  player can act on.
- **There are two kinds of staleness, and they stay separate.** *Act sameness* (exists in
  `AudienceModel`, measured in weeks) is how samey this wrestler's recent booking has been.
  *Character freshness* (new, measured in months) is how worn the character itself is. Different
  things cause each, and different things restore each.
- **Promos go deep, and neither form wins by rule.** Scripted promos can be superb: consistent,
  well structured stories, given good writers and performers who can deliver written material.
  Improvised promos can be superb too, given performers who can work without a script and the
  right chemistry. **Both can be awful.** What decides it is who is involved: the writers' quality
  and each performer's skill at *that* kind of promo, in *that* register. Mic skill is not one
  number, and it is not "good at scripted" versus "good at improv":
  - some people are bad at promos, full stop;
  - some are worse at one form than the other;
  - some are good at improvised comedy but need a script when the moment is serious.

  This **corrects doc 19's "Sim implications"**, which said scripting everything should give
  "consistent mid-range results". The correction is made inline there.
- **Rivals turn and repackage their wrestlers too, when it makes sense to.** One set of
  consequences (#102): a rival's turn is judged by the same model as the player's.

## 1. What a character is

The gimmick has three layers. Each layer does a different job, and changing each one costs
something different.

| Layer | Holds | Changed by |
|---|---|---|
| **Core** | Type, tone (register), the traits | Repackage |
| **Stance** | Booked alignment | Turn |
| **Presentation** | Name, entrance and music, descriptor | Repackage, or evolution (presentation only) |

On top of the gimmick sit three things that belong to the **performer**, not the character. They
come with the wrestler into any gimmick.

- **Charisma.** Presence: how much people want to look at them. Unchanged.
- **Promo skill** (new, Part 3): how well they talk, by form and register.
- **Fit** (new, Part 1A): how much of the gimmick is really them.

### 1A. Fit

Doc 19 §2.2: a gimmick works when it is "80% the performer and 20% amplification". Fit, from 0 to
1, is how well this gimmick suits this person. It controls how much of the gimmick's potential is
realised, both in connection and in how long the character lasts (Part 4).

- Fit is derived from the performer's attributes against what the type asks for:
  - a Monster wants size and power;
  - a Daredevil wants a high-flyer;
  - a Comedy act and a Showman want charisma and comic promo skill;
  - a Technician or Realist wants ring craft;
  - a Cult Leader or Cryptic act wants serious promo skill and psychology.
- **For the real roster, the shipped gimmick is the person's real, working character, so it
  starts with high fit by construction.** Fit matters most when the character *changes*. A
  repackage is a bet on fit, which is why some repackages make a career and others end one.
- A generated wrestler's starting gimmick is drawn with ordinary fit, some good and some poor. A
  generated wrestler in the wrong character is a real find for a booker who notices.

## 2. Characters on the card — the interchangeability fix

**This is the heart of the plan.** Each type states three things: what *feeds* it, what *damages*
it, and where on a card it does its best work. Traits sharpen that, so two Monsters are not the
same Monster.

Illustrative only. Every number is a first guess for calibration (Part 8).

| Type | Fed by | Damaged by | Notes |
|---|---|---|---|
| Monster | Dominant wins, squashes, being protected | Clean losses (cost far more), long even matches, talking | Doc 19 §2.1: "collapses when beaten too often" |
| Underdog | Close losses, comebacks, beating the odds | Squash wins, a long streak, never getting the payoff | Must eventually win something; the crowd tires of an underdog who never does |
| Showman | Big shows, entrances, the crowd's attention | Being buried low on the card | The entrance is "worth 20–30% of a reaction" (doc 19 §8) |
| Superstar | Main events, title pictures | Being booked below their perceived level | Doc 17 §3.5 |
| Egotist / Sellout | Talking down to people, cheap wins | Losing clean to someone they belittled (the payoff, which is good for the *opponent*) | The classic heel engine |
| Comedy | Light segments, crowd participation | Serious main-event threat (the one-way door, below) | |
| Cult Leader / Cryptic / Dark Persona | Slow builds, vignettes, mystery | Overexposure, being laughed at | Medium-high durability, "ridiculous when it doesn't" land |
| Realist / Traditionalist | Match quality, which carries more of their draw | Gimmick-heavy stipulations | Doc 19: "ceiling is respect rather than love" |
| Daredevil | Spectacle, high-risk spots, stipulations | Repetition: athletic novelty wears fastest (doc 17 §3.4) | Act sameness already models part of this |
| Anti-Hero / Rebel | Defying authority, fighting the system | Having nobody above them to fight | Doc 19: "requires an authority figure" |
| Loner | Fighting against the odds or against groups | Being put in a team or faction | |

How this reaches the game:

- **In the engine:** a result's effect on overness, momentum and appeal is weighted by type
  (a Monster's clean loss is dearer; an Underdog's close loss is cheaper or even pays). The pull
  on the card is weighted by placement. Each segment kind suits some characters better than
  others (Part 3).
- **In the builder:** the roster picker and match builder say *why* a booking suits or harms
  somebody, in words ("a monster cannot take many of these"). This follows the existing
  `BookingSuggestions` band pattern.
- **In the report:** the play-by-play and show report say what the character did ("the crowd
  will not buy Bronson Reed losing clean twice in a month").

**The comedy one-way door.** A flag set once a performer has been booked as comedy for long
enough. While it is set, serious threat (main-event credibility, a heel's danger) is harder to
establish. Only a genuine repackage clears it (doc 19 §5.3).

**Effects are centred.** Averaged across the roster and a typical card, the type weights come to
about 1.0. Characters should differ from each other without the whole roster drawing more or
less (Part 8).

## 3. Promos as performances

### 3.1 The performer: promo skill by form and register

Proposed shape (open question 1): **a 2×2 per performer**, scripted or improvised against serious
or comic.

| | Serious | Comic |
|---|---|---|
| **Scripted** | Can deliver written serious material with conviction | Can land written jokes |
| **Improvised** | Can cut a serious promo off the cuff | Can riff, work the crowd, be funny live |

This expresses every case the owner named:

- bad at promos: low everywhere;
- worse at one form: one row low;
- good improvising comedy but needs a script for serious: improvised-comic high,
  improvised-serious low, scripted-serious fine.

Charisma stays a separate thing. A magnetic person can still be a poor talker, which is the case
managers exist for.

The gimmick's **tone** sets the register a promo is in by default (Humorous → comic, Serious,
Violent or Cryptic → serious). A segment can be played against it, at a cost, using the
performer's skill in the other column.

### 3.2 The writers

A scripted promo is only as good as what is on the page. Proposed for the first build (open
question 2): **the promotion's creative team as a quality figure**, set by what the promotion
spends and its tier. It also has a **range**: some rooms write excellent serious drama and poor
comedy. Writers as named, hireable staff are the fuller version, recorded as later.

### 3.3 The quality of a promo

- **Scripted:** writing quality in that register × the performer's scripted skill in that
  register × how well the material fits the gimmick. Variance comes from the people, not the
  form.
- **Improvised:** the performer's improvised skill in that register, partner chemistry in a
  confrontation, and psychology for staying on the rails. Variance again comes from the people.
- **Both can fail, in different ways.** A scripted promo can be wooden: a performer visibly
  reciting lines they cannot sell (doc 19 §3.4: "reciting a script" kills a promo). An improvised
  one can lose the thread or go dead. Neither form carries a built-in bonus, and the flat ×1.15
  and the "scripted never botches" rule are removed.
- **Ceiling and floor are properties of the people.** A great writing team with a great deliverer
  gives the best, most consistent results. A great improviser without a script gives brilliance.
  A poor talker improvising gives disasters, and a poor talker given a script may only be dull.
  None of that is a rule about the form; it falls out of the inputs.

### 3.4 Who decides

The player chooses at three levels (open question 3 asks which to build first):

- a **promotion default** (script everything, script nothing, or by person);
- a **per-wrestler instruction**, e.g. "let them talk" or "script their serious material";
- a **per-segment override** in the segment builder, which already has `IsScripted`.

The builder shows what each choice would get from these people ("X is far better off the cuff"),
in words that sharpen the longer somebody has worked for you, following the existing `Scouting`
pattern.

### 3.5 Legibility

The report says why a promo landed or did not: "Scripted, and it showed — every line sounded
read," or "Handed the microphone, X found the crowd and never let go."

## 4. A character's life: freshness

**Character freshness** (0–1) is the dead `Gimmick.Freshness` field, brought to life.

- **Wears out** with time on television, weighted by exposure. Doc 17 §4.1 gives 6–18 months
  for a new character. The rate is set by durability (already on the roster), fit (a character
  that is really the person lasts longest, doc 19 §2.1) and type.
- **Restored by** absence, evolution, a turn, a new context (a new brand or division, a title
  picture), and a repackage. Each restores a different amount. Doc 17 §4.2 lists the tools.
- **Effect:** scales the gimmick's appeal, and therefore connection. It is shown on the profile
  in words ("fresh", "established", "getting familiar", "has said everything it has to say").
- **Act sameness is a separate thing.** A repackage or turn resets character freshness. It also
  breaks the run of sameness, because the act is genuinely new; that keeps today's `LastGimmick`
  behaviour, but keys it to the whole character, not only the name. A new opponent breaks
  sameness without touching freshness.

The other dead fields:

- `PopularityModifier` is **deleted**, because appeal already does its job (open question 4);
- `GimmickTraits` become the sharpening effects in Part 2;
- `Tone` becomes the promo register.

The fan groups that never appear in a crowd stay unused until markets put them in one.

## 5. Turns

A turn is something the player books, as a segment. The betrayal segment already exists; a turn
makes it flip alignment, and there is a face-turn counterpart (a rescue, or standing up to a
heel). The booked alignment changes because the player chose it.

How much a turn pays depends on:

- **The crowd's current reading.** A heel the crowd has been cheering for months turns face to an
  enormous reaction. Turning somebody against how the crowd already feels is a harder sell. The
  gap the player has been shown (§0A) is the signal. Doc 12 §4 says the window is about
  3–9 months, after which the audience gets tired of asking.
- **The asymmetry.** Heel turns are easier and more reliable; face turns need forgiveness: a
  greater villain, or an act of sympathy (doc 19 §5.3).
- **The moment.** The size of the show, and what is at stake: a title, a partner of years, a
  feud's peak.
- **Frequency.** Turns are rare. Roughly once every 2–4 years per performer is sustainable; more
  often than that and returns shrink, until alignment stops meaning anything (doc 19 §5.4).
- **Comedy.** A comic character's heel turn reads as a joke unless the door has been cleared.

After a turn:

- the alignment flips;
- character freshness is partly restored;
- appeal moves group by group (hardcore fans often follow a cool turn that casual fans resist);
- the turn becomes a feud history tag, so the revenge spot exists to be booked.

Teasing a turn over weeks (seeds, doc 19 §5.2 rule 1) is open question 5.

## 6. Repackage and evolution

**A repackage** changes the core and the presentation: a new name, type, tone and descriptor, and
possibly alignment. It is booked from the profile and debuts with a segment, usually a vignette.

- Character freshness resets.
- Fit is recalculated against the new type. **This is the gamble**, and the main reason fit
  exists.
- The wrestler loses some momentum, because the audience starts over.
- Too many repackages shrink every version (doc 12 §4.5). So does repackaging somebody the crowd
  has rejected *as a person*, which is sustained low overness rather than a stale character.
- The old character goes into `PreviousGimmicks`, with `ChangeGimmick` fixed.

**Evolution** changes presentation, or tone within a type: the Undertaker template of a fixed
core and periodic surface reinvention (doc 19 §6.3). It costs less, restores less and risks less.

**Names.** A new name must not be any real wrestler's ring name or real name. It is checked
against the database, the generator's taken names and `PreviousNames`.

**Real people.** Turning or repackaging a real wrestler changes the *character* inside one save.
`RealName` and the database are untouched, so it stays within CLAUDE.md's rule. *(Owner to
confirm.)*

## 7. Rivals

The rival booker turns and repackages when it makes sense to (decided):

- turn when the gap between booked alignment and the crowd's reading has held for a while;
- repackage when a character is stale and the wrestler is cold;
- evolve somebody whose character is wearing but who is still over.

How often and how well a rival does this follows its booking competence, as everything in
`RivalBooker` already does: a badly run promotion turns people too often or at the wrong time.
The outcome is judged by the same turn model as the player's. Rivals already run promos, so a
rival's promos use the same promo model, and its writing team is part of its promotion. The news
reports a rival's turns and repackages.

## 8. Calibration

- Type weights are centred: averaged over the roster on a typical card, a character's effect is
  about 1.0. Fit for shipped gimmicks starts high and freshness starts from the roster's
  `Freshness` values, so day one of a save draws about what it draws now.
- Anything that moves draw moves `MatchEngine.TypicalInvestment` and
  `BoxOffice.TypicalCardPullFor`. Both are **re-measured, not widened**, in the PR that moves
  them.
- Promo quality is centred too. A typical performer under the typical policy scores about what a
  promo scores today, so segment values on the card (`ShowSimulator`) stay in range.
- Corpus checks use `StableSeed.From(...)`.

## 9. The roster data

- **Promo skill (2×2) for all 124 real people**, judged relative to people already in the file
  and sourced the way `roster-rubric.md` requires, with a rubric section added for it. This is
  the largest piece of data work in the plan.
- Traits reviewed so that they are meaningful, now that they will do something.
- Generated wrestlers draw promo skill from their template, shifted like everything else.
- **Old saves:** load with promo skill derived from charisma and psychology, fit high for the
  gimmick they hold, and freshness from the field they already carry.

## 10. Build order (proposed)

Each step is reachable by the booker and legible in the report before it counts as done
(CLAUDE.md, "A feature the player cannot reach is not finished").

1. **Foundations.**
   - Character freshness lives and is shown.
   - The crowd's reading is shown beside the booked alignment in the profile and picker.
   - `PopularityModifier` is deleted.
   - `ChangeGimmick` is fixed.
   - The doc corrections are made.
2. **Characters on the card** (Part 2): type and trait weights, fit, builder hints, report lines,
   and the comedy door. *The interchangeability fix.*
3. **Promos** (Part 3): the 2×2 data, the creative team, policy at the chosen levels, and the
   rebuilt promo quality.
4. **Turns** (Part 5), with rivals.
5. **Repackage and evolution** (Part 6), with rivals.
6. Later, written down so as not to look finished: managers and mouthpieces (doc 19 §7);
   entrance and music as an investment (doc 19 §8); named writers as staff; motivation as a
   field; chemistry between particular pairs; markets that bring the other fan groups into a
   crowd.

## 11. Open questions

1. **Promo skill shape.** The 2×2 (scripted or improvised against serious or comic), or
   something else?
2. **Writers.** A creative-team figure with a range now and named staff later, or named writers
   from the start?
3. **Who decides scripting.** Promotion default, per wrestler, per segment: all three, or which
   first?
4. **`PopularityModifier`.** Delete it, or keep it as something specific?
5. **Teasing a turn.** A one-segment turn first, or a multi-week setup from the start?
6. **How visible.** Should fit and promo skill be shown in words that sharpen with time (like
   potential), or as numbers?
7. **Real people.** Confirm that turning and repackaging a real wrestler's character inside a
   save is acceptable.
