# 18 — Match Craft

*How a wrestling match actually works: psychology, structure, styles, pacing, and the
difference between good wrestling and a good match.*

---

## 1. What a match is for

A wrestling match is a **story told with bodies**, whose purpose is to deliver on whatever
the build promised. Its jobs, in order:

1. **Pay off the story** the audience was sold
2. **Establish or transfer status** (someone wins)
3. **Advance or conclude the feud**
4. **Entertain in itself**

A match that is athletically extraordinary and does none of the first three is a
performance, not a match. A match that does all three with basic offence is doing its job.
**Both are legitimate; conflating them is the source of most wrestling arguments.**

---

## 2. Ring psychology — the core doctrine

"Psychology" in wrestling means: **every action has a reason, and the audience can follow
the reasons.**

### 2.1 The principles

**Cause and effect.** If you hurt a limb, it stays hurt. If you take a big bump, you don't
pop up. Consequences persist.

**Selling.** The single most important skill and the most under-appreciated. Selling is not
grimacing — it is **making the opponent's offence matter**. A wrestler who sells well makes
their opponent look devastating; a wrestler who doesn't makes everything look harmless.

*The industry term "Ricky Morton" — as in "he's playing Ricky Morton" — is literally the
name of a performer who was so good at extended sympathetic selling that it became the
generic term for the role.* That's how central the skill is.

**Escalation.** The match gets bigger. Early exchanges are basic; later ones are desperate.
Kicking out of a finisher early in a match is a psychology error because it leaves nowhere
to go.

**Struggle.** Nothing should look easy. A move that is executed effortlessly reads as
choreography; a move that is fought for reads as a contest.

**Transitions.** How control changes hands must be **earned**. A reversal out of nowhere is
the most common psychology failure — the audience needs a reason the momentum shifted.

**The false finish.** A near-fall the audience genuinely believed. Requires the finisher to
be protected, the count to be timed right, and the audience to have been given reason to
believe.

**The finish.** Must be decisive, must make sense, and must favour whoever's momentum
justified it. See §5.

### 2.2 Limb work — the classic psychological structure
Attack a body part → the opponent sells it → it limits their offence → the finisher targets
it → the match ends because of it. This is the purest form of wrestling psychology and it
requires **both** wrestlers: the attacker to stay on it, and the seller to keep it hurt for
the entire match.

Its failure is endemic: fifteen minutes of leg work followed by a diving finisher off the
top rope. The audience notices. It notices even if it can't articulate why the match felt
hollow.

### 2.3 The dominant structure: face in peril

```
  SHINE          ── The babyface controls, looks great, crowd is happy
     │
  CUT-OFF        ── The heel takes over, usually via a cheat or a mistake
     │
  HEAT           ── Extended heel control. The longest section. Crowd tension builds.
     │              Rest holds, wear-down, the babyface's brief hope spots.
  HOPE SPOTS     ── Brief comeback attempts that fail. Each raises tension.
     │
  COMEBACK       ── The babyface finally breaks through. Maximum crowd release.
     │
  FINISHING      ── Trading near-falls, finishers, escalation
  STRETCH
     │
  FINISH         ── The end
```

**Why this works:** the heat section builds a tension the comeback releases. The longer and
more frustrating the heat, the bigger the comeback pops. The hope spots are essential —
they keep the audience from giving up during the heat.

This structure is 70+ years old, works in every culture, and is the reason "face in peril"
is in this repo's `MatchStructureLibrary`.

### 2.4 The other structures
- **The even contest / sports match.** Two respected athletes, no clear face/heel, back and
  forth, escalating. NJPW's default. Requires both to be over.
- **The squash.** One-sided, short. Establishes dominance cheaply. Under-used in the modern
  era relative to its efficiency.
- **The brawl.** Little structure, escalating violence, usually a feud blow-off. Emotion
  over technique.
- **The spotfest.** Sequential high spots. Athletic showcase. Weakest psychology, strongest
  immediate reaction, worst long-term returns.
