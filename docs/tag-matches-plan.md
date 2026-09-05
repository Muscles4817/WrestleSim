# Tag matches — implementation plan

*Written against the codebase at `53e0233`. Companion to
[31-sim-mapping.md](wrestling-reference/31-sim-mapping.md), which does not currently list
tag matches at all.*

---

## 0. The premise

The engine is 1v1 at every layer. `MatchPlan` carries `WrestlerA` and `WrestlerB` as required
init properties, `BeatControl` names those two people as its control axis, `MatchEngine.Ctx`
holds exactly two `PerformerProfile`s, `MatchEngineResult` reports one winner and one loser,
`HeatEconomy` moves status between two people, `FeudBook` keys on a pair of names, and
`TitleReign` has a single champion. None of that is wrong; it is just singles.

Two things have to be true for this to be worth building:

1. **A tag match must not be a singles match with four names on it.** If the only difference
   is the commentary strings, this is decoration. What makes the format distinct is a
   specific narrative machine — shine, cut-off, isolation, the near-tag denied, the hot tag —
   and the hot tag is the single loudest planned moment in professional wrestling. If the
   engine does not model *earning* it, there is no point.
2. **The refactor must not disturb singles.** There are 285 tests, several of which recreate
   real matches against a fixed seed and assert on threshold star ratings
   (`MatchEngineTests` — WM20, WM34). A change that shifts RNG draw order in the singles path
   will move those numbers. Phase 1 below is designed so that singles execution is
   byte-identical afterwards.

---

## 1. The core decision: sides, not a parallel plan type

Two options were considered.

**A parallel `TagMatchPlan` + `TagMatchEngine`.** Less churn on existing files, but it forks
the engine. Every future beat, every rating change, every balance pass would have to be made
twice, and the two copies would diverge within a month. Rejected.

**A sides abstraction.** `MatchPlan` holds `SideA` and `SideB`, each a `MatchSide` of one or
more members. A singles match is a 1v1 side match. This is more churn up front, and it is the
right primitive — it also opens trios, handicap, multi-man and battle royals later without a
third code path.

**Recommendation: sides, introduced behind compatibility shims** so the existing call sites
and all 285 tests keep compiling unchanged.

```csharp
public sealed class MatchSide
{
    public List<Wrestler> Members { get; init; } = new();
    public TagTeam? Team { get; set; }          // phase 4
    public int LegalIndex { get; set; }          // who is in the ring
    public Wrestler Legal => Members[LegalIndex];
    public bool IsTag => Members.Count > 1;
}

public class MatchPlan
{
    public MatchSide SideA { get; init; } = new();
    public MatchSide SideB { get; init; } = new();

    // Compatibility shims. Existing object initialisers — and there are many —
    // keep working verbatim: new MatchPlan { WrestlerA = a, WrestlerB = b }.
    public Wrestler WrestlerA { get => SideA.Members[0]; init => SideA.Members.Add(value); }
    public Wrestler WrestlerB { get => SideB.Members[0]; init => SideB.Members.Add(value); }
}
```

`Validate()` gains: both sides non-empty, sides the same size (until handicap is a deliberate
feature), and **no wrestler on both sides** — the last one matters because `Ctx.For(w)`
currently resolves a profile by reference equality and would silently return the wrong one.

### What does *not* change

`MatchEngineState.Advantage` stays a single −100..+100 scalar. This is the pleasant surprise
of the refactor: in-match advantage in a tag match is a *side-level* property — the heels are
on top, the faces are in trouble — which is exactly what the existing scalar already
expresses. Comebacks, the earned-finish rule, and `RawAdvantage` all carry over untouched.

`BeatControl.WrestlerA`/`WrestlerB` keep their names in phase 1 and are simply reinterpreted
as "side A" / "side B". Renaming them to `SideA`/`SideB` would churn 38 call sites in
`MatchStructureLibrary` alone and — because `BeatDto` persists this enum through
`JsonStringEnumConverter` — would change what gets written into saves. Deferred to a
cosmetic cleanup after the feature lands.

---

## 2. The narrative machine

New `BeatType` members, and the rule each one encodes:

