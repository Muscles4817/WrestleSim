using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Rumble;
using WrestlingSim.Models.World;
using SegmentItem = WrestlingSim.Models.Segment.Segment;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// How an audience tires. docs/wrestling-reference/17 §3–4, 16 §4.10, 23 §2–3.
    ///
    /// **Sameness, not screen time.** The first design of this counted appearances. Research
    /// overturned it: liking for a character rises with exposure, advertising barely wears out
    /// from repetition, and boredom follows low novelty. Rhea Ripley on screen every week with a
    /// story that moves holds; the same act headlining for years does not. So an appearance only
    /// costs anything when it carried nothing new, and something new takes the cost back off.
    ///
    /// Three things, kept separate on purpose:
    ///
    ///   1. A wrestler's *act* goes stale — at a rate that is the wrestler's own: charisma and a
    ///      gimmick with somewhere to go slow it, novelty that rests on athletic spots wears
    ///      faster (doc 17 §3.4).
    ///   2. Each *part of the audience* feels it differently. The weekly viewer sees everything
    ///      and tires first; the casual viewer wants the familiar star; the hardcore fan wants
    ///      novelty and tires of dominance (doc 23 §2).
    ///   3. The *show* goes stale, whoever is on it — the same faces in the same kinds of match.
    ///
    /// The fastest-decaying thing in wrestling, the match-up itself, is not here: it is
    /// <see cref="Models.MatchPlan.Feud.Familiarity"/>, and it already existed.
    ///
    /// Every number in this file is a design estimate tuned against doc 17 §4.1's ranges.
    /// Nobody has measured these for wrestling.
    /// </summary>
    public static class AudienceModel
    {
        // ── Rates ────────────────────────────────────────────────────────────

        /// <summary>Sameness added by one appearance that carried nothing new, at rate 1.</summary>
        public const double Gain = 5;

        /// <summary>Sameness removed by one appearance that was entirely new.</summary>
        public const double Relief = 6;

        /// <summary>Off screen, an act's sameness halves in ten weeks. Rest is slow relief, not a reset.</summary>
        public const double ActHalfLifeDays = 70;

        /// <summary>A show's sameness halves in four weeks, so a promotion that runs rarely never accumulates it.</summary>
        public const double ShowHalfLifeDays = 28;

        /// <summary>
        /// How fast this wrestler's act wears, around 1.0. The owner's reading and doc 17's:
        /// it "varies… affected by the charisma and gimmick of the wrestler".
        ///
        ///   • Charisma (0–5): a 5 wears at 0.6×, a 0 at 1.5×.
        ///   • <see cref="GimmickDurability"/>: the roster already says how long each gimmick
        ///     lasts — short-term 1.3×, long-term 0.7×. Nothing read it until now.
        ///   • A high-flyer's novelty is athletic, and "athletic novelty depletes faster than
        ///     character novelty" (doc 17 §3.4): 1.15×.
        /// </summary>
        public static double Rate(Wrestler w)
        {
            double rate = 1.5 - 0.18 * Math.Clamp(w.Charisma, 0, 5);
            rate *= w.Gimmick?.Durability switch
            {
                GimmickDurability.ShortTerm => 1.3,
                GimmickDurability.LongTerm  => 0.7,
                _                           => 1.0
            };
            if (w.Style == WrestlingStyle.HighFlyer) rate *= 1.15;
            return rate;
        }

        // ── Who is watching ──────────────────────────────────────────────────

        /// <summary>
        /// What each part of the audience will sit through. Comfort is the sameness below which
        /// it does not mind; depth is how far its response can fall beyond that.
        /// </summary>
        public static (double Comfort, double Depth, bool TiresOfDominance) Taste(FanGroup g) => g switch
        {
            // Wants recognisable stars and no homework. Sees little, minds little.
            FanGroup.Casual        => (70, 0.15, false),
            FanGroup.Families      => (75, 0.10, false),
            FanGroup.Kids          => (75, 0.10, false),
            // Watches every week and follows the stories: the first to notice nothing moved.
            FanGroup.Regular       => (35, 0.45, false),
            FanGroup.International => (35, 0.45, false),
            // Wants novelty, and tires of somebody who never loses.
            FanGroup.Hardcore      => (25, 0.55, true),
            FanGroup.Online        => (25, 0.55, true),
            _                      => (50, 0.30, false)
        };

        /// <summary>How one group responds to an act this stale, 1.0 being undiminished.</summary>
        public static double GroupResponse(FanGroup g, double sameness, int streak)
        {
            var (comfort, depth, dominance) = Taste(g);
            double over = Math.Clamp((sameness - comfort) / (100 - comfort), 0, 1);
            double response = 1 - depth * over;
            if (dominance) response -= Math.Min(0.20, 0.03 * Math.Max(0, streak - 5));
            return Math.Max(0.25, response);
        }

        /// <summary>
        /// Who is in the building or watching, by share. Weekly television is made for the
        /// regular viewer; the big night brings the casual one back (doc 23 §2.1–2.2); an indie
        /// crowd leans hardcore (doc 23, "segment composition varies by market").
        /// </summary>
        public static IReadOnlyList<(FanGroup Group, double Share)> Mix(PromotionTier tier, ShowType type)
        {
            if (tier == PromotionTier.SuperIndie)
                return [(FanGroup.Hardcore, 0.40), (FanGroup.Regular, 0.40), (FanGroup.Casual, 0.20)];
            if (tier < PromotionTier.SuperIndie)
                return [(FanGroup.Regular, 0.45), (FanGroup.Casual, 0.35), (FanGroup.Hardcore, 0.20)];

            return type switch
            {
                ShowType.PremiumEvent => [(FanGroup.Casual, 0.45), (FanGroup.Regular, 0.35), (FanGroup.Hardcore, 0.20)],
                ShowType.HouseShow    => [(FanGroup.Casual, 0.50), (FanGroup.Regular, 0.35), (FanGroup.Hardcore, 0.15)],
                _                     => [(FanGroup.Regular, 0.50), (FanGroup.Casual, 0.30), (FanGroup.Hardcore, 0.20)]
            };
        }

        /// <summary>
        /// A gimmick's appeal to a group, 0–1. Regular has no rating in the roster and reads
        /// Casual's — the two differ in how often they watch, not in who they like. Unrated
        /// groups read a neutral 0.5.
        /// </summary>
        public static double Appeal(Wrestler w, FanGroup g)
        {
            var ratings = w.Gimmick?.AppealRatings;
            if (ratings is null) return 0.5;

            double? Of(FanGroup group) => ratings
                .FirstOrDefault(r => string.Equals(r.Group, group.ToString(), StringComparison.OrdinalIgnoreCase))
                ?.AppealScore;

            return Of(g) ?? (g == FanGroup.Regular ? Of(FanGroup.Casual) : null) ?? 0.5;
        }

        /// <summary>
        /// How much of a wrestler's pull this audience still feels, 1.0 being all of it. Each
        /// group's response, weighted by how much of the room it is and how much it likes them —
        /// so a casual favourite is carried by the people who do not mind, which is how one
        /// wrestler is hot with one crowd and stale with another.
        /// </summary>
        public static double Response(Audience? audience, Wrestler w, PromotionTier tier, ShowType type,
                                      int daysAhead = 0)
        {
            var act = audience?.ActOf(w);
            if (act is null) return 1.0;

            // A show booked for next month is watched next month: the act will have had that
            // long off the screen by then.
            double sameness = act.Sameness * Math.Pow(0.5, Math.Max(0, daysAhead) / ActHalfLifeDays);

            double total = 0, weights = 0;
            foreach (var (group, share) in Mix(tier, type))
            {
                double weight = share * (0.5 + Appeal(w, group));
                total += weight * GroupResponse(group, sameness, act.Streak);
                weights += weight;
            }
            return weights <= 0 ? 1.0 : total / weights;
        }

        /// <summary>
        /// The same reading for a sameness and a streak handed in — for a forecast, which
        /// carries its own count of shows that have not happened.
        /// </summary>
        public static double ResponseAt(Wrestler w, double sameness, int streak, PromotionTier tier, ShowType type)
        {
            double total = 0, weights = 0;
            foreach (var (group, share) in Mix(tier, type))
            {
                double weight = share * (0.5 + Appeal(w, group));
                total += weight * GroupResponse(group, sameness, streak);
                weights += weight;
            }
            return weights <= 0 ? 1.0 : total / weights;
        }

        /// <summary>Why an act is wearing, in words, or null when it is not.</summary>
        public static string? Reading(Audience? audience, Wrestler w)
        {
            var act = audience?.ActOf(w);
            if (act is null) return null;

            double regular  = GroupResponse(FanGroup.Regular, act.Sameness, act.Streak);
            double hardcore = GroupResponse(FanGroup.Hardcore, act.Sameness, act.Streak);
            double casual   = GroupResponse(FanGroup.Casual, act.Sameness, act.Streak);

            if (casual < 0.95)
                return "the act has gone stale with everybody — it needs something new";
            if (regular < 0.90)
                return "the weekly audience has seen this act too often with nothing new; casual fans do not mind";
            if (hardcore < 0.90)
                return act.Streak > 5
                    ? $"{act.Streak} wins in a row — the hardcore fans are tiring of it; everybody else is fine"
                    : "the hardcore fans are tiring of the act; everybody else is fine";
            return null;
        }

        // ── Time ─────────────────────────────────────────────────────────────

        public static void Decay(Audience audience, int days = 1)
        {
            if (days <= 0) return;
            double acts = Math.Pow(0.5, days / ActHalfLifeDays);
            foreach (var act in audience.Acts.Values) act.Sameness *= acts;
            audience.ShowSameness *= Math.Pow(0.5, days / ShowHalfLifeDays);
        }

        // ── A show ───────────────────────────────────────────────────────────

        /// <summary>What kind of thing an item is, as the audience would describe it.</summary>
        public static string KindOf(ICardItem item) => item switch
        {
            BookedMatch m => $"match:{m.Plan.Sides.Count}x{m.Plan.Sides.Max(s => s.Members.Count)}" +
                             (m.Plan.Stipulation != Stipulation.None ? $":{m.Plan.Stipulation}" : "") +
                             (m.Plan.IsTitleMatch ? ":title" : ""),
            SegmentItem s => $"segment:{s.Type}",
            RumblePlan    => "rumble",
            RumbleDraw    => "draw",
            _             => item.Kind.ToString()
        };

        /// <summary>
        /// How new each wrestler's part in this card is, 0–1, **read before the show runs** —
        /// running a match moves the familiarity this reads. An opponent the crowd has not seen
        /// them with is new; so is a different kind of match, or a different gimmick.
        /// </summary>
        public static Dictionary<string, double> Novelty(
            Audience audience, IEnumerable<ICardItem> card, FeudBook? feuds, DateOnly on)
        {
            var novelty = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in card)
            {
                string kind = KindOf(item);
                double opponent = item switch
                {
                    BookedMatch m => OpponentNovelty(m, feuds, on),
                    SegmentItem   => 0.15,   // talking is how an act moves, a little, every time
                    _             => 0.5     // a battle royal is a different night
                };

                foreach (var w in item.Wrestlers)
                {
                    var act = audience.ActOf(w);
                    double n = opponent;
                    if (act is null || act.LastSeen is null) n = 1.0;                // a debut
                    else
                    {
                        if (act.LastKind != kind) n += 0.3;
                        if (act.LastGimmick.Length > 0 && act.LastGimmick != (w.Gimmick?.Name ?? "")) n = 1.0;
                    }
                    n = Math.Min(1.0, n);

                    // Twice on one night: the second is not new because the first was.
                    novelty[w.Id] = novelty.TryGetValue(w.Id, out var first) ? Math.Min(first, n) * 0.5 : n;
                }
            }
            return novelty;
        }

        /// <summary>First time together is new; by the third it is not.</summary>
        private static double OpponentNovelty(BookedMatch m, FeudBook? feuds, DateOnly on)
        {
            double met = BoxOffice.StoryBetween(m.Plan, feuds)?.MeetingsRemembered(on) ?? 0;
            return met < 0.5 ? 1.0 : met < 1.5 ? 0.5 : met < 2.5 ? 0.25 : 0.0;
        }

        /// <summary>
        /// The show has happened. Each act moves by how new its part was — with a story that
        /// advanced or a title that changed counted as new after the fact — and the show's own
        /// sameness moves by how like the last few it was.
        /// </summary>
        public static AudienceReport Observe(
            Audience audience, ScheduledShow show, IReadOnlyDictionary<string, double> novelty, ShowResult result,
            IReadOnlySet<string>? winnerIds = null)
        {
            double before = audience.ShowSameness;

            // Something happened to them tonight: that is new, whatever the card looked like.
            var moved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in result.FeudUpdates.Where(u => u.Escalated || u.NewTags.Count > 0))
                foreach (var w in show.Card.SelectMany(i => i.Wrestlers).Where(u.Feud.Involves))
                    moved.Add(w.Id);

            var crowned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in result.TitleUpdates.Where(t => t.Event is not (TitleEvent.Retained or TitleEvent.NonTitleLoss)))
                foreach (var w in t.Champions.Concat(t.OutgoingChampions)
                                             .Append(t.Champion).Append(t.OutgoingChampion).OfType<Wrestler>())
                    crowned.Add(w.Id);

            // The booked winners, read off the beat sheet — or handed in, for a show whose
            // matches were not worked beat by beat and so have no sheet to read.
            var winners = winnerIds ?? show.Card.OfType<BookedMatch>()
                .SelectMany(m => m.Plan.BookedWinningSide?.Members ?? [])
                .Select(w => w.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var wrestled = show.Card.OfType<BookedMatch>()
                .SelectMany(m => m.Wrestlers).Select(w => w.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var lastKind = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in show.Card)
                foreach (var w in item.Wrestlers) lastKind[w.Id] = KindOf(item);

            var notes = new List<(double Sameness, string Text)>();
            foreach (var w in show.Card.SelectMany(i => i.Wrestlers).Distinct())
            {
                if (!audience.Acts.TryGetValue(w.Id, out var act))
                    audience.Acts[w.Id] = act = new Act();

                double n = novelty.GetValueOrDefault(w.Id, 0);
                if (moved.Contains(w.Id)) n += 0.6;
                if (crowned.Contains(w.Id)) n = 1.0;
                n = Math.Min(1.0, n);

                act.Sameness = Math.Clamp(act.Sameness + Gain * Rate(w) * (1 - n) - Relief * n, 0, 100);

                if (winners.Contains(w.Id)) act.Streak++;
                else if (wrestled.Contains(w.Id)) act.Streak = 0;

                act.LastKind    = lastKind[w.Id];
                act.LastGimmick = w.Gimmick?.Name ?? "";
                act.LastSeen    = show.Date;

                // Doc 17 §3.2: nothing goes wrong, the reactions just get smaller.
                if (act.Sameness > 50) w.Momentum = Math.Max(-100, w.Momentum - 1.5);

                if (Reading(audience, w) is { } reading) notes.Add((act.Sameness, $"{w.RingName}: {reading}."));
            }

            // The show against the last few, the recent ones counting most.
            var shape = new ShowShape
            {
                Date    = show.Date,
                Cast    = show.Card.SelectMany(i => i.Wrestlers).Select(w => w.Id).Distinct().ToList(),
                Formats = show.Card.Select(KindOf).ToList()
            };
            audience.Recent.RemoveAll(s => show.Date.DayNumber - s.Date.DayNumber > 60);
            if (audience.Recent.Count > 0)
            {
                double target = 100 * Math.Clamp((Similarity(audience.Recent, shape) - 0.35) / 0.65, 0, 1);
                audience.ShowSameness += 0.5 * (target - audience.ShowSameness);
            }
            audience.Recent.Add(shape);
            if (audience.Recent.Count > 4) audience.Recent.RemoveAt(0);

            double filled = show.RuntimeMinutes <= 0 ? 1.0 : show.BookedMinutes / (double)show.RuntimeMinutes;

            return new AudienceReport
            {
                ShowSamenessBefore = before,
                ShowSamenessAfter  = audience.ShowSameness,
                SamenessPenalty    = SamenessPenalty(audience.ShowSameness),
                Filled             = filled,
                UnderrunPenalty    = show.Type == ShowType.HouseShow ? 0 : UnderrunPenalty(filled),
                Notes              = notes.OrderByDescending(x => x.Sameness).Select(x => x.Text).Take(5).ToList()
            };
        }

        /// <summary>
        /// How like the recent shows this one is, 0–1: mostly who was on it, partly what it was
        /// made of. A show weeks ago counts for less than last week's.
        /// </summary>
        public static double Similarity(IReadOnlyList<ShowShape> recent, ShowShape shape)
        {
            double total = 0, weights = 0, nearest = 0;
            foreach (var past in recent)
            {
                int age = Math.Max(0, shape.Date.DayNumber - past.Date.DayNumber);
                double weight = Math.Pow(0.5, age / ShowHalfLifeDays);
                nearest = Math.Max(nearest, weight);

                // How many of tonight's faces were on that show. Containment, not overlap of
                // the two casts: the audience's question is "have I just seen these people",
                // and a rotating four out of the same six is still the same six.
                var cast = past.Cast.Intersect(shape.Cast, StringComparer.OrdinalIgnoreCase).Count();
                double who = shape.Cast.Count == 0 ? 0 : cast / (double)shape.Cast.Count;

                var theirs = past.Formats.GroupBy(f => f).ToDictionary(g => g.Key, g => g.Count());
                int common = shape.Formats.GroupBy(f => f).Sum(g => Math.Min(g.Count(), theirs.GetValueOrDefault(g.Key)));
                int most = Math.Max(past.Formats.Count, shape.Formats.Count);
                double what = most == 0 ? 0 : common / (double)most;

                total += weight * (0.6 * who + 0.4 * what);
                weights += weight;
            }

            // Recent shows count most among themselves, and the whole comparison fades with
            // the gap since the last one: an identical show six weeks ago is not the same show
            // to the people watching tonight. (The first version divided by the count, which
            // diluted last week's show with the weeks before it and barely registered a cast
            // that never changed.)
            return weights <= 0 ? 0 : total / weights * nearest;
        }

        /// <summary>Up to a fifth off the show for being the show they saw last week.</summary>
        public static double SamenessPenalty(double showSameness) =>
            0.20 * Math.Clamp((showSameness - 30) / 70, 0, 1);

        /// <summary>
        /// How much of a slot has to be filled before nobody notices: the rest is entrances,
        /// packages and breaks. Doc 29 §6: a two-hour television show is four to six matches
        /// and as many segments. Half, because that is what the game's own guidance comes to —
        /// a card of `TypicalCardSizeFor` television-length matches fills 51–76% of
        /// `DefaultRuntimeFor` across the tiers, and a typical card must not read as thin.
        /// </summary>
        public const double FullCard = 0.50;

        /// <summary>
        /// A slot left under-filled is a worse show, as one that overruns already is. Free down
        /// to <see cref="FullCard"/> of the runtime; up to 30% off below that.
        /// </summary>
        public static double UnderrunPenalty(double filled) =>
            Math.Min(0.30, Math.Max(0, FullCard - filled) * 0.6);

        /// <summary>
        /// What a thin card does to the house and the tune-in, 0.5–1.0. A ticket buys an
        /// evening and a viewer sits down for a show; two matches is neither. Read from the card
        /// as advertised, like everything else that sells the ticket.
        /// </summary>
        public static double CardDepth(ScheduledShow show)
        {
            if (show.Type == ShowType.HouseShow || show.RuntimeMinutes <= 0 || show.Card.Count == 0) return 1.0;
            double filled = show.BookedMinutes / (double)show.RuntimeMinutes;
            return 0.5 + 0.5 * Math.Min(1.0, filled / FullCard);
        }
    }
}