- **The epic.** 25–40 minutes, multiple false finishes, escalating stakes. Reserved for
  the biggest matches; loses its power if used often.
- **The comedy match.** Its own grammar; effective in the right slot, corrosive if the
  performers need to be taken seriously later.

### 2.5 Multi-man matches

Everything above assumes two sides. Most of it still holds with more, but three things break,
and they break in ways that decide whether a multi-man match is good or a mess.

**The problem is the third man.** In a two-side match every second is accounted for: one
person is working, one is being worked. Add a third and somebody is doing nothing. Wrestling's
answer is the **disposal spot** — the third man is put through a table, sent into the steps,
dumped to the floor — and the entire craft of a multi-man match is disposing of people
plausibly and then bringing them back at the right moment. A triple threat that never explains
where the third man went is the format's characteristic failure.

Formats, and what each is for:

- **Triple threat / fatal four-way.** No disqualification, no count-out, first fall wins.
  The rule that matters is that **anyone can be pinned**, which is why these exist: they let a
  champion lose the match without losing cleanly, and they let a challenger win without
  beating the champion. That is a booking convenience with a real cost — see §5.2.
- **Trios.** Three a side, tag rules — and two genuinely different matches wearing one
  name, which is worth separating because the engine treats them differently.

  The **American six-man** is a tag match with more people: the face-in-peril structure
  applies unchanged, and what the third man buys is a deeper heat (three fresh opponents
  rotating on one man, which two a side cannot book) and a fresh man for the finish. It is
  not a novelty format either — NJPW and WWE run six-mans routinely on house and television
  cards, precisely because they get six people onto a card in one slot.

  The **lucha trios** is a different match. Doc [25](25-international-models.md) §3.3 is
  explicit that three-a-side is the *default* in lucha rather than a variant, and that this
  "changes everything": more people on every card, rapid tag rules that allow constant motion,
  story told through the group dynamic, and singles matches made special by contrast. (The
  three-fall tradition is §3.1, not §3.3.)
  There is no long isolation in it and no hot tag to charge — the shape is the opposite of
  the Southern Tag.

  So the honest summary is that trios are not really a *multi-man* match — nobody has to be
  disposed of, because everyone not legal is on the apron by rule — but they are not simply
  "a tag match with more people" either. What the two formats share is that they need
  nothing the two-side engine does not already have.
- **Elimination.** Falls remove people; last one standing wins. Solves the third-man problem
  by construction, which is why it scales where a four-way does not. The drama moves from the
  fall to the *order* of eliminations.
- **The battle royal / Rumble.** Over-the-top elimination. Everyone starts together in a
  battle royal; timed continuous entry is the Rumble variant, and it is the entries that
  make the Rumble a story rather than a scramble. Barely a match:
  a vehicle for a spectacle, a surprise return, and one story told in eliminations. Judged on
  moments, not on work.
- **Handicap.** Two or more against one. Almost never a contest; it is a *statement*, and the
  statement is usually about the lone man's toughness rather than the outcome.

**Crowd attention does not divide evenly.** A three-way between one over performer and two
midcarders is not a three-way — it is the over performer's match with two people in it. The
room follows whoever it came to see, and the sequences that do not involve them are dead air
however well worked. This is the reason a multi-man match is a poor place to elevate somebody:
proximity transfers heat in a tag match ([17](17-heat-and-getting-over.md) §2.8) because the
partners share a story, and it does not here because they are competing for the same attention.

**Protection is the point, and protection is the cost.** The reason to book a multi-man title
match is that the champion can be beaten without being beaten. Used once, that is a genuine
tool. Used repeatedly, the audience learns that title changes do not require winning, and the
belt stops meaning "the best" and starts meaning "whoever got the last pin" — the shortcut
whose cost is invisible per use and enormous in aggregate
([04](04-booking-philosophy.md) §5.1).

**What good ones do:**
- Give each participant a distinct role — the aggressor, the opportunist, the one being
  ganged up on. Undifferentiated three-ways are the worst matches in wrestling.
