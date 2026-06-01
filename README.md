# WrestlingSim — Booking System

A console-based professional wrestling booking simulator written in C# (.NET 8). You play the booker: assemble a roster, build feuds, design match plans beat by beat, and run the show. The engine scores every match on technical quality, storytelling, and crowd reaction — then gives you a star rating.

---

## Getting Started

**Prerequisites:** .NET 8 SDK

```bash
git clone https://github.com/Muscles4817/WrestlingSim
cd WrestlingSim
dotnet run --project WrestlingSim
```

**Run the tests:**
```bash
dotnet test
```

---

## Main Menu

```
  ╔══════════════════════════════════════════════════════╗
  ║          ★  W R E S T L I N G   S I M  ★            ║
  ║               B O O K I N G   S Y S T E M            ║
  ╚══════════════════════════════════════════════════════╝

  [ 1 ]   Book a Match
  [ 2 ]   Book a Show
  [ 3 ]   View Wrestlers
  [ 4 ]   Exit
```

---

## Booking a Match

Selecting **Book a Match** walks you through a five-step flow:

### 1 — Pick your wrestlers
Numbered list of the loaded roster showing Popularity, Skill, and Charisma.

### 2 — Set the match type
| Type | Effect |
|---|---|
| Standard | Balanced scoring |
| Technical | Technical score ceiling raised |
| Storytelling | Storytelling score ceiling raised |
| Spotfest | High-spot contributions amplified |

### 3 — Set up a feud (optional)
An active feud unlocks additional beat types and multiplies their impact.

| Intensity | Energy Bonus | Score Multiplier |
|---|---|---|
| Cold | +3 | ×1.05 |
| Building | +7 | ×1.15 |
| Hot | +12 | ×1.30 |
| Nuclear | +18 | ×1.50 |

You can also attach **history tags** (Betrayal, InjuryAngle, TitleStolen, FamilyInvolved, ManagerConflict, etc.) that gate certain beats.

### 4 — Choose a match structure
Pick one of seven pre-built structures as your starting plan, or build from scratch.

| Structure | Beats | Description |
|---|---|---|
| **TV Formula** | 4 | Weekly TV bread-and-butter. Opening → Heat → Comeback → Finish. |
| **Face-in-Peril** | 5 | The Hogan/Cena formula. Face dominates, gets cut off, long heat, big pop comeback. |
| **Technical Showcase** | 7 | Bret/HBK/Benoit psychology. Mat work, limb targeting, submission payoff. |
| **Spotfest** | 7 | High spots carry the match. Two aerial sequences, minimal psychology. |
| **Grudge Brawl** | 6 | Ringside chaos and revenge. Works without a feud, better with one. |
| **Feud Blowoff** | 8 | The definitive end to a rivalry. Requires an active feud at Building+. |
| **Big Match Epic** | 9 | WrestleMania main event structure. Slow build to a defining finish. |

### 5 — Edit in the Beat Editor

```
  ╔══════════════════════════════════════════════════════╗
  ║                    BEAT EDITOR                        ║
  ╚══════════════════════════════════════════════════════╝
  The Rock  (A)     vs     Hulk Hogan  (B)

    #   TYPE                      CONTROL
    ──────────────────────────────────────────────────────
    1   Hot Opening               Even
    2   Heat Segment              B — Hulk Hogan
    3   Crowd Brawl               Even
    4   Third Party Pull-In       Even
    5   Goes It Alone             B — Hulk Hogan
    6   Comeback                  B — Hulk Hogan
    7   Near Fall                 B — Hulk Hogan
    8   Finish: Clean             B — Hulk Hogan
    ──────────────────────────────────────────────────────

  [A]dd   [R]emove   [C]hange control   [G]o
```

- **Opening beats** are highlighted cyan, **finish beats** yellow.
- **Add** inserts a new beat before the finish. You pick from the full Beat Library, filtered by your feud state.
- **Remove** deletes a beat. You cannot remove the only opening or the only finish.
- **Change control** reassigns who is driving the action on any beat.
- **Go** validates the plan. If there are errors (e.g. a feud-gated beat with no feud set), they are listed and the editor reopens.

---

## The Beat Library

