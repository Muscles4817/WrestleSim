# Exhibition UX — feature tickets

Extracted from a UX review of the exhibition booking flow (screenshots of the five-step
builder and the result screen). Nothing here is built yet; this is a backlog for review.

The review's summary of the problem, which is worth keeping at the top because most of the
tickets are downstream of it:

> The game knows significantly more than the interface tells the player. The player makes
> selections without enough context to decide, and afterwards cannot trace the rating back to
> those decisions.

That framing survives contact with the code. Two findings from reading the engine make it
concrete, and they change what some of these tickets cost:

- **The result screen already has the causal data and throws it away.** `MatchEngine`
  returns a `ScoreBreakdown` (`MatchEngine.cs:2646-2656`) carrying the weighted technical,
  storytelling and crowd components, the investment factor, and the finish, variety and
  type-coherence nudges. `MatchScreen.razor` renders none of it. The diagnosis work is
  mostly presentation, not new simulation.
- **The scorecard's denominators are not the engine's.** `MatchScreen.razor:34-35` renders
  `Max="60"` and `Max="80"` while the engine grades those axes through
  `Saturate(raw, 48)` and `Saturate(raw, 62)` (`MatchEngine.cs:30-31, 2565-2566`). The
  `MaxTechnical = 60` / `MaxStorytelling = 80` constants at `MatchEngine.cs:244-245` are
  referenced nowhere else in the solution. So `40/60` is not "40 of 60 available points" —
  it is a display constant with no relationship to how the number was judged.

Priorities follow the reviewer's own ordering. Sizes are rough: S ≈ an afternoon, M ≈ a day
or two, L ≈ needs its own design pass first.

| ID | Title | Priority | Size |
| --- | --- | --- | --- |
| EXH-01 | Make the winner an explicit choice | P1 | M |
| EXH-02 | Persistent booking-summary rail across the wizard | P1 | M |
| EXH-03 | Turn the result screen into a causal diagnosis | P1 | L |
| EXH-04 | Normalise the scorecard and explain the star maths | P1 | M |
| EXH-05 | Make the beat sheet a real editor with labelled fields | P1 | M |
| EXH-06 | Dramatic-curve preview on the beats page | P1 | M |
| EXH-07 | Pre-flight "Booking read" — strengths and risks before the bell | P2 | L |
| EXH-08 | Give the picker booking context in exhibition mode | P2 | M |
| EXH-09 | Picker filters, column meaning and the unexplained dash | P2 | S |
| EXH-10 | Reframe "Match type" as a promise, not a weight table | P2 | M |
| EXH-11 | Structure cards: metadata, suitability, readable disabled state | P2 | M |
| EXH-12 | Assign and check face/heel roles against the structure | P2 | M |
| EXH-13 | Differentiate the repeated heat segments | P2 | S |
| EXH-14 | Play-by-play: prose first, telemetry behind a toggle | P3 | M |
| EXH-15 | Sticky navigation on desktop and revisitable steps | P3 | S |
| EXH-16 | Readability, selection state, and gold overload | P3 | M |
| EXH-17 | Say where the extra two minutes come from | P3 | S |

---

## EXH-01 — Make the winner an explicit choice
**Priority P1 · Size M · Feedback §2**

**Problem.** The most consequential decision in the workflow is helper text. Step 1 says
"Side A wins by default in every preset" in `small muted` in the panel header
(`MatchBuilder.razor:82`).

**Current behaviour.** The winner is derived, not chosen: `MatchPlan.BookedWinningSide`
reads the `Control` of the last finish beat (`MatchPlan.cs:153-161`), and every library
structure is written with `WrestlerA` as the intended winner
(`MatchStructureLibrary.cs:9-10`). The player *can* change it — the finish beat's "Who is on
top" chips in the beat sheet — but only by opening a beat editor four steps later and
knowing that control on the finish means victory.

**Proposed change.**
- A winner control on the lineup step, once the sides are complete: one option per side,
  named after the wrestlers ("Charlotte Flair wins"), not the corners.
- Selecting it rewrites the finish beat's `Control` when the structure is applied, and
  mirrors the finish beat if the player changes it later — the two must never disagree.