- Use **temporary alliances** and their betrayal. Two working together against the third is
  the format's single best story, and the moment it breaks is the peak.
- Dispose of people *for a reason*, so the return is a payoff and not a reset.
- Finish where the story pointed. The stealing pin — a third party stealing a fall after two
  others have destroyed each other — is legitimate, and it is legitimate *because* it says
  something about that character. A stolen fall with no character behind it is just a random
  outcome.

**What bad ones do:** rotate through pair-ups with no reason for who is in the ring, leave the
third man on the floor for four minutes with no explanation, escalate everyone equally so
nobody is anybody, and finish on whoever happened to be standing.

---

## 3. Pacing and time

### 3.1 Match length and what it should contain
| Length | Structure |
|---|---|
| **Under 3 min** | A squash or an angle. One idea. |
| **5–8 min** | Shine, cut-off, short heat, comeback, finish. TV standard. |
| **10–15 min** | Full face-in-peril structure with a proper heat section. The workhorse length. |
| **15–25 min** | Adds a second heat/comeback cycle and a real finishing stretch. Big matches. |
| **25–40 min** | Epic structure. Requires two performers with enough over-ness to hold attention and enough conditioning to work it. |
| **40+ min** | Iron man, 60-minute draws. A spectacle in itself; very few can do it. |

**The most common error is a match longer than the story it has to tell.** A ten-minute
match between two people the audience doesn't care about is worse than a four-minute one,
because the audience has time to notice they don't care.

### 3.2 Working the crowd
Great workers adjust in real time. If the crowd is hot, go faster; if flat, simplify and go
to the comeback earlier. This is why "calling it in the ring" matters and why over-scripted
matches feel inert — they can't respond.

### 3.3 The pyramid rule
Everything should escalate. The biggest move is the last one. The loudest reaction is at
the end. A match that peaks in the middle has a dead ending.

---

## 4. Styles

| Style | Characteristics | Ages | Signature promotions |
|---|---|---|---|
| **Technical / mat** | Holds, counters, limb work, grappling | Very well | World of Sport, NJPW, ROH, Bret Hart's lineage |
| **Strong style** | Stiff strikes, fighting-spirit no-selling, realism | Moderately | NJPW, NOAH |
| **Lucha libre** | Fast, aerial, rope-running sequences, trios | Moderately | CMLL, AAA |
| **Powerhouse** | Slams, size, dominance | Very well | Every era |
| **Brawling** | Punches, weapons, crowd, blood | Well | Memphis, ECW, Attitude |
| **High-flying** | Dives, top-rope offence, risk | **Badly** | Every era's junior/cruiser divisions |
| **Hardcore / deathmatch** | Weapons, blood, extreme spots | **Catastrophically** | ECW, FMW, CZW, GCW |
| **Sports entertainment** | Character-driven, moderate athleticism, TV-optimised | Well | WWE |
| **Puroresu junior** | Fast, technical, high-flying hybrid | Moderately | NJPW junior division, Dragon Gate |
| **Southern/territory** | Psychology-heavy, slow builds, heat and comeback | Very well | Mid-South, Crockett, Memphis |

### 4.1 Style clash
Two performers from incompatible styles can produce a bad match despite both being
excellent. The classic clashes: mat technician vs pure brawler, high-flyer vs immobile
powerhouse, comedy worker vs serious worker.

**The mitigation is adaptability** — the best workers can work anyone's style, which is why
adaptability is a genuine and separate attribute (see [03](03-roles-and-competence.md) §5.1).

### 4.2 Style and era
What "a good match" means changes:
- **1970s:** long, slow, hold-based, psychology-dominant. A 30-minute match with six moves.
- **1980s:** character-driven, shorter, TV-optimised, spectacle over technique
- **1990s:** the workrate revolution — cruiserweights, ECW, All Japan's King's Road epics
- **2000s:** the "moves" era; escalating spot density
- **2010s–2020s:** extremely high athletic baseline; move-heavy; criticism of "no selling"
  and finisher inflation