Every beat in the editor is drawn from a library of named archetypes. Each template has default intensity and duration — both can be overridden.

### Openings
| Template | Type | Default |
|---|---|---|
| Hot Start | HotOpening | High / Short |
| Feeling-Out Process | SlowOpening | Low / Medium |
| Standard Collar-and-Elbow | StandardOpening | Medium / Short |

### Control
| Template | Type | Default |
|---|---|---|
| Power Beatdown | HeatSegment | High / Medium |
| Technical Dissection | HeatSegment | High / Long |
| Methodical Grind | HeatSegment | Medium / Long |
| Suplex Run | HeatSegment | High / Medium |
| Explosive Flurry | HeatSegment | Extreme / Short |

### Comebacks
| Template | Type | Default |
|---|---|---|
| Hot Comeback | Comeback | High / Short |
| Fighting Spirit | Comeback | Extreme / Short |
| Slow Burn Rally | Comeback | Medium / Medium |

### Rest Holds
| Template | Type | Default |
|---|---|---|
| Wear-Down Hold | RestHold | Low / Long |
| Strategic Ground Work | RestHold | Low / Medium |

### Spots
| Template | Type | Default |
|---|---|---|
| Aerial Assault | HighSpot | High / Brief |
| Jaw-Dropper | HighSpot | Extreme / Brief |
| Ringside Brawl | CrowdBrawl | Medium / Short |
| Full-Crowd War | CrowdBrawl | High / Long |

### Near Falls
| Template | Type | Default |
|---|---|---|
| Signature Cover | NearFall | High / Brief |
| Shock Kickout | NearFall | Extreme / Brief |
| Counter Roll-Up | NearFall | Medium / Brief |

### Storytelling
| Template | Type | Notes |
|---|---|---|
| Mind Games | PsychologicalWarfare | Charisma-driven |
| Trash Talk | PsychologicalWarfare | Higher story contribution |
| Revenge Spot | RevengeSpot | Callback to earlier in the feud |
| Feud Erupts | FeudalEscalation | Requires feud: Building+ |
| Outside Party | ThirdPartyPullIn | Requires feud tag: FamilyInvolved or ManagerConflict |
| Goes It Alone | AlliesRejected | Must follow an Outside Party beat |

### Finishes
| Template | Type |
|---|---|
| Clean Victory | FinishClean |
| Dominant Statement | FinishSuperFinisher |
| Roll-Up Steal | FinishRollup |
| Tap Out | FinishSubmission |
| Dirty Win | FinishInterference |
| DQ Finish | FinishDQ |
| Count-Out | FinishCountout |

---

## The Rating System

After execution the engine produces a star rating (0–5★) from three components:

| Component | Weight | Driven by |
|---|---|---|
| Technical | 35% | Ring skills, match type, pace of beats |
| Storytelling | 30% | Feud intensity, beat sequencing, psychology |
| Crowd | 35% | Crowd energy arc across the match |

A **finish quality** check adds or subtracts up to 5 points: if momentum doesn't favour the booked winner when the finish lands, the rating is penalised.

**Crowd energy mechanics:**
- Starts based on average popularity and a "both-over bonus" if both wrestlers have strong crowd dispositions.
- Decays 3% naturally between every beat.
- Comebacks from deep momentum deficits earn a larger pop (`earnedBonus` scales with deficit).
- Near-falls have diminishing returns — each successive kickout in the same match hits slightly softer (×0.85 per near-fall).

**Crowd disposition** is not simply alignment. It is calculated from popularity and per-fan-group `AppealRatings` on the wrestler's gimmick. A heel with massive nostalgia appeal (e.g. Hulk Hogan at WrestleMania X8) reads as high-disposition and gets massive reactions — exactly as the Toronto crowd gave him.

---

## Wrestlers

Wrestlers are loaded from `Wrestlers.json` at startup. Each wrestler has:

**Ring Skills** (1–5 each)
- HighFlyer, Grappler, Powerhouse, Technical, Brawler, Striker

**Mental Attributes** (0–100)
- Psychology, Selling, RingIQ, Toughness