- Surface the choice in the booking summary rail (EXH-02) so it stays visible.
- Consider draw/no-contest only if the engine supports it; `MatchPlan.Validate` currently
  requires a booked winning side, so treat that as out of scope unless we want it.

**Acceptance criteria.**
- [ ] Winner is chosen by name on step 1 and cannot be left ambiguous.
- [ ] Changing the winner after picking a structure re-aims the finish beat, and changing the
      finish beat's control updates the displayed winner.
- [ ] "Side A wins by default" helper text is gone.
- [ ] Corner labels read "Charlotte Flair" rather than "Corner A" wherever a name is known
      (`MatchBuilder.razor:613-635`, the versus block).

**Notes.** Sides still exist internally — `MatchSide`, `BeatControl.WrestlerA/B/SideC/SideD`
are load-bearing throughout the engine. This is a labelling and binding change, not a model
change.

---

## EXH-02 — Persistent booking-summary rail across the wizard
**Priority P1 · Size M · Feedback §1**

**Problem.** Context evaporates after the step that produced it. By the beats step the
player can see the two names and the feud (`MatchBuilder.razor:612-660`) but not the
declared type, the title at stake, the structure, or the winner.

**Proposed change.** One summary component, rendered on every step: right-hand rail on
desktop, collapsible strip on mobile. Contents, each linking back to the step that set it:

- Match (names and shape), winner, alignments
- Declared type, title at stake, feud state and heat
- Structure, beat count, expected length
- Key strengths and main risks once EXH-07 exists (until then, the warnings that already
  exist: familiarity, blow-off unearned, hot-tag advice)

