# WrestlingSim — Booking System

A professional wrestling booking simulator written in C# (.NET 8), playable in the browser
or the terminal. You play the booker: build feuds with segments, design match plans beat by
beat, and assemble the card. The engine scores every match on technical quality,
storytelling and crowd reaction — then gives you a star rating.

The engine lives in `WrestlingSim.Core` and is shared by both front ends, so the browser
build runs the exact same simulation as the console app rather than a reimplementation.

---

## Getting Started

**Prerequisites:** .NET 8 SDK

There are two front ends over one engine — a browser UI and the original terminal app.

### In VS Code

Open the folder and press **F5**. The default launch configuration starts the web UI on
<http://localhost:5080> and opens your browser.

Other configurations in the Run and Debug panel:

| Configuration | What it does |
|---|---|
| **▶ Run Web UI** | Dev server + browser. No extensions required. |
| **▶ Run Web UI (debug C#)** | Same, with C# breakpoints in the browser. Needs the C# Dev Kit. |
| **▶ Run Web UI (hot reload)** | `dotnet watch` — edits refresh the page automatically. |
| **▶ Run Console App** | The terminal version. |
| **▶ Run Tests** | The xUnit suite. |

`Ctrl+Shift+B` builds. **Terminal → Run Task…** also has *web: publish* and
*web: preview published site*, which serves the exact static output Pages will host.

### From the command line

```bash
git clone https://github.com/Muscles4817/WrestlingSim
cd WrestlingSim

dotnet run --project WrestlingSim.Web   # browser UI on http://localhost:5080
dotnet run --project WrestlingSim       # terminal UI
dotnet test                             # 85 tests
```

---

## Deploying to GitHub Pages

The web build is Blazor WebAssembly, so it publishes to plain static files and needs no
server. [`.github/workflows/deploy-pages.yml`](.github/workflows/deploy-pages.yml) runs
the tests, publishes, and deploys on every push to `main`.

To turn it on: **Settings → Pages → Build and deployment → Source: GitHub Actions**.

The workflow handles the three things that otherwise break a Blazor app on Pages:

- rewrites `<base href="/">` to `/<repo>/`, since project sites are served from a sub-path
- adds `.nojekyll`, or Jekyll strips the `_framework` directory
- copies `index.html` to `404.html` as a single-page-app fallback

The roster is compiled into the engine assembly as an embedded resource rather than
fetched, so the app behaves identically at any sub-path. Payload is roughly 6 MB
uncompressed on first load, then cached.

---

## Main Menu

```
  ╔══════════════════════════════════════════════════════╗
  ║          ★  W R E S T L I N G   S I M  ★            ║
  ║               B O O K I N G   S Y S T E M            ║
  ╚══════════════════════════════════════════════════════╝

  [ 1 ]   Book a Match
  [ 2 ]   Book a Segment
  [ 3 ]   Book a Show
  [ 4 ]   View Wrestlers
  [ 5 ]   View Feuds
  [ 6 ]   Exit
```

---

## The Booking Loop

The three booking flows feed one shared **feud book** that persists for the session:

```
   Book a Segment ──► deposits heat + history tags ──► Feud
                                                        │
                        raises intensity, unlocks beats │
                                                        ▼
   Book a Match  ◄── reads the feud automatically ──  Blowoff
        │
        └──► deposits more heat + PriorMatch ──► back to the Feud
```

You do not declare that a betrayal happened — you **book** the betrayal, and the tag
gets stamped. Three angles between two wrestlers is roughly enough to reach Building,
which unlocks the feud-gated beats in the match editor.

---

## Booking a Match

Selecting **Book a Match** walks you through a five-step flow:

### 1 — Pick your wrestlers
Numbered list of the loaded roster showing Popularity, Skill, and Charisma.

### 2 — Set the match type
Standard, Technical, Storytelling or Spotfest.

The match type is a real commitment, not a label. It does two things:

1. **Shifts the component weights.** A Technical match is graded mostly on ring work
   (46/24/30); a Spotfest mostly on crowd reaction (30/22/48); Storytelling on story
   (24/42/34). Standard stays balanced at 35/30/35.
2. **Grades your plan against what you advertised.** Each type has a set of on-type
   beats. Book a mat-based plan and call it Technical and you are paid up to +8 points;
   book a brawl and call it a technical classic and you lose up to 8. Standard promises
   nothing specific, so it is neutral — the safe choice, never the free one.

### 3 — The feud
The booker reads the feud these two have actually built. If they have history, it is
shown and used automatically; if they have none you can still declare one by hand,
and that declaration is written back into the feud book so later segments build on it.

Intensity is **derived from accumulated heat**, not chosen:

| Intensity | Heat required | Energy Bonus |
|---|---|---|
| Cold | 5 | +3 |
| Building | 15 | +7 |
| Hot | 30 | +12 |
| Nuclear | 50 | +18 |

**History tags** (Betrayal, InjuryAngle, TitleStolen, FamilyInvolved, ManagerConflict, …)
are stamped by the segments that earn them and gate certain beats:

| Beat | Requires |
|---|---|
| Feud Erupts | Feud at Building or higher |
| Outside Party | Tag `FamilyInvolved` or `ManagerConflict` |
| Goes It Alone | An Outside Party beat earlier in the match |

`Feud.IntensityMultiplier` (×1.05–×1.50) reaches the ring three ways:

- An explicit `FeudalResonance` on a beat pays the full multiplier.
- **Implicit resonance:** beats that are *about* the rivalry — Feud Erupts, Revenge Spot,
  Outside Party, Mind Games, Trash Talk, Goes It Alone — draw 70% of the multiplier
  automatically, with no hand-authored resonance object required.
- At Hot or above, the bad blood bleeds into near-fall drama as well.

Feud intensity also still pays off through starting crowd energy and beat unlocks.

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

  [A]dd   [R]emove   [C]hange control   [I]ntensity   [G]o
```

- **Opening beats** are highlighted cyan, **finish beats** yellow.
- **Add** inserts a new beat before the finish. You pick from the full Beat Library, filtered by your feud state.
- **Remove** deletes a beat. You cannot remove the only opening or the only finish.
- **Change control** reassigns who is driving the action on any beat.
- **Intensity** overrides a beat's intensity and duration away from the template defaults.
- **Go** validates the plan. If there are errors (e.g. a feud-gated beat with no feud set), they are listed and the editor reopens.

The editor also shows the plan's estimated runtime, which is what a match spends
against the show's budget when it goes on a card.

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

After execution the engine produces a star rating (0–5★) from three components, weighted
by the declared match type (Standard shown):

| Component | Weight | Driven by |
|---|---|---|
| Technical | 35% | Ring skills for the style being worked, the opponent's selling, ring psychology |
| Storytelling | 30% | Connection, psychology, feud intensity, beat sequencing |
| Crowd | 35% | Crowd energy arc, capped by how much this audience cares about these two |

Three adjustments then apply:

- **Finish quality**, ±10 points. If momentum doesn't favour the booked winner when the
  finish lands, the finish is *unearned* and its technical and storytelling contributions
  are cut by 45%.
- **Variety**, ±5 points. Measured as distinct beat types over total beats. A four-beat
  match using four types is fully varied; a twelve-beat match using four types is not.
- **Type coherence**, ±8 points, as described under *Set the match type* above.

### The performer model

Every wrestler resolves to a set of factors before the bell, each centred on 1.00 for a
competent main-roster regular. Beat handlers multiply by the factors relevant to what the
beat is doing, so two wrestlers with the same overall skill produce different matches.

| Factor | From | Drives |
|---|---|---|
| Connection | Popularity, gimmick appeal, **Charisma** | Crowd reaction on every beat, and the crowd ceiling |
| Workrate | Ring skill for the style being worked | Technical score |
| RingPsych | Psychology, **RingIQ** | Storytelling, whether the match hangs together |
| Selling | **Selling** | How good the *opponent's* offence looks |
| Resilience | **Toughness** | Near-fall credibility |
| Conditioning | **Stamina** | Late-match fade; long matches punish poor conditioning |
| Athleticism | **Agility**, **Speed** | High spots, comebacks, hot openings |
| Power | **Strength**, **Size** | Powerhouse and brawling offence, super-finishers |

The bolded stats were read zero times by the match engine before this model existed.

**Crowd energy mechanics:**
- Starts from average popularity, a "both-over bonus", and the pair's Connection.
- **Has a per-pairing ceiling.** Two people the audience is not invested in cannot reach a
  main-event reaction no matter how the match is booked. Craft raises the ceiling a little;
  it cannot substitute for the crowd already caring who you are. Asking for a long match
  from two poorly-conditioned wrestlers lowers it further.
- Gains compress as the crowd approaches its ceiling — the last 20 points of a reaction are
  much harder to buy than the first 20.
- Decays 3% naturally between every beat.
- Comebacks from deep momentum deficits earn a larger pop (`earnedBonus` scales with deficit).

**Repetition and fatigue.** Each beat type retains less of its value every time it is
repeated in a match (heat segments 68%, rest holds 60%, near-falls 85%, third-party
run-ins 55%). Technical score decays more slowly than crowd reaction — repeated limb work
is a story, a third identical brawl is not. Past the fifth beat, contributions taper
according to both wrestlers' conditioning. Together these mean **more beats is not a
strategy**: a repeated-beat plan peaks and then declines.

**Crowd disposition** is not simply alignment. It is calculated from popularity and per-fan-group `AppealRatings` on the wrestler's gimmick. A heel with massive nostalgia appeal (e.g. Hulk Hogan at WrestleMania X8) reads as high-disposition and gets massive reactions — exactly as the Toronto crowd gave him.

---

## Wrestlers

Thirty wrestlers ship in `Wrestlers.json` — 15 in the women's division, 15 in the men's,
each division spanning all five card positions.

The spread is deliberate. An earlier roster clustered everyone between popularity 70–95
and skill 2.83–3.82, which used a quarter of each stat's range and made every match rate
roughly the same regardless of who was in it. The shipped roster now spans popularity
23–96, charisma 1.6–4.9 and psychology 44–96, and `RosterAttributes_UseMostOfTheirLegalRange`
fails the build if that compresses again.

It is built around archetypes the engine can tell apart:

| Wrestler | The idea | Reads as |
|---|---|---|
| Becky Lynch | Elite at everything | Top of the card on every axis |
| Shayna Baszler | Best pure worker, cannot connect | High Technical score, flat crowd |
| Chad Gable | Elite technician nobody pushes | 48% better ring work than LA Knight, far smaller reaction |
| LA Knight | Pure charisma, limited in-ring | Big crowd, thin Technical score |
| Randy Orton | Veteran: elite psychology, spent body | Excellent short, fades in a marathon |
| Bron Breakker | Freak athlete, green psychology | The inverse of Orton |
| Ricochet | Elite aerial athlete, weak promo | Carries a Spotfest, not a Storytelling match |
| Otis | Comedy act | Appeal well above his workrate |
| Von Wagner | Bottom of the card on every axis | The roster's floor |

Each wrestler has:

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
- Popularity (0–100), Charisma (0–5), Wrestling Style, Division, Moveset, Signatures

`CardPosition` (Main event → Enhancement) is derived from popularity rather than stored,
so it can never drift out of sync. The booking screens group by it.

---

## Playing on a phone

The web build is designed for mobile, not merely shrunk to fit.

- **Navigation moves to the thumb.** Below 820px the top nav is replaced by a fixed
  bottom tab bar. The top of a phone screen is the hardest place to reach.
- **The wizard footer sticks.** Back/Next sit above the tab bar in the match and segment
  builders, so you never scroll past a nine-beat sheet to advance. Browse screens do not
  get the sticky treatment — a lone "Main menu" button is not worth 60px of screen.
- **Touch targets are 44px minimum.** Buttons, chips, picks and the beat editor's controls.
- **Form controls are 16px+.** Anything smaller makes iOS Safari zoom the viewport on
  focus and never zoom back out.
- **Hover styles are gated behind `(hover: hover)`.** On touch, `:hover` latches after a
  tap — an ungated rule leaves the button you just pressed stuck in its hover state, and
  made an unselected card look selected.
- **Wide tables become cards.** The roster's two 10-column tables are unusable in a 360px
  scroll jail, so below 640px they are replaced by a grouped card list with expandable
  skill bars.
- **Thirty names need filtering.** Every roster picker has a search box and division
  chips, and groups results by card position.
- **Safe-area insets** are honoured top and bottom for notched devices.

Verified at 390×844 and 320×568 with a scripted audit for horizontal overflow,
sub-44px touch targets and iOS zoom triggers.

---

## Booking a Segment

Segments are how feuds get built. The flow mirrors match booking: pick an archetype,
cast it, then edit the action list.

### Segment templates

| Template | Cast | Stamps |
|---|---|---|
| Ring Promo | 1 | — |
| Backstage Interview | 2 | PersonalInsult |
| Face-to-Face Confrontation | 2 | PersonalInsult |
| Contract Signing | 2 | PersonalInsult, ChampionshipRivalry |
| Championship Celebration | 2 | ChampionshipRivalry, TitleStolen |
| Surprise Return | 2 | — |
| Betrayal | 2 | Betrayal |
| Post-Match Beatdown | 2+ | — |
| Faction Dominance | 2+ | FactionConflict |
| Crowd Brawl | 2 | — |
| Authority Announcement | 2+ | ManagerConflict |

Or build from scratch and assemble actions yourself from the **action library**:
Cut a Promo, Trash Talk, Issue a Challenge, Interrupt, Stand Tall, Blindside Attack,
Weapon Shot, Run-In, Turn on a Partner.

### Segment mechanics

- **Charisma** drives talking segments; it does nothing for a run-in.
- **Location** is a real modifier — Crowd ×1.10, Ring ×1.00, ParkingLot ×0.90,
  Backstage ×0.85, GMOffice ×0.80.
- **Scripted vs unscripted** is a trade-off: unscripted gets a ×1.15 rawness bonus
  but risks a botch, resisted by Psychology. A botch costs impact, heat, *and* popularity.
- **Injuries** come from physical actions and are resisted by the target's Toughness.
  An injury stamps `InjuryAngle` on the feud.
- **Heat** = action heat + charisma-scaled verbal heat + half the crowd reaction.
  A turn generates the most of anything in the game.

---

## Booking a Show

**Book a Show** assembles a card of matches and segments in any order, on a shared
runtime budget, and runs the whole thing through the real engines.

```
    #  ITEM                                      KIND      MIN
    ──────────────────────────────────────────────────────────
    1  Rhea Ripley vs Charlotte Flair            Match      22
    2  Rhea Ripley Betrays Charlotte Flair       Segment     3
    3  Becky Lynch vs Asuka                      Match      18
    ──────────────────────────────────────────────────────────

  Runtime  █████░░░░░░░░░░░░░░░  43 / 180 min   137 left

  [M]atch   [S]egment   [U]p   [D]own   [R]emove   [G]o   e[X]it
```

- **Position matters** — slot #1 is the opener (×1.2), the last slot is the main event (×1.5).
- **Fatigue** — two items of the same kind back to back take ×0.85. Flagged amber on the sheet.
- **Crowd mood** carries between items: a hot crowd lifts what follows, a flat one drags it down.
- **Runtime** — going over `TotalDurationMinutes` costs up to 35% of the overall score.
- Everything on the card deposits into the feud book, so a beatdown in segment 2
  is worth more heat by the time you book the rematch.

---

## Project Structure

```
WrestlingSim.Core/                  — the engine. No UI, no I/O assumptions.
├── Engine/
│   ├── MatchEngine.cs          — Executes a MatchPlan, returns MatchEngineResult
│   ├── MatchEngineState.cs     — Mutable crowd energy + momentum state during execution
│   ├── BeatLibrary.cs          — Catalogue of all named beat templates
│   ├── MatchStructureLibrary.cs — Seven preset match structures
│   ├── SegmentSimulator.cs     — Executes a Segment, returns SegmentResult
│   ├── SegmentActionLibrary.cs — Catalogue of named segment actions
│   ├── SegmentTemplateLibrary.cs — Eleven preset segment archetypes
│   ├── FeudBook.cs             — Session feud store; the segment↔match connector
│   ├── ShowSimulator.cs        — Full card execution on ICardItem
│   └── MatchSimulator.cs       — Legacy match simulator (no longer on any path)
├── Models/
│   ├── Wrestler.cs             — Core wrestler model
│   ├── Gimmick.cs              — Character, alignment, fan appeal
│   ├── RingSkills.cs           — Six-skill matrix + scoring
│   ├── ICardItem.cs            — Uniform view of anything on a show card
│   ├── BookedMatch.cs          — A MatchPlan sitting on a card
│   ├── Show.cs / ShowResult.cs — Card, runtime budget, per-item results
│   ├── Segment/
│   │   ├── Segment.cs          — Cast, actions, location, history tags
│   │   ├── SegmentAction.cs    — A single beat of a segment
│   │   ├── SegmentActionTemplate.cs — Named, reusable action archetype
│   │   ├── SegmentTemplate.cs  — Named, reusable segment archetype
│   │   └── SegmentResult.cs    — Engine output for one segment
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
└── JSON/                           — Roster, moves, signatures (also embedded)

WrestlingSim/                       — terminal front end
├── UI/
│   ├── MainMenu.cs             — Main menu rendering
│   ├── ConsoleUi.cs            — Shared console rendering + input helpers
│   ├── MatchBookingFlow.cs     — Guided match-booking UI + beat editor
│   ├── SegmentBookingFlow.cs   — Guided segment-booking UI + action editor
│   └── ShowBookingFlow.cs      — Card assembly, reordering, runtime budget
└── Program.cs

WrestlingSim.Web/                   — browser front end (Blazor WebAssembly)
├── Screens/
│   ├── MainMenuScreen.razor    — Landing screen + live feud summary
│   ├── RosterScreen.razor      — Roster and ring-skill tables
│   ├── FeudsScreen.razor       — Feud book with unlocked-beat hints
│   ├── MatchScreen.razor       — Match wizard + scorecard + play-by-play
│   ├── SegmentScreen.razor     — Segment wizard + outcome
│   └── ShowScreen.razor        — Card sheet, runtime budget, show result
├── Shared/
│   ├── Shell.razor             — Top bar and screen dispatch
│   ├── MatchBuilder.razor      — Reusable 5-step match builder + beat editor
│   ├── SegmentBuilder.razor    — Reusable 3-step segment builder + action editor
│   ├── PlayByPlay.razor        — Beat-by-beat commentary
│   ├── FeudUpdates.razor       — What a booking did to each feud
│   ├── Stars.razor / Meter.razor — Rating and bar primitives
│   └── ConsoleUi-equivalent styling in wwwroot/css/app.css
├── Services/GameState.cs       — Roster + the shared FeudBook for the session
└── wwwroot/                    — index.html, app.css, .nojekyll

WrestlingSim.Tests/
├── MatchEngineTests.cs         — Match engine tests incl. real-world match recreations
├── BeatLibraryTests.cs         — Beat library catalogue and round-trip tests
├── MatchStructureLibraryTests.cs — Structure preset tests
├── SegmentSimulatorTests.cs    — Botch, injury, overness, location, heat
├── SegmentLibraryTests.cs      — Every template builds, casts and simulates
├── FeudBookTests.cs            — Heat thresholds, tag stamping, pair keying
├── ShowSimulatorTests.cs       — Card rules + the end-to-end booking loop
└── TestRoster.cs               — Shared wrestler factory
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