**Gimmick**
- Name, Type (Monster, Anti-Hero, Showman, etc.), Tone (Serious, Comedic, Cryptic…)
- Alignment (Face / Heel / Tweener)
- Fan Group Appeal ratings — determines crowd disposition independently of alignment
- Freshness (0.0–1.0, decays with use)

**Other**
- Popularity (0–100), Charisma (0–5), Wrestling Style, Moveset, Signatures

---

## Booking a Show

**Book a Show** lets you assemble a full card of matches and promo segments, then simulates the whole show and returns an overall rating with a per-match/segment breakdown.

---

## Project Structure

```
WrestlingSim/
├── Engine/
│   ├── MatchEngine.cs          — Executes a MatchPlan, returns MatchEngineResult
│   ├── MatchEngineState.cs     — Mutable crowd energy + momentum state during execution
│   ├── BeatLibrary.cs          — Catalogue of all named beat templates
│   ├── MatchStructureLibrary.cs — Seven preset match structures
│   ├── MatchSimulator.cs       — Legacy match simulator
│   ├── ShowSimulator.cs        — Full show execution
│   └── SegmentSimulator.cs     — Promo / segment execution
├── Models/
│   ├── Wrestler.cs             — Core wrestler model
│   ├── Gimmick.cs              — Character, alignment, fan appeal
│   ├── RingSkills.cs           — Six-skill matrix + scoring
│   ├── MatchPlan/
│   │   ├── MatchPlan.cs        — The booker's plan: wrestlers + beats + feud
│   │   ├── MatchBeat.cs        — A single beat (type, control, intensity, duration)
│   │   ├── BeatTemplate.cs     — Named, reusable beat archetype
│   │   ├── MatchStructure.cs   — Named preset beat sequence
│   │   ├── Feud.cs             — Feud state between two wrestlers
│   │   ├── FeudalResonance.cs  — Contextual feud amplifier on individual beats
│   │   ├── BeatResult.cs       — Engine output for a single beat
│   │   └── MatchEngineResult.cs — Full match output with star rating
│   └── Person/
│       ├── MentalAttributes.cs
│       └── PhysicalAttributes.cs
├── Enums/
│   ├── BeatEnums.cs            — BeatType, BeatControl, BeatIntensity, BeatDuration
│   ├── FeudEnums.cs            — FeudIntensity, FeudHistoryTag, FeudalResonanceType
│   └── ...
├── UI/
│   ├── MainMenu.cs             — Main menu rendering
│   └── MatchBookingFlow.cs     — Full guided match-booking UI
├── Program.cs
└── Wrestlers.json              — Roster data

WrestlingSim.Tests/
├── MatchEngineTests.cs         — Match engine tests incl. real-world match recreations
├── BeatLibraryTests.cs         — Beat library catalogue and round-trip tests
└── MatchStructureLibraryTests.cs — Structure preset tests
```

---

## Design Notes

**Why beats instead of a probability roll?** A single-roll match simulator can tell you who won. A beat-based simulator can tell you *how* it went — which is what wrestling actually is. The booker controls the narrative; the engine scores how well it landed with the crowd.

**The "cool heel" problem is modelled.** When a high-disposition heel (e.g. Brock Lesnar) controls the heat segment, `tensionFactor` is low because the crowd isn't generating "save the face" heat. This is why Roman vs Brock WM34 rates around ★★★ even with objectively good execution — the crowd never invested in the story the booker was trying to tell.

**Near-falls have diminishing returns by design.** The third Shock Kickout in a match will never land as hard as the first. If you want to maximise a near-fall sequence, save your biggest one for last.

**The finish must be earned.** If momentum doesn't favour the booked winner when the finish lands, the rating takes a 45% penalty to finish quality. Book your match so the right person has the advantage going into the finish.

---

## Running Tests

```bash
dotnet test
```

The test suite includes recreations of real-world matches to validate the engine:

- **Goldberg vs Brock — WM20** — Should rate ★¼ (crowd checked out, both turned)
- **Roman vs Brock — WM34** — Should rate ★★★¼ (cool heel problem, no crowd investment)
- Both matches are also re-booked with different beat sequences to demonstrate how a better plan changes the rating.
- **Taker vs HBK WM25** beat coverage verified against the full 14-beat match structure.
