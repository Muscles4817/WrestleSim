# The interface — what was built and why (Nov 2026)

Built from the mock-ups at https://claude.ai/artifact/S2FvTApJJvHjQeFtdx1hYy after a review on
the desktop app found the navigation confusing and several screens thin. This records the
structure, the defects that review found, and what the mock-ups showed that is **not** built.

## The structure

- **One frame for every career screen** (`Shared/Shell.razor`): promotion and level, the
  date (now the largest thing in the bar), the clock controls, cash and reputation, and a
  Menu with save, export and save-and-exit. When a show is due the clock controls become the
  one button that deals with it.
- **Six groups** (`Services/Navigation.cs`, the one table that places every screen): Home,
  Booking (calendar, your shows, brands), Talent (your roster, the market, everyone,
  contracts and offers), Stories (overview, feuds, titles), World (promotions, news) and
  Business (money, television). Each group's pages are the tab row under the bar. Contracts,
  Rivals and the money were previously reachable only through buttons inside other screens,
  or not at all.
- **Phones**: the bar compresses to the date, the money and the clock; navigation is a bottom
  bar of Home, Booking, Talent, Stories and More (World, Business and the save controls).
- **Exhibition** has its own cold-coloured bar labelled as a sandbox, its own pages, and a
  Leave exhibition button.

## Screens

- **Home**: "Needs you" (a show due or short, rival bids for your people, expiring deals,
  vacant titles, a draft due, recent news about you, acts the crowd is tiring of), each with
  the button that deals with it; the next show with its projected house and result; the
  industry; your roster; money this month; champions.
- **Talent** (`TalentScreen`): one table for your roster, the market and everyone, with
  search, division, style, age, fee and availability filters, sortable columns, an Overview /
  Ring skills column switch, who else they work for, and the row's action. It replaces a
  roster screen that listed all ninety wrestlers twice without marking the player's own.
- **Wrestler profile** (`ProfileScreen`), opened from any name: gimmick and alignment, the
  three headline figures, every ring skill and attribute as bars, the age reading, the deal or
  who holds them, rival bids, condition and injury history, touring, recent matches on the
  player's shows, feuds and titles, title history, previous names and the contract log.
- **Contracts and offers**: bids to answer, expiring deals, every deal with what they would
  ask today, your lodged offers, and the contract log. The offer form is one component
  (`OfferPanel`) shared with the table and the profile.
- **Calendar**: rival shows (run and scheduled) and deadlines (contract ends, decisions on
  your offers, the TV renewal) on the grid, a legend, and a coming-up list.
- **Card builder**: a strip of time filled, expected house, fees and expected result at the
  top, and a "Before you run it" checklist (fix / think / good) above the box office.
- **Stories**: feud cards with heat, how fresh the match-up is, when it last moved and what
  it needs next; titles with vacancy and neglect warnings; tag teams; what the crowd is tired of.
- **World**: what competition is doing to prices and interest, a table of every promotion
  with what it last did, and news. **Rival page**: what it wants, its own forecast of itself,
  its roster, last show, champions, television and every decision with its reasons.
- **Business**: cash, how long it lasts at the last four shows' rate, a typical show, fixed
  costs, cash after each show, show-by-show books, and where the money went since the books
  opened (reconciles to the cash). **Television**: the fee, the audience, the renewal if it
  ended today, the term's episodes and past terms.
- **Show report**: house, money made and reputation change beside the rating at the top.
- **New career**: a World panel saying which world the career is in.

## Defects the review found, fixed

- In exhibition, opening Roster or Feuds switched the top bar to the career's, whose Home
  then said no career was open.
- Every numeric table heading was left-aligned over right-aligned figures.
- The dashboard's "top of the card" listed the world's three most popular wrestlers, not the
  player's.
- On a phone every table was hidden (a rule left from the old roster cards).
- A screen-reader label in the talent table widened the page on a phone.

## What the mock-ups showed that is not built

These were drawn from the living-world plan and the game does not model them, so they are left
out rather than faked:

- Personality traits, contentment ("how she feels"), potential and the scouting outlook.
- Regions: no region column, filter or calendar marks.
- Choosing a generated world, a custom database, or the world's size at career start.
- A rival's offer that the wrestler weighs and decides on; rivals sign the player's regulars
  outright, and the news says so.
- "Talk to her": there is no conversation with a wrestler.
- What it would take to get television: promotions do not move between levels yet.
- Condition for wrestlers who work elsewhere: rival shows do not wear anybody out or keep them
  sharp yet, so everybody there read "ring rusty". Shown as nothing instead.

Also not redesigned: the match, segment and battle royal builders, the titles, feuds, shows and
brands detail screens. They sit in the new frame and work as before.
