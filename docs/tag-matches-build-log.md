# Tag matches — build log

This file was a single 2,800-line record of everything: what was built, and what independent
review made of it. It got too long to be readable as either, so it is now two files.

### [Design record →](tag-matches-design-record.md)

**What the engine does and why.** One section per piece of work — the problem, what was built,
and the measurements behind it. Read this to understand how tag matches, trios, feud decay, the
crowd reaction vector or the match builder work.

Corrections stay in it, inline: where a claim was withdrawn or a figure re-measured, it says so
next to the original.

### [Review record →](tag-matches-review-record.md)

**What review found, round by round.** Every blocker, every surviving mutation, and every claim
of mine that did not survive measurement.

Read in order it is repetitive, because the same two failures recur — a mechanism guarded at
its end points and free in between, and claims that outran their measurements. It also records
where the review loop itself went wrong: the stopping rule was "review until a round comes back
clean", which does not terminate when the instrument is mutation testing.

---

Both files came from this one; the split moved 2,760 lines and dropped none of them. Phases 1–5
keep their review rounds inline in the design record, because those phases were worked
build → review → fix → build and separating them leaves two halves that read as neither.

The plan this was built from is [tag-matches-plan.md](tag-matches-plan.md).