| Beat | What it does | Reference |
|---|---|---|
| `Shine` | The face team's opening control stretch. Establishes them before the heat. | [18](wrestling-reference/18-match-craft.md) §3 |
| `Cutoff` | The heels take over — usually off a distraction. Ends the shine, starts the heat. | |
| `Isolation` | The face-in-peril segment. A heat segment scoped to a side, keeping one man cut off from his corner. | |
| `NearTag` | The tag reached for and denied. **Negative** crowd energy in the moment, and it charges the hot tag. | [16](wrestling-reference/16-crowd-psychology.md) §2 |
| `HotTag` | The payoff. The largest single crowd delta the engine can produce. | [30](wrestling-reference/30-glossary.md) |
| `BlindTag` | A tag the opponents did not see. Heel cheating, or a face surprise. | |
| `DoubleTeam` | Team offence. Scores off **team chemistry**, not individual workrate. | |
| `Miscommunication` | Partners collide. Storytelling now, a team split later. | |
| `SaveBreakup` | The partner breaks up the pin. Extends a near-fall; decays hard on repeat. | |
| `AllFourBrawl` | Everyone in, referee loses control. Resets the room before the finish. | |

### The hot tag rule

This is the heart of it, and it should be built as a direct sibling of the rule the engine
already has for finishes — *the payoff must be earned*:

```
hotTagPayoff = base
             × isolationCharge   // beats spent isolated since the last tag, saturating ~3
             × nearTagCharge     // near-tags denied since the last tag, saturating ~2
             × connection(incoming partner)
             × selling(the isolated man)
```

A `HotTag` with no preceding `Isolation` takes the same shape of penalty `ApplyFinish` takes
for an unearned finish (currently `×0.55`). Booking the hot tag cold should read as flat,
because it is.

`NearTag` is deliberately a *negative* crowd delta that increases the charge. This is the one
mechanic in the plan that is counter-intuitive as a number and completely obvious as
wrestling: the denied tag makes the room quieter and angrier, and that is stored energy.

### Legal-man tracking

`MatchEngineState` gains `LegalA` / `LegalB` indices. `HotTag`, `BlindTag` and a plain `Tag`
beat swap the legal man on their side. `MatchBeat` gains optional `SideAPerformer` /
`SideBPerformer` overrides (null = whoever is currently legal) so a booker who wants a
specific man in peril can say so, without forcing that choice on every beat.

`Ctx` changes from two profiles to a `Dictionary<Wrestler, PerformerProfile>` plus
`Side(BeatControl)` and `Legal(BeatControl)` accessors. `Ctx.Pair(f)` — used by beats nobody
controls — becomes an average over the two *legal* performers, which keeps singles identical.

### New structures

`MatchStructure` gains `SideSize` so the builder only offers valid presets. Three to add:

- **Southern Tag** — shine, cut-off, isolation ×2, near-tag, isolation, near-tag, hot tag,
  all-four brawl, save-breakup near-fall, finish. The canonical thirteen-minute tag match.
- **Tag Sprint** — six beats, no isolation, double-teams and a flash finish. The TV opener.
- **Formula Tag** — the television version of Southern Tag, one isolation shorter.

---

## 3. Consequences

### Heat: the man who ate the pin

`HeatEconomy.ForMatch` becomes `ForSides`, returning a `StatusChange` per participant:

- The **pinned** man takes the full loser's hit; his partner takes roughly **35%** of it.
- The **pinner** takes the full winner's gain; his partner roughly **50%**.
- The transfer pool is computed from each side's **average** overness, so beating a team of
  two mid-carders is not the same statement as beating one main-eventer.

That asymmetry is the whole point. It makes "have the other guy take the fall" a real,
costed booking lever — the most common way a tag match is used to protect someone — and it
falls straight out of the rules already written in
[17](wrestling-reference/17-heat-and-getting-over.md) §6.

`MatchEngineResult` gains `WinningSide` / `LosingSide` / `Pinner` / `Pinned`, with `Winner`
and `Loser` kept as shims onto `Pinner` / `Pinned`. That keeps `ShowSimulator`'s non-title-loss
rule (`_titles.HeldBy(engineResult.Loser)`) working *and* correct: only the man who actually
ate the pin devalues the belt he is carrying.

### Feuds

`FeudBook.Key` extends from two sorted `RealName`s to sorted side keys, so `A+B vs C+D` is its
own feud with its own familiarity curve — which is right, because the crowd's appetite for a
team pairing is genuinely separate from its appetite for the singles matches inside it. A tag
match records full heat against the team feud and a fraction against each cross-pair, so a
tag programme can build toward a singles blow-off.