**Acceptance criteria.**
- [ ] Every step shows the same summary, populated with whatever has been decided so far.
- [ ] Fields not yet chosen read as "not chosen", not as blanks.
- [ ] Clicking a field navigates to its step (subject to EXH-15's revisit warning).
- [ ] On ≤820px it collapses to a one-line strip that expands on tap.

**Notes.** The review also observes there is a lot of empty horizontal space around a narrow
central column on desktop; the rail is the argument for spending it. Depends on EXH-01 for
the winner field.

---

## EXH-03 — Turn the result screen into a causal diagnosis
**Priority P1 · Size L · Feedback §8**

**Problem.** The scorecard says what happened, never why. `MatchScreen.razor:31-40` is five
meters and two conditional warnings. The player cannot tell what earned the storytelling
score, what cost it, or which booking decision cost them the fourth star.

**Current behaviour.** The data largely exists and is discarded:
- `MatchEngineResult.Breakdown` — weighted components, `InvestmentFactor`, `FinishNudge`,
  `VarietyNudge`, `CoherenceNudge` (`MatchEngine.cs:2646-2656`).
- `MatchEngineResult.MatchTypeCoherence` — whether the plan delivered the declared type.
- Per-beat `TechnicalContribution` / `StorytellingContribution` / crowd deltas, already
  rendered as raw numbers in `PlayByPlay.razor:12-18`.

**Proposed change.**
- Each scorecard row expands into what raised it and what held it back, written as booking
  causes rather than arithmetic: the beats that contributed most, the missing beat types the
  declared type wanted, the coherence and variety nudges, the familiarity discount.
- Two or three actionable lessons under the card ("replace the second heat segment with a
  hope spot or false finish"), derived from the same signals, not hand-written per match.
- Attribute contributions to the wrestlers where the engine knows — the finish's technical
  contribution is already scaled by the controlling wrestler's style proficiency, workrate
  and the opponent's selling (`MatchEngine.cs:2536-2540`).

**Acceptance criteria.**
- [ ] Every score row expands to positive and negative causes; no row expands to nothing.
- [ ] Lessons are generated from the result, and a plan that fixes them measurably scores
      better on a re-run.
- [ ] Nothing in the default view is a raw engine number without a stated meaning.

**Notes.** This is the largest ticket and the one the reviewer rates most important. It may
need a small `MatchDiagnosis` service in Core so the same explanation logic can be tested and
reused by the show report, rather than living in Razor.

---

## EXH-04 — Normalise the scorecard and explain the star maths
**Priority P1 · Size M · Feedback §9**

**Problem.** `40/60` and `39/80` imply the categories have those maxima. They do not. The
engine saturates the raw sums with scales of 48 and 62 and then multiplies by the match
type's weights (`MatchEngine.cs:30-31, 2565-2566`); the 60 and 80 in the UI
(`MatchScreen.razor:34-35`) come from constants nothing else reads
(`MatchEngine.cs:244-245`). A player optimising against those denominators is optimising
against a number the engine never uses.

**Proposed change.**
- Show performance normalised to 100 (`Saturate(raw, scale) * 100`) alongside the
  contribution in points that actually reached the final score — the `Breakdown` values.
- A short "how this became 3.83 stars" panel: components, the three nudges, the investment
  factor, `finalScore / 20`.
- Delete `MaxTechnical` / `MaxStorytelling`, or make them the real scales so the UI cannot
  drift from the engine again.

**Acceptance criteria.**
- [ ] No denominator on screen that the engine does not use.
- [ ] The star rating can be reconstructed by the player from what the page shows.
- [ ] A test asserts the displayed components sum (with nudges) to `FinalScore`.

**Notes.** Small and high-value; EXH-03 depends on getting this right first.

---

## EXH-05 — Make the beat sheet a real editor with labelled fields
**Priority P1 · Size M · Feedback §6**

**Problem.** The row reads `Rhea Ripley · High · Medium · 4 min`
(`MatchBuilder.razor:694-699`) with no field labels — the player has to remember the order of
control, intensity, duration. The row does not look clickable, and reordering is two icon
buttons (`MatchBuilder.razor:701-707`).

**Proposed change.**
- Label the fields on the row, or restructure it so each value is unambiguous.
- Give the row a visible affordance (chevron, hover/focus state) — it already opens the beat
  sheet on click, nothing advertises it.
- Add duplicate and delete to the row; keep remove in the sheet.
- Replace or supplement the ↑/↓ buttons with a drag handle, keeping keyboard reordering.
- Show the dramatic function in the sheet: what the beat is for, what it hands to the next
  one. `BeatLibrary` already carries `BookerTip`, currently only shown in the sheet header
  area (`MatchBuilder.razor:231-235`) and the library list.

**Acceptance criteria.**
- [ ] No unlabelled value on a beat row.
- [ ] Row is obviously interactive at rest, not only on hover.
- [ ] Duplicate, delete, reorder and edit are all reachable from the row, by mouse and keyboard.

---

## EXH-06 — Dramatic-curve preview on the beats page
**Priority P1 · Size M · Feedback §6**

**Problem.** The beat sheet is a list; the thing it describes is a curve. Nothing on the page
shows the shape of the match the player has booked.

**Proposed change.** A compact chart above the beat sheet plotting, against booked minutes:
projected crowd trajectory, advantage/control, intensity, and where the finish lands. It does
not need to be the engine's real simulation — a cheap projection from beat intensity,
duration and control is enough to make the structure legible, but it must not contradict the
result (see risk).

**Acceptance criteria.**
- [ ] Reordering or editing a beat visibly changes the curve.
- [ ] The finish's position and the peak are readable at a glance.
- [ ] The preview is labelled as a projection, not a prediction of the score.

**Risk.** If the projection and the actual result disagree noticeably, this makes the
legibility problem worse rather than better. Either derive it from the same contribution
tables the engine uses, or keep it deliberately qualitative (shape, not numbers).

---

## EXH-07 — Pre-flight "Booking read": strengths and risks before the bell
**Priority P2 · Size L · Feedback §7**

**Problem.** The builder warns about specific mistakes (validation errors, hot-tag advice,
unearned blow-off, pairing fatigue) but never states what the plan as a whole is trying to do
or where it is weak.

**Proposed change.** A panel on the final step, before "Run the match":
- **What this plan is trying to do** — read from the structure and the beat sequence.
- **Strengths** — e.g. both wrestlers credible at the booked intensity, finish aligned with
  the built advantage.
- **Risks** — e.g. declared Storytelling with no callback or near-fall in the sheet; two
  consecutive heat beats with the same function; feud too cold for the emotional escalation
  the structure assumes; opening too short to establish the contrast.

Forecasts, not scores: no star estimate.

**Acceptance criteria.**
- [ ] Every risk names the beat or decision it comes from.
- [ ] Declared-type mismatch is flagged before the match runs, not only afterwards.
- [ ] The panel never shows a predicted rating.

**Notes.** `TypeCoherence` (`MatchEngine.cs:2721`) is `private static` and already computes
the declared-type fit from the plan alone. This ticket wants it exposed as a public,
UI-callable plan analysis — probably the same Core service EXH-03 needs.

---

## EXH-08 — Give the picker booking context in exhibition mode
**Priority P2 · Size M · Feedback §3**

**Problem.** The reviewer saw name, style, alignment, one number and a dash. That is exactly
what the code produces *in exhibition*, and it is not what the picker is capable of.

**Current behaviour.** `BookingSuggestions.Suggest` bands rows as "There is a story here",
"The crowd has seen this", "Suggested", "Recently booked" with a reason string
(`BookingSuggestions.cs:105-184`). In exhibition `GameState.EnterExhibition` sets
`Career = null` (`GameState.cs:380-386`), so `StandingPartnerOf` returns null and
`RecentlyBooked` returns empty (`RosterPicker.razor:296-318`), and the feud book starts
empty. Every row therefore falls through to `Plain(...)` — "Style · alignment" — which is the
screenshot.

**Proposed change.** Decide what exhibition means, then make the picker honest about it:
- If exhibition is meant to be contextless, say so once in the sheet rather than showing
  bands that can never populate.
- Better: seed exhibition from the shipped roster's own relationships — rivalry and
  chemistry data that exists in `Wrestlers.json` and the team model — so "Rhea Ripley —
  active rivalry, strong chemistry" is available without a career save.
- Once a second wrestler is chosen, retitle the sheet to "Opponents for Charlotte Flair" and
  sort by matchup relevance (`Against` already drives the ranking).

**Acceptance criteria.**
- [ ] A first-time exhibition picker shows at least one piece of situational information per
      row beyond style and alignment.
- [ ] The second slot's sheet is explicitly framed against the first pick.
- [ ] No band heading renders that cannot contain rows in this mode.

**Open question.** How much of the career layer should exhibition inherit? This is a design
call, not an implementation detail — flagged for you.

---

## EXH-09 — Picker filters, column meaning and the unexplained dash
**Priority P2 · Size S · Feedback §3, §12**

**Problem.** The dash the reviewer could not explain is the momentum arrow: `Trend` renders
`—` for anything between −6 and +6, with `ShowLabel="false"` in the picker
(`RosterPicker.razor:113`, `Trend.razor:31-33`), so its only explanation is a `title`
tooltip. The overness number has no header either.

**Proposed change.**
- Give the two value columns headings or inline labels ("Overness", "Momentum"), or drop the
  steady dash entirely — an absent arrow says "steady" as well as a dash does and reads as
  data rather than as missing data.
- Filters beyond division: brand, alignment, availability, feud, team, card position. The
  filter component (`CastFilter`) already carries query, division and crossover.
- Tooltips on every icon and number, not only the trend.

**Acceptance criteria.**
- [ ] Nothing in a picker row is unlabelled.
- [ ] `—` never appears where a value is genuinely known.
- [ ] Added filters combine with search and persist while the sheet is open.

---

## EXH-10 — Reframe "Match type" as a promise, not a weight table
**Priority P2 · Size M · Feedback §4**

**Problem.** The type step leads with percentages (`MatchBuilder.razor:357`,
`TypeWeights` at `1069-1079`), which invites optimisation against the scoring function rather
than an expressive booking choice. "Type" also collides with the shape choice on step 1
(singles / tag / triple threat).

**Proposed change.**
- Lead with what the audience has been promised; move the weights behind a "How this is
  judged" disclosure.
- Make the option respond to the booked wrestlers: fit and risk lines ("both have excellent
  psychology"; "the feud is cold, limiting the payoff").
- Rename the step. Candidates: **Match promise**, **Declared style**, **Audience
  expectation**. The reviewer prefers something that cannot be confused with match format.
- `TypeWeights` in the Razor duplicates `WeightsFor` in the engine
  (`MatchEngine.cs:2671-2680`). Whatever survives should read one source.

**Acceptance criteria.**
- [ ] Percentages are available but not the headline.
- [ ] Each option shows at least one fit or risk line derived from the booked wrestlers.
- [ ] Step label no longer reads "Type"; nothing in the UI hardcodes the weights twice.

---

## EXH-11 — Structure cards: metadata, suitability, readable disabled state
**Priority P2 · Size M · Feedback §5**

**Problem.** Structures are a long list of dense sentences with a beat count
(`MatchBuilder.razor:583-597`). The disabled feud-gated option's explanation is very low
contrast — the text that tells the player how to unlock it is the hardest to read.

**Proposed change.**
- Compact metadata per card: beat count, recommended length, intended protagonist, ideal
  feud intensity, required skills, likely crowd shape, fit for the booked pair.
  `MatchStructure` currently carries only `Name`, `Description`, `SideSize`, `SideCount`,
  `Tags`, `RequiresFeud`, `Beats` (`MatchStructure.cs`) — most of this is new model data.
- A one-line skeleton of the shape: "Early shine → isolation → comeback → decisive finish".
- Raise disabled contrast to at least AA; disabled must stay readable because it is
  instructional.
- A clearly visible selected state (see EXH-16).

**Acceptance criteria.**
- [ ] Every structure card shows length, beat count and a fit indication.
- [ ] Disabled cards meet contrast requirements and state their unlock condition legibly.
- [ ] Fit is computed from the booked wrestlers, not authored per structure.

---

## EXH-12 — Assign and check face/heel roles against the structure
**Priority P2 · Size M · Feedback §5**

**Problem.** Face-in-Peril casts side A as the sympathetic face and side B as the imposing
heel — the library says as much (`MatchStructureLibrary.cs:9-10`, "WrestlerA is the face /
intended winner in all defaults"). Nothing checks that against the wrestlers' stored
alignments (`Gimmick.NaturalAlignment`), and nothing tells the player which role each has been
cast in. A heel-vs-heel Face-in-Peril books silently.

**Proposed change.**
- State the assigned roles explicitly on the structure and beats steps ("Charlotte works
  face, Rhea works heel").
- Warn when the assignment contradicts stored alignment, without blocking it — booking
  against alignment is a legitimate call and sometimes the point.
- Decide whether the engine should care. If alignment already affects sympathy or crowd
  reaction, the warning should say what it costs; if it does not, the warning is presentational
  and should say that too.

**Acceptance criteria.**
- [ ] Structures that imply roles state them.
- [ ] A conflicting alignment produces a visible, non-blocking warning naming both wrestlers.

**Open question.** Whether alignment should feed the simulation is a design decision — flagged
rather than assumed.

---

## EXH-13 — Differentiate the repeated heat segments
**Priority P2 · Size S · Feedback §6**

**Problem.** Two Heat Segments in the same plan render identically, so the sheet reads as
repetition rather than escalation. The engine may well treat them differently; the sheet
cannot say so.

**Proposed change.** Distinguish heat beats by dramatic job — initial cutoff, sustained heat,
hope spot denied, desperation phase — either as separate library entries or as a role field on
the beat, and show that role on the row and in the play-by-play. Then EXH-07 can flag "two
beats with the same function" as a real risk.

**Acceptance criteria.**
- [ ] Two consecutive control beats never render with identical text.
- [ ] The distinction is visible on the row, the beat sheet and the play-by-play.

---

## EXH-14 — Play-by-play: prose first, telemetry behind a toggle
**Priority P3 · Size M · Feedback §10**

**Problem.** Every beat ends with `Crowd 72 → 74 · Advantage −53.6 · Tech +9.6 · Story +5.4`
(`PlayByPlay.razor:12-18`), which reads as a debug log. Commentary lines repeat across beats
of the same type. `+ PriorMatch` is an enum name leaking into the UI — exhibition records
`FeudHistoryTag.PriorMatch` on every match (`MatchScreen.razor:110`) and the feud panel
prints the tag directly.

**Proposed change.**
- Default view: what happened, how the crowd reacted, whether the beat's intended function
  landed, what it carried into the next beat. Numbers move into a "Simulation details"
  disclosure.
- Player-facing labels for `FeudHistoryTag` values ("Previous encounter"), used everywhere the
  tags are shown — the feud step lists them too (`MatchBuilder.razor:513`).
- Widen or vary commentary for repeated beat types so a two-heat-segment match does not print
  near-identical lines.
- Clarify "Run the same plan again": the engine has per-beat variance, so label it "Run again
  with new performance variance" and say what changes.

**Acceptance criteria.**
- [ ] No raw engine value in the default play-by-play view.
- [ ] No enum name reaches the screen.
- [ ] Re-running the same plan is labelled with what it varies.

---

## EXH-15 — Sticky navigation on desktop and revisitable steps
**Priority P3 · Size S · Feedback §11**

**Problem.** The wizard bar is sticky only below 820px — `app.css:224-248` scopes
`.wizard-bar--sticky` to `@media (max-width: 820px)`, deliberately, but the consequence on
desktop is that Next sits below a long structure list. The step indicator renders as `div`s
(`MatchBuilder.razor:7-22`), so the green ticks imply revisitable steps that cannot be
clicked.

**Proposed change.**
- Make the wizard bar sticky on desktop too: Back left, validation status centre, Next right.
  The status line already exists as `Blocker()` (`MatchBuilder.razor:1227`); the bar should
  also carry the positive state ("Face-in-Peril selected · 5 beats · ~18 min").
- Make completed steps clickable, with a warning when returning to a step whose change would
  regenerate the beat sheet — picking a different structure replaces `beats` wholesale
  (`PickStructure`), which silently discards the player's edits.

**Acceptance criteria.**
- [ ] Next is reachable without scrolling on every step at desktop widths.
- [ ] Completed steps are focusable, clickable controls.
- [ ] Any navigation that would discard beat edits asks first.

---

## EXH-16 — Readability, selection state, and gold overload
**Priority P3 · Size M · Feedback §12**

**Problem.** Important secondary information is muted blue-grey at small sizes: option
descriptions, disabled structure explanations, metric annotations, picker secondary detail,
the reorder controls. Gold simultaneously means selected, important, branded, warning, score
and positive result, so selection is carried almost entirely by colour.

**Proposed change.**
- A contrast pass on `.pick__desc`, `.small.muted`, disabled `.pick`, and `.beat-row__sub`;
  target AA for anything instructional.
- A stronger selected treatment that does not rely on hue alone — check or radio marker,
  filled edge, or an explicit "Selected" badge on `.pick.is-selected`.
- Audit gold usage and give at least warning and score their own treatments.

**Acceptance criteria.**
- [ ] All instructional text meets AA, disabled states included.
- [ ] Selection is identifiable in greyscale.
- [ ] A documented rule for what gold means.

---

## EXH-17 — Say where the extra two minutes come from
**Priority P3 · Size S · Feedback §6**

**Problem.** The beat sheet header reads `~18 min` while the listed beats sum to 16.
`RuntimeMinutes` is `2 + beats.Sum(x => x.DurationMinutes)` (`MatchBuilder.razor:1207`) — a
flat two minutes for entrances that nothing on screen explains.

**Proposed change.** Either label the addition ("16 min of beats · +2 entrances") or drop it
and let the number match the sheet.

**Acceptance criteria.**
- [ ] The header total is reconcilable with the rows without reading the source.

---

## Raised in the review, not ticketed

- **Sticky footer "missing" on mobile.** It exists (`app.css:224-248`); only desktop is
  affected. Folded into EXH-15.
- **"Add filters beyond gender."** The existing filter is division, which is not the same
  thing; the filter set is ticketed in EXH-09 regardless.
- **"The picker gives only a roster ranking."** True in exhibition, not in a career save —
  the cause is in EXH-08, which is why that ticket is about mode parity rather than about
  adding fields.
- **Draw / no contest.** Out of scope for EXH-01 unless we want it: `MatchPlan.Validate`
  requires a booked winning side, so it is an engine change, not a UI one.

## Open questions for you

1. **How much of the career layer should exhibition inherit** (EXH-08)? Rivalries and teams
   without a world clock, or an explicitly contextless sandbox?
2. **Should stored alignment affect the simulation** (EXH-12), or is the face/heel role
   purely a presentational cast list?
3. **Does the curve preview** (EXH-06) **need to be engine-accurate**, or is a qualitative
   shape acceptable? Accuracy is most of the cost.
4. **Are these being tracked here, or as GitHub issues?** The repo has no issues open; say
   the word and I will file each of these as one.