**Finisher inflation** is a real, measurable trend: the number of finishers kicked out of
per main event has risen steadily, which devalues each one. This is the athletic equivalent
of currency debasement and it happens because each match must top the last.

---

## 5. Finishes

The most consequential 30 seconds of a match.

### 5.1 Finish types and what they mean

| Finish | Message | Cost |
|---|---|---|
| **Clean pin** | The winner is better | Costs the loser status |
| **Submission** | Total dominance | Highest status transfer; costs the loser most |
| **Roll-up / flash pin** | A fluke; the loser wasn't beaten | Protects the loser; devalues the win |
| **Interference** | The heel needed help | Protects both; escalates the feud; devalues the finish |
| **DQ** | No resolution | Protects both; frustrates the audience |
| **Count-out** | No resolution | Same, weaker |
| **Ref bump + something** | Chaos | Overused to the point of parody |
| **Referee stoppage** | Devastating dominance | Rare; very strong; use sparingly |
| **Draw / time limit** | Equals | Powerful once; infuriating twice |
| **Finisher kick-out then a second finisher** | The winner had to go beyond | Escalates but inflates |

### 5.2 The rules of a good finish
1. **The momentum must favour the winner.** If the loser was dominating and then loses out
   of nowhere, the finish is unearned. *(This repo's engine implements this directly — an
   unearned finish costs 45% of its technical and storytelling contribution. That's the
   right mechanic.)*
2. **It should be the biggest thing in the match.**
3. **It should be legible.** The audience must know instantly what happened.
4. **It should be consistent** with what has beaten this person before.
5. **It should serve the next thing** — a finish that closes the feud, or opens the next.

### 5.3 The protected finisher
A finisher that reliably ends matches is a promotion's most valuable narrative currency. It
means:
- Every time it's hit, the audience believes the match is over
- A kick-out is genuinely shocking
- The performer using it has a credible path to victory in every match

**Protecting it costs discipline:** it must not be kicked out of casually, must not be used
in throwaway matches, and must not be hit five times in one match.

---

## 6. Safety and the physical reality

- **The performer taking the move controls their own safety** as much as the one giving it.
  Wrestling is cooperative; a bad bump is usually a communication failure.
- **Certain moves are inherently high-risk**: piledrivers and their variants (banned or
  restricted in several promotions and in Mexico by commission rule), unprotected chair
  shots to the head (banned by WWE from 2007), top-rope moves onto the floor, anything
  involving the neck landing first.
- **Blood** ("colour", "juice") — historically via blading (a concealed razor cut).
  Restricted at WWE since the PG era; still used elsewhere. It has a genuine and powerful
  effect on perceived stakes, at real medical risk.
- **The ring itself** is a variable. A ring with more give is safer; a hard ring produces
  more impact noise and more injuries.

---

## 7. What makes a match rate well vs draw well

These are different and the distinction matters:

| A match rates well when | A match draws well when |
|---|---|
| Execution is clean and difficult | The audience wanted to see it |
| Psychology is coherent | The stakes were clear |
| The story within the match is told | The build made it feel necessary |
| The finish is earned | The people in it are over |
| Both performers are skilled | Both performers are over |

**A five-star match between two people nobody cares about draws nothing.** A three-star
match at the end of a hot year-long feud sells out a stadium. Promotions optimise for the
second and the internet grades the first, which is the entire critical argument in
wrestling in one line.

The correct synthesis: **story sells the ticket; quality sells the next ticket.**

---

## 8. The star rating tradition

- Popularised by Dave Meltzer in the *Wrestling Observer Newsletter*, using a 0–5 scale
  (later extended above 5, controversially)
- **It measures match quality as a performance**, explicitly not drawing power
- It has become culturally influential enough to affect booking — performers chase ratings
- **The criticism:** the scale rewards length, difficulty, and near-fall density, which
  incentivises exactly the escalation that damages long-term match value

