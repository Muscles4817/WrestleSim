# Adding real wrestlers — the rubric (agreed 15 Nov 2026)

`WrestlingSim.Core/JSON/Wrestlers.json` holds real people. Every new entry is scored on the same
scale as the ninety already there, so the anchors below are people already in the file. Agreed
with the owner before the first research batch.

**Facts are looked up, never remembered. Judgements are placed against the people already here.**

## The rules

1. **Facts need a source.** Real name, ring name, birth year, debut year, division, finisher names,
   build. Each comes from a public source (Wikipedia, Cagematch, the promotion's own profile) and
   the source is listed. A fact that cannot be confirmed is left out — `BornIn` may be `null` — and
   never guessed. A real person is never given an invented fact (CLAUDE.md).
2. **Judgements are relative.** Abilities, charisma and fame are placed between two people already
   in the file ("a better technician than X, not as good as Y"), using public signals — Cagematch
   match ratings and reputation for ring work, card position and championships for standing.
3. **As of 2026**, the game's baseline year (`AgeCurve.RosterBaselineYear`).
4. **Keep the scale honest.** Very few people sit at 95+ in fame or charisma; most wrestlers are
   good at one or two ring skills and ordinary or weak at the rest.
5. **Nobody twice.** Check the file (real names, ring names, previous names) before adding anyone.

## Scales

The game shows everything 0–100. In the JSON, ring skills and charisma are **0–5** (shown ×20);
everything else is 0–100 as stored. Below, the 0–100 figure first and the JSON value in brackets.

### Fame (`Overness`, 0–100)

**Fame belongs to the person, not to where they work now** (owner, 15 Nov). How well the wrestling
audience as a whole knows and cares about them, across every company. So:

- A former national-TV star now working the independents keeps much of their name — a Matt
  Cardona is in the 55–65 band, not the indie bands below.
- Somebody can build a big name on the independents alone; score the name, not the venue.
- Somebody who arrives already famous from outside wrestling (a Ronda Rousey) starts high on day
  one, whatever their ring skill.

| Fame | Meaning | Anchors in the file |
|---|---|---|
| 95–100 | Known well beyond wrestling; the biggest names alive | Roman Reigns 96, Becky Lynch 95 |
| 85–94 | Main-eventer of a major company; casual fans know them | Cody Rhodes 93, Charlotte Flair 92 |
| 70–84 | Established upper card; champion level on national TV | Chad Gable 70 |
| 50–69 | Regular TV midcarder viewers recognise; a big independent name | file median 60 |
| 35–49 | Lower card or developmental; known to weekly viewers | |
| 20–34 | Barely featured; known mostly to hardcore fans | Von Wagner 23 |
| under 20 | Local and regional talent | |

### Ring skills (`RingSkills`: HighFlyer, Technical, Powerhouse, Striker, Brawler, Grappler)

| Shown (JSON) | Meaning | Anchors |
|---|---|---|
| 90–100 (4.5–5.0) | Among the best in the world at it | Ricochet's flying 100 (5.0), Gunther's striking 98 (4.9) |
| 75–89 (3.75–4.45) | Excellent | |
| 60–74 (3.0–3.7) | Good, solid | file median ≈ 62 (3.1) |
| 45–59 (2.25–2.95) | Competent but limited | |
| 30–44 (1.5–2.2) | Weak | |
| under 30 (under 1.5) | A real gap | Gunther's flying 24 (1.2) |

The best all-rounders in the file average about 80 (Damian Priest 81, Gunther 78).

### Charisma (`Charisma`, JSON 0–5)

| Shown (JSON) | Meaning | Anchors |
|---|---|---|
| 95–100 (4.75–5.0) | Elite talker | LA Knight 98 (4.9), Seth Rollins 96 (4.8) |
| 85–94 (4.25–4.7) | Strong | |
| 70–84 (3.5–4.2) | Good | file median 70 (3.5) |
| 55–69 (2.75–3.45) | Serviceable | |
| 40–54 (2.0–2.7) | Weak | |
| under 40 (under 2.0) | Keep them off the microphone | Von Wagner 32 (1.6) |

### Craft (`Mental`: Psychology, Selling, RingIQ)

90+ ring general (Randy Orton 96 / 90 / 94) · 80s main-event craft · 70s solid professional (file
median 74) · 60s developing · 50s green · under 50 rookie. Craft rises with years in the business:
a 22-year-old rarely belongs above the 60s whatever their athleticism.

### Body (`Physical`, `Mental.Toughness`)

| Attribute | Range in the file | Anchors |
|---|---|---|
| Strength | 44–96 | 90+ elite powerhouse — Otis 96 |
| Speed, Agility | 42–97 | 90+ elite flyer — Ricochet, Rey Fenix |
| Stamina | 62–90 | 85+ goes long — Bron Breakker 90 |
| Toughness | 54–94 | 90+ famously hard — Gunther 94 |
| Size (1–5) | 1–5 | 1 cruiserweight / small woman, 3 average, 5 giant — Gunther, Otis, Piper Niven |

## The entry

Exactly the shape of the existing entries (see Chad Gable or Rhea Ripley in the file). Allowed
values:

- `Division`: `Mens`, `Womens`
- `Style`: `HighFlyer`, `Grappler`, `Powerhouse`, `Technical`, `Brawler`, `Striker`
- `Gimmick.Type`: `Monster`, `Underdog`, `AntiHero`, `Rebel`, `CultLeader`, `Showman`, `Realist`,
  `Comedy`, `Traditionalist`, `Loner`, `Superstar`, `Egotist`, `DarkPersona`, `Sellout`, `Cryptic`,
  `Daredevil`, `Gimmickless`
- `Gimmick.Tone`: `Serious`, `Humorous`, `Cryptic`, `Arrogant`, `Inspirational`, `Melancholic`,
  `Violent`, `SelfRighteous`, `Chaotic`
- `Gimmick.NaturalAlignment`: `Face`, `Heel`, `Tweener` (their alignment in 2026)
- `Gimmick.Durability`: `ShortTerm`, `MediumTerm`, `LongTerm`
- `Gimmick.Freshness`: 0–1; `PopularityModifier`: about −5 to +10 like the file
- `AppealRatings[].Group`: `Casual`, `Hardcore`, `Families`, `Kids`, `International`, `Online`;
  `AppealScore` 0–1
- Moves: `Types` from `Takedown`, `Throw`, `Slam`, `Submission`, `Strike`, `Aerial`, `Technical`;
  `Risk` / `Impact` from `VeryEasy`, `Easy`, `Medium`, `Hard`, `VeryHard`
- `Signature`: their real finisher(s) — the name the finisher is known by, and the move it is.
  `Overness` of a signature 0–100 (how over the move is).
- `PersonaDescriptor`: a short descriptor of the character as presented (an existing nickname
  where they have one).
- Two `Moveset` moves and one or two `Signature` entries, like the file.

Every new entry gets an explicit `"Id"`: the real name lower-cased with hyphens
(`"Id": "charles-betts"`), so saves stay stable.

## After each batch

- Check: schema loads, no duplicates, a few facts spot-checked against the sources, the batch's
  spread reported against the file's.
- Re-measure `MatchEngine.TypicalInvestment` and `BoxOffice.TypicalCardPullFor` (CLAUDE.md) —
  update the constants, never widen the tests.