`ShowSimulator.RunMatch` currently reads `match.Plan.WrestlerA/B` to fetch familiarity and
record the feud; both move to side keys.

### Tag titles

`TitleReign.Champion` → `Champions` (a list), with `Champion` as a shim onto `Champions[0]`.
`Title` gains a `SideSize` (1 for singles belts, 2 for tag). `TitleEconomy.ResolveTitleMatch`
takes sides. Everything else in the title economy — standing, dilution, the fixed attention
pool — is unchanged, and a tag belt correctly starts competing for the same finite attention
as every other belt, which is exactly the behaviour doc 21 §2.1 describes.

---

## 4. Persistence and UI

**Save format v2 → v3.** `CardItemDto` gains `SideA` / `SideB` as `List<string>`, keeping
`WrestlerA` / `WrestlerB` for reading v2 saves (`SideA ??= [WrestlerA]` on load).
`TitleReignDto.Champion` → `Champions`, same treatment. `SaveGame` gains `Teams`.

**Web builder** (`MatchBuilder.razor`). Step 0 becomes "pick the sides" — a side-size toggle,
then 1..n slots per side. The beat editor's control dropdown becomes a side selector plus an
optional performer selector for the tag beats. `BookedMatch.Name` becomes
`"A & B vs C & D"`, or the team names once teams exist.

**Console flow** (`MatchBookingFlow.cs`) mirrors it.

---

## 5. Phasing

Each phase ends green and, from phase 3, playable.

| Phase | Work | Ends with |
|---|---|---|
| **1. Sides** | `MatchSide`, shims on `MatchPlan`, `Ctx` by dictionary, validation. No new beats, no UI, no persistence change. | All 285 existing tests pass **unmodified**; new tests build a 2v2 plan in code and run it. It plays like a singles match with four names — that is the correct outcome for this phase. |
| **2. The machine** | Tag beat types, legal-man tracking, hot-tag charge rule, three structures. Engine-only, test-driven. | A Southern Tag structure out-rates a beat-for-beat identical plan with the hot tag booked cold. |
| **3. Playable** | Save v3 + migration, web builder, console flow. | You can book, run, save and reload a tag match end to end. |
| **4. Teams** | `TagTeam` (name, members, chemistry, tenure), chemistry feeding `DoubleTeam` and suppressing `Miscommunication`, `Career.Teams`. | A veteran team out-performs two strangers of identical individual skill. |
| **5. Consequences** | Heat split by who was pinned, team feud keying, tag titles. | Booking your star's partner to take the fall measurably protects the star. |

Rough shape: phase 1 is the largest single diff and the lowest risk; phase 2 is where the
design judgement lives; phases 3–5 are each smaller than phase 1.

---

## 6. Risks

1. **Seeded test drift.** `MatchEngineTests` recreates real matches against a fixed seed with
   threshold assertions, some tight (`<= 1.75`, `>= 2.25`). Any change to RNG *draw order* in
   the singles path moves them. Phase 1 must add no `Rng` call on that path; if a threshold
   moves anyway, that is a signal the refactor changed behaviour, not a test to relax.
2. **`Ctx.For` reference equality.** Currently `w == Plan.WrestlerA ? A : B` — it silently
   returns B for any unrecognised wrestler. Under sides this becomes a dictionary lookup and
   should throw, with the same-person-on-both-sides case caught in `Validate()`.
3. **Enum serialisation.** Leave `BeatControl`'s member names alone until the feature has
   landed; renaming them changes what is written into saves.
4. **Scope.** Phases 4 and 5 are each independently valuable and independently shippable. If
   this runs long, phases 1–3 alone give a playable tag division; stopping there is a
   legitimate outcome, not a half-finished one.

## 7. What this is not

This does not address the roadmap items 31-sim-mapping.md ranks above it by
value-per-unit-of-work — A3 (feud heat decay and a terminal blow-off), A5 (the reaction
vector), A6 (persistent limb damage), A7/A8 (finisher and interference credibility). Tag
matches are a Tier-B-sized job that the gap analysis does not currently list at all. They are
worth building because the format is a third of a real card and because the hot tag is the
best-understood crowd mechanic in wrestling — but they are an addition to the roadmap, not a
step along it.
