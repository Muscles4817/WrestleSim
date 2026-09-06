# Working agreements

## Subagents: the ten-minute rule

Long-running review agents caused a real failure on this repo. Two PRs sat open for three
hours in a review loop while the user was asleep, and the loop was only broken when they woke
up and asked whether I was stuck. The rules below exist because of that.

**Whenever a subagent is spawned, start a ten-minute timer in the same message:**

```
Bash({ command: "sleep 600", run_in_background: true,
       description: "10-min check on <agent name>" })
```

When it fires, look at the agent's progress and answer three questions:

1. **Is it doing the task it was given**, or has it wandered into something adjacent?
2. **Is it converging?** Compare against the last check. More findings than last time is a
   warning sign, not a success sign.
3. **Would I act on what it has produced so far?** If not, it does not get another ten
   minutes.

If any answer is bad: `TaskStop` it. Do not let it run on in the hope it improves. Re-arm the
timer if it passes the check.

## Review rounds have a limit

**Two rounds per PR by default.** A third needs a reason that is not "the last round found
something", because a review round will *always* find something.

**Stop when the findings change character.** Real defects — wrong output, data corruption, a
user-reachable bug — justify another round. Findings that a constant is not tightly pinned, or
that a mutation of a threshold survives, are test-adequacy notes: log them and merge. Mutation
testing has unbounded depth and cannot be exhausted; "review until a round comes back clean"
is not a stopping rule, it is an infinite loop.

**Watch for the loop being self-sustaining.** On this repo each round's fixes were generating
the next round's blockers — the roster picker's keyboard cursor was broken and fixed three
times, each fix breaking it a new way. If a round's blockers were introduced by the previous
round's fixes, the churn is the problem. Stop editing and merge.

**Never review a review.** No agent whose task is to check another agent's findings.

## Delivering

- If the user has said they are going away and want to wake to finished work, **finish it**.
  Merging with two logged follow-ups beats leaving a PR open and perfect.
- Log deferred items as tasks with a reason. Do not fix them in-flight on a branch under
  review; that is what creates the next round's blockers.
- Clean up: kill dev servers, remove `/tmp` clones and git worktrees when done. A reviewer's
  `nohup` dev server outlives the agent that started it and must be killed by hand.

## This repo

- **The roster is real WWE wrestlers.** `WrestlingSim.Core/JSON/Wrestlers.json` uses
  `RealName` for the performer and `Gimmick.Name` for the character. Do not invent wrestlers.
- **`MatchEngine.TypicalInvestment` is the measured median of that roster.** Any roster change
  moves it. `ATypicalMatchIsUnmoved_AndThatIsMeasuredNotAsserted` fails when the two part
  company — re-measure and update the constant, do not widen the test.
- **Corpus tests must use `StableSeed.From(...)`, never `HashCode.Combine`,** which .NET
  randomises per process, so figures from it are one sample and not reproducible.
- Documentation lives in `docs/tag-matches-design-record.md` (what the engine does and why)
  and `docs/tag-matches-review-record.md` (what review found). Corrections go inline next to
  the claim they correct, not into a footnote.