This repo's 0–5★ output sits in that tradition, and its three-component model
(technical / storytelling / crowd) is a good structural improvement on a single number,
because it separates the axes that the single number conflates.

---

## 9. Common match-craft failures

| Failure | Symptom |
|---|---|
| **No selling** | Big move, immediate recovery. Nothing means anything. |
| **Wasted limb work** | Fifteen minutes of leg work, unrelated finish |
| **Move-for-move** | Sequences with no logic between them; the audience sees choreography |
| **Peaking early** | The biggest spot is in the middle; the finish is smaller |
| **Too long** | The story ran out before the time did |
| **Too many finishers** | Nothing is a finisher any more |
| **The unearned reversal** | Control changes hands for no visible reason |
| **The dead crowd ignored** | The performers work their planned match while the room sits silent |
| **The finish that contradicts the story** | The person who dominated loses out of nowhere |
| **Both performers doing the same thing** | No contrast; nothing to root for |
| **The unexplained third man** | In a multi-man match, somebody is on the floor for minutes with no reason given |

---

## Sim implications

The engine already implements a great deal of this. Notes on gaps:

- **Selling as a beat-level property** exists (`Selling` drives how good the opponent's
  offence looks). The next step is **persistent damage**: a limb targeted early should
  modify later beats' effectiveness, and failing to sell it should cost storytelling score.
  That single change would make limb-work psychology a real mechanic instead of a beat type.
- **Finisher protection as a promotion-level variable.** Every kick-out of a finisher
  should slightly reduce the credibility of all finishers, decaying over time. This
  reproduces finisher inflation and makes near-fall spam self-limiting in the right way.
- **Style clash** between two performers' `WrestlingStyle` values should modify match
  quality, with an `Adaptability` attribute that mitigates it.
- **Escalation checking:** the engine could verify that beat intensity trends upward and
  penalise a match that peaks in the middle. §3.3's pyramid rule is a clean scoring rule.
- **Match length vs story** — a length appropriateness check against feud heat and
  performer over-ness would formalise "a match longer than the story it has to tell".
- **Rate well vs draw well as two separate outputs** (§7) is the highest-value addition
  in this document: the sim should report a **quality rating** and a **business result**,
  and they should be able to diverge sharply. That divergence is what wrestling is.
- **Multi-man matches** (§2.5) need four things the two-side engine does not have, and the
  order matters:
  1. **A per-side advantage reading.** The engine's `Advantage` runs −100 to +100, which is
     inherently two-poled: it can say who is on top of a two-sided match and cannot say
     anything at all about a three-sided one. Three sides need an array. This is a
     data-shape change and nothing more — it is a hard prerequisite, and it is cheap.
  2. **Attention as a scarce resource.** The separate and larger point: in a multi-man match
     the interesting quantity is not who is winning but **who the room is watching**. The
     reaction vector ([16](16-crowd-psychology.md) §2, A5 in [31](31-sim-mapping.md)) is what
     would let the engine read that. It is not a prerequisite — a triple threat could ship
     with a three-pole advantage and no reaction vector — it is the difference between a
     format that works and one that is worth booking.
  3. **Presence, so that being out of the ring is modelled.** A disposal spot should remove
     someone for a bounded stretch and their return should be worth something. Without this
     there is no way to distinguish a well-worked three-way from a badly-worked one, because
     the difference is entirely in how people leave and come back.
  4. **A protection cost at the promotion level.** Winning a belt without beating the
     champion is a real tool with an aggregate price, and it has the same shape as
     interference and non-finishes — cheap once, corrosive repeated.

  Trios are the exception and need none of it: three a side is still two sides, and both
  formats above run on the tag machinery the engine already has. Shipped — see
  `Six-Man War` and `Lucha Trios` in `MatchStructureLibrary`. The one thing the engine
  deliberately does *not* have is a term for headcount: a side is read from its members, so
  a third man is worth exactly what the booking gives him to do, and a structure that never
  tags him in has added a name to the card and nothing else.
