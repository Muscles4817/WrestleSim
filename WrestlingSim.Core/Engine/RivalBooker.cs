using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// A rival's booker (living-world plan, Part 5). It decides **who wins**, which the world
    /// needs before a rival's night can have consequences (Part 4): popularity moves on results,
    /// and results rolled by dice took the top ten from 92 to 67 in five years the one time they
    /// were tried, because that arithmetic assumes somebody is choosing.
    ///
    /// So it chooses, the way a real booker does (docs 04, 12): it protects the people who draw,
    /// builds a few people up over months, and takes a belt off a champion whose run has gone on
    /// long enough — and somebody has to lose, because protecting everybody destroys the graph.
    /// Its **direction** (who it is building, where its belts are going) is decided once a month
    /// and kept. How well it carries that out is its **competence**.
    /// </summary>
    public static partial class World
    {
        /// <summary>
        /// Default booking competence by kind of owner: first guesses, for Part 8. Somebody
        /// who started a promotion because wrestling should be good books best; somebody who
        /// started one to be on top of it books worst.
        /// </summary>
        public static double CompetenceFor(FounderMotive m) => m switch
        {
            FounderMotive.Ideology  => 0.88,
            FounderMotive.Business  => 0.82,
            FounderMotive.Legacy    => 0.82,
            FounderMotive.Platform  => 0.78,
            FounderMotive.MarkMoney => 0.72,
            FounderMotive.Vanity    => 0.65,
            _                       => 0.8
        };

        public static double CompetenceOf(RivalPromotion rival) => rival.Competence ?? CompetenceFor(rival.Motive);

        /// <summary>
        /// A promotion's own booking competence: its kind of owner's default, give or take a
        /// little, from its identity — two vanity projects are not equally badly run.
        /// </summary>
        public static double DrawCompetence(RivalPromotion rival) =>
            Math.Round(Math.Clamp(CompetenceFor(rival.Motive) + (new Random(StableSeed.From(rival.Id, "competence")).NextDouble() * 2 - 1) * 0.08, 0.4, 0.97), 2);

        /// <summary>How long a reign usually runs before the booker plans its end (doc 21 §4: months, not weeks).</summary>
        public const int ReignBeforeChange = 150;

        /// <summary>
        /// The month's direction. Keeps whoever it is already building if they still fit and the
        /// audience has not refused them; fills the rest from the people the audience could come
        /// to pay for — charisma, ability, youth, the owner's own favourites — who are not
        /// already made. Plans each belt's next champion when the current reign has run its
        /// course, from the people it is building first.
        /// </summary>
        public static BookingDirection Direct(Career career, RivalPromotion rival)
        {
            var today = career.CurrentDate;
            var d = rival.Direction ?? new BookingDirection();
            d.Decided = today;

            var people = rival.Contracts.Select(c => career.FindWrestler(c.WrestlerId)).OfType<Wrestler>()
                .Where(w => !w.IsRetired).ToList();
            var favour = new HashSet<string>(rival.Favourites);
            foreach (var key in d.Rejected.Where(r => today.DayNumber - r.Value.DayNumber > 365).Select(r => r.Key).ToList())
                d.Rejected.Remove(key);

            // A push the audience has refused is dropped, and not tried again for a year
            // (doc 17 §9: about a third of sustained pushes are). It is news when it mattered.
            foreach (var id in d.Pushes.ToList())
                if (people.FirstOrDefault(w => w.Id == id) is { RejectedSince: not null } refused)
                {
                    d.Pushes.Remove(id);
                    d.Rejected[id] = today;
                    career.AddNews($"{rival.Name} has backed off {refused.RingName}'s push after the crowd turned on it.");
                }

            bool Eligible(Wrestler w) => !w.IsPartTime && w.Overness < 75 && (w.AgeOn(today) ?? 30) <= 34
                                         && !d.Rejected.ContainsKey(w.Id);
            double Promise(Wrestler w) =>
                0.45 * Math.Clamp(w.Charisma, 0, 5) * 20
              + 0.30 * w.RingSkills.GetOverallSkill() * 20
              + 0.25 * w.EffectiveOverness
              + Math.Max(0, 30 - (w.AgeOn(today) ?? 30))
              + (favour.Contains(w.Id) ? 15 : 0);

            int places = rival.Promotion.HasTelevision ? 3 : 1;
            d.Pushes = d.Pushes.Where(id => people.FirstOrDefault(w => w.Id == id) is { } w && Eligible(w)).ToList();
            foreach (var w in people.Where(w => Eligible(w) && !d.Pushes.Contains(w.Id))
                                    .OrderByDescending(Promise).ThenBy(w => w.Id, StringComparer.Ordinal))
            {
                if (d.Pushes.Count >= places) break;
                d.Pushes.Add(w.Id);
            }

            d.NextChampion.Clear();
            foreach (var title in rival.Titles.Active.Where(t => t.SideSize == 1))
            {
                var champion = title.Champion;
                bool due = champion is null || champion.IsRetired || !people.Contains(champion)
                        || title.CurrentReignDays(today) >= ReignBeforeChange;
                if (!due) continue;
                var next = people.Where(w => w.Division == title.Division && w != champion && !w.IsPartTime)
                    .OrderByDescending(w => d.Pushes.Contains(w.Id) ? 1 : 0)
                    .ThenByDescending(w => w.EffectiveOverness)
                    .ThenBy(w => w.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (next is not null) d.NextChampion[title.Name] = next.Id;
            }

            rival.Direction = d;
            return d;
        }

        /// <summary>
        /// What winning is worth to the booker, for this person: how much the audience will
        /// pay to see them, plus a push, a belt, or the owner's favour. The side worth more wins.
        /// </summary>
        public static double BookingValue(RivalPromotion rival, Wrestler w)
        {
            var d = rival.Direction;
            double value = w.EffectiveOverness;
            if (d?.Pushes.Contains(w.Id) == true) value += 20;
            if (rival.Titles.HeldBy(w).Any()) value += 10;
            if (rival.Favourites.Contains(w.Id)) value += 10;
            return value;
        }

        /// <summary>
        /// Who wins: the planned new champion on the night their title is defended on a big show;
        /// otherwise the side the booker judges worth more. A booker short of competence judges
        /// worse, which is how a badly run promotion books badly for reasons in the model.
        /// </summary>
        public static int Winner(RivalPromotion rival, BookedMatch match, ShowType type, Random rng)
        {
            var sides = match.Plan.Sides;
            int planned;
            if (match.Plan.TitleAtStake is { } title && title.Champion is { } champion)
            {
                int held = Math.Max(0, sides.FindIndex(s => s.Members.Contains(champion)));
                planned = held;
                if (type == ShowType.PremiumEvent && rival.Direction?.NextChampion.TryGetValue(title.Name, out var next) == true)
                {
                    int to = sides.FindIndex(s => s.Members.Any(w => w.Id == next));
                    if (to >= 0) planned = to;
                }
            }
            else
            {
                // A booker short of competence misjudges who is worth more — by up to about
                // thirty points at the worst — rather than sending the wrong side over at random.
                // A real bad call is an upset between people near each other, not the opener's
                // jobber pinning the main eventer: the first version flipped a random fifth to a
                // third of results and took the established stars from ninety to forty in five
                // years (measured 15 Nov).
                double misjudge = (1 - CompetenceOf(rival)) * 30;
                planned = sides.Select((s, i) => (i, Value: s.Members.Max(w => BookingValue(rival, w))
                                                          + (rng.NextDouble() * 2 - 1) * misjudge))
                    .OrderByDescending(x => x.Value).ThenBy(x => x.i).First().i;
            }
            return planned;
        }

        /// <summary>
        /// Turns a planned match into one the engine can work, the way the player's quick path
        /// does: a brief (who goes over, what it is about, how long, how it ends) written into
        /// beats by <see cref="BriefDirector"/>. Somebody worth protecting who loses loses by a
        /// stolen finish rather than clean.
        /// </summary>
        public static void Write(RivalPromotion rival, BookedMatch match, int winner, ShowType type, bool mainEvent, bool opener,
                                 int minutes = 0)
        {
            var sides = match.Plan.Sides;
            double loserValue = sides.Where((_, i) => i != winner).SelectMany(s => s.Members).Select(w => BookingValue(rival, w)).DefaultIfEmpty(0).Max();
            double winnerValue = sides[winner].Members.Max(w => BookingValue(rival, w));

            var brief = new MatchBrief
            {
                WinningSide = winner,
                Story = winnerValue - loserValue >= 25 ? MatchStory.Showcase
                      : match.Plan.TitleAtStake is not null || mainEvent ? MatchStory.FaceInPeril
                      : MatchStory.EvenContest,
                Length = mainEvent && type == ShowType.PremiumEvent ? MatchScale.BigMatch
                       : mainEvent || match.Plan.TitleAtStake is not null ? MatchScale.Workhorse
                       : opener ? MatchScale.Opener
                       : MatchScale.Television,
                Finish = loserValue >= 60 && loserValue > winnerValue ? FinishKind.Stolen
                       : winnerValue - loserValue >= 25 ? FinishKind.Dominant
                       : FinishKind.Clean
            };
            match.Plan.Brief = brief;

            // The time it has: a booker lays the card out to fill the show. The scale above is
            // where it starts; it is lengthened until the match takes its share of the night
            // (measured 15 Nov: written at the scale alone, rival cards filled half to three
            // quarters of their running time and the audience marked them down for it).
            var written = BriefDirector.Write(brief, sides);
            while (minutes > 0 && written.Minutes + BookedMatch.EntranceMinutes < minutes && brief.Length < MatchScale.Epic)
            {
                brief.Length++;
                written = BriefDirector.Write(brief, sides);
            }
            match.Plan.Beats.Clear();
            match.Plan.Beats.AddRange(written.Beats);
        }
    }
}
