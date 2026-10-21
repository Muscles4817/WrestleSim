# Exhibition UX — the diagnosis

The work queue lives in [#80](https://github.com/Muscles4817/WrestleSim/issues/80); each item
is its own issue. This file is the reasoning behind the order, which is the part that does not
belong in any single ticket.

Screenshots in [`ux-review/`](ux-review/) were taken from the running app — Playwright at
1440×900 and 390×844, booking Charlotte Flair vs Becky Lynch end to end. That matters: half of
what follows is invisible when reading the source, and #74 could not have been found any other
way.

## What is actually wrong

Not the craft. The spacing is disciplined, the colour ramps are reasoned, and the copy is
better than most shipped games manage — the `BriefCritique` notes and the beat library's booker
tips are genuinely good writing about wrestling.

The problem is that **every screen is assembled from the same four parts**: an eyebrow, a large
title, a muted lede, and a grid of grey rounded rectangles. There are 31 eyebrows and 23 ledes
in the app. Nothing distinguishes the screen where you choose who the match is about from the
screen where you read what happened, because both are a stack of `--panel` cards with 1px
borders at one elevation.

Layered on that: there is no typeface (the whole game runs on the system UI font), no imagery
(76 wrestlers, no faces), and no motion (12 transitions in 1473 lines of CSS, all hover
colours). Those three absences are most of what "lacks personality and energy" means in
practice. A dark theme with a gold accent is not an identity; it is the absence of one.

## The two orderings that were considered

**Depth first** — fix the scoring legibility and the beat editor, then make it look good. This
is the tempting order because those are the most *substantial* issues, and it is the wrong one.
The game already has more depth than it shows; adding more while every screen still reads as an
admin console means the depth stays invisible.

**Identity first** — typeface, icons, motion, the result reveal, then depth. This is the order
in #80. A single display face and a result page that lands changes the felt quality of every
screen at once, including the ones nobody has touched yet. It also makes the depth work worth
doing, because by then there is a surface worth putting it on.

The exception at the top of pass 1 is #74, the scroll bug, which goes first only because it is
ten minutes of work and #75 is meaningless while the result opens below the fold.

## The thing worth protecting

The brief step. "a television match built as an even contest. Charlotte Flair goes over clean."
is a real idea with a voice — the player writes a sentence rather than filling in a form, and
the written sheet that follows it (phase, beat, the reason it is there, who works it) is the
best screen in the application.

It is also the measure for the rest. The beat editor one click later shows the same six beats
with the phase, the reason and the worker all stripped out (#60) — which is the whole problem
in miniature: the game knows more than it shows, and here it demonstrably knows, because it
showed it on the previous screen.

## What "done" looks like

A player who has never read a word of documentation should be able to book a match, watch it
happen, understand why it scored what it did, and want to book another one immediately. Today
they can do the first and the fourth.
