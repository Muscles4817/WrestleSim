using WrestlingSim.Enums;
using WrestlingSim.Models;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// What kind of result each kind of character wants — docs/gimmick-plan.md Part 2, the fix
    /// for a roster that played as interchangeable. Two wrestlers with the same numbers used to
    /// be the same wrestler; now a monster beaten clean pays far more for it than a showman
    /// would, an underdog who loses a close one barely pays at all, and a heel who steals a
    /// win is doing their job.
    ///
    /// One table, read in three places, so they cannot disagree:
    ///   • the show (<see cref="ShowSimulator"/>), after the bell, on what the result did;
    ///   • the builder (<see cref="BriefCritique.Character"/>), before it, on what it will do;
    ///   • the profile, on what this character is fed by and hurt by.
    ///
    /// A multiplier scales the standing a result moves — the gain for a win, the cost for a
    /// loss — and never flips its sign. Every number is a design estimate against doc 19 §2.1
    /// and doc 12; none is measured.
    /// </summary>
    public static class CharacterTypes
    {
        /// <summary>The result as a character would read it.</summary>
        public enum ResultKind
        {
            /// <summary>Won clean, having had the match their own way.</summary>
            DominantWin,

            /// <summary>Won clean, in a match they had to fight for.</summary>
            HardWin,

            /// <summary>Won by a roll-up, a run-in, a disqualification.</summary>
            CheapWin,

            /// <summary>Beaten clean, having had a real share of the match.</summary>
            CloseLoss,

            /// <summary>Beaten clean, and beaten up on the way.</summary>
            BeatenLoss,

            /// <summary>Lost in a way the audience forgives.</summary>
            CheapLoss
        }

        /// <summary>A side's share of the match above this, in a clean win, is having it their own way.</summary>
        public const double DominantShare = 0.70;

        /// <summary>A loser's share at or above this was a close one.</summary>
        public const double CloseShare = 0.35;

        public static ResultKind Read(bool won, FinishWeight finish, double ownShare) =>
            (won, finish) switch
            {
                (true, FinishWeight.Decisive)  => ownShare >= DominantShare ? ResultKind.DominantWin : ResultKind.HardWin,
                (true, _)                      => ResultKind.CheapWin,
                (false, FinishWeight.Decisive) => ownShare >= CloseShare ? ResultKind.CloseLoss : ResultKind.BeatenLoss,
                _                              => ResultKind.CheapLoss
            };

        /// <summary>
        /// How much of the match a side was *booked* to have, 0–1: what the result says about
        /// them depends on the match they were given. Read from the brief — the story, and how
        /// each side was booked to come out — so the builder's warning before the bell and the
        /// show's reading after it are the same reading. A match with no brief reads as a
        /// fought one either way.
        ///
        /// (The first version measured it from the beats each side worked, and a showcase came
        /// out at 57% for its winner against an even contest's 67%: who works a beat is not who
        /// is on top in it.)
        /// </summary>
        public static double ShareFor(Models.MatchPlan.MatchBrief? brief, bool won, int side)
        {
            if (brief is null) return won ? 0.5 : 0.4;
            var booked = brief.Bookings.TryGetValue(side, out var b) ? b : Booking.Even;
            return won
                ? (brief.Story == MatchStory.Showcase || brief.Finish == FinishKind.Dominant ? 0.85 : 0.5)
                : (brief.Story == MatchStory.Showcase || booked == Booking.Diminished ? 0.15
                   : booked is Booking.Protected or Booking.Elevated ? 0.45 : 0.4);
        }

        public static bool IsWin(ResultKind k) => k is ResultKind.DominantWin or ResultKind.HardWin or ResultKind.CheapWin;

        /// <summary>
        /// One type's appetite: a multiplier for each kind of result, and what it means in a
        /// sentence. 1.0 is a result read the way it always was.
        /// </summary>
        public sealed record Appetite(
            double DominantWin, double HardWin, double CheapWin,
            double CloseLoss, double BeatenLoss, double CheapLoss,
            string FedBy, string HurtBy)
        {
            public double Of(ResultKind k) => k switch
            {
                ResultKind.DominantWin => DominantWin,
                ResultKind.HardWin     => HardWin,
                ResultKind.CheapWin    => CheapWin,
                ResultKind.CloseLoss   => CloseLoss,
                ResultKind.BeatenLoss  => BeatenLoss,
                _                      => CheapLoss
            };
        }

        private static readonly Appetite Neutral = new(1, 1, 1, 1, 1, 1, "results read plainly", "nothing in particular");

        /// <summary>
        /// The table. Doc 19 §2.1 for the archetypes' durability and failure modes; doc 12 for
        /// protection; doc 17 §6 for heat moving through results.
        /// </summary>
        public static Appetite For(GimmickType? type) => type switch
        {
            // "Requires protection; collapses when beaten too often."
            GimmickType.Monster => new(1.5, 0.8, 0.6, 1.8, 1.8, 0.8,
                "dominant wins", "being beaten clean, and having to struggle"),

            // "Must eventually win something." A squash is not an underdog's win; a close loss
            // to a bigger name is the character working.
            GimmickType.Underdog => new(0.6, 1.3, 1.0, 0.4, 1.0, 0.8,
                "hard-fought wins and close losses", "squashes, either way round"),

            GimmickType.Superstar => new(1.1, 1.0, 0.9, 1.3, 1.3, 0.9,
                "big wins", "clean losses — the crowd thinks they should not happen"),

            GimmickType.Showman => new(1.0, 1.1, 0.9, 1.0, 1.0, 1.0,
                "big nights and a match that gives them the stage", "being hidden"),

            // The classic heel engine: taking what they have not earned. Losing clean is the
            // payoff — good for whoever does it, and charged to them.
            GimmickType.Egotist => new(0.9, 1.0, 1.3, 1.2, 1.2, 0.8,
                "stolen wins", "being beaten clean — which is somebody else's payoff"),
            GimmickType.Sellout => new(0.9, 1.0, 1.3, 1.2, 1.2, 0.8,
                "stolen wins", "being beaten clean — which is somebody else's payoff"),

            // Doc 19 §5.3: once the crowd laughs with you, threat is hard to have. Nobody
            // minds a comedy act losing, and nobody takes their wins as statements.
            GimmickType.Comedy => new(0.85, 0.85, 0.85, 0.6, 0.6, 0.6,
                "the crowd's affection, win or lose", "being asked to be a threat"),

            // "Extremely powerful when it lands, ridiculous when it doesn't." The mystique is
            // what a clean loss costs.
            GimmickType.CultLeader  => new(1.2, 1.0, 1.0, 1.4, 1.4, 1.0, "mystery and dominance", "clean losses, which break the spell"),
            GimmickType.Cryptic     => new(1.2, 1.0, 1.0, 1.4, 1.4, 1.0, "mystery and dominance", "clean losses, which break the spell"),
            GimmickType.DarkPersona => new(1.2, 1.0, 1.0, 1.4, 1.4, 1.0, "mystery and dominance", "clean losses, which break the spell"),

            // The ones who bend rules are doing it in character.
            GimmickType.AntiHero => new(1.0, 1.1, 1.2, 1.0, 1.0, 0.9, "winning their own way", "nothing in particular"),
            GimmickType.Rebel    => new(1.0, 1.1, 1.2, 1.0, 1.0, 0.9, "winning their own way", "nothing in particular"),

            // Risk-takers: the crowd forgives the loss because of what they tried.
            GimmickType.Daredevil => new(1.0, 1.1, 1.0, 0.8, 0.9, 1.0, "taking risks", "nothing, as long as they are spectacular"),

            // The match is the character — see QualityFor.
            GimmickType.Realist        => Neutral with { FedBy = "great matches", HurtBy = "bad ones" },
            GimmickType.Traditionalist => Neutral with { FedBy = "great matches", HurtBy = "bad ones" },
            GimmickType.Gimmickless    => Neutral with { FedBy = "great matches", HurtBy = "bad ones" },

            GimmickType.Loner => Neutral with { FedBy = "standing alone", HurtBy = "being put in a team" },

            _ => Neutral
        };

        /// <summary>
        /// For a character that is the work — the realist, the traditionalist, nobody but
        /// themselves: "ceiling is respect rather than love" (doc 19 §2.1). Their results count
        /// for more in a great match and less in a poor one: 0.85× at one star, 1.15× at five.
        /// </summary>
        public static double QualityFor(GimmickType? type, double stars) =>
            type is GimmickType.Realist or GimmickType.Traditionalist or GimmickType.Gimmickless
                ? 0.775 + 0.075 * Math.Clamp(stars, 0, 5)
                : 1.0;

        /// <summary>A loner on a team is not the character the crowd bought.</summary>
        public const double LonerInATeam = 0.8;

        /// <summary>Beating a heel whose whole act is unearned success, clean, is the payoff — paid to the winner.</summary>
        public const double PayoffBonus = 1.15;

        // ── Traits ───────────────────────────────────────────────────────────
        //
        // The roster's traits are free text, about two hundred of them. The common ones are read
        // here as a handful of effects; the rest are flavour until promos (plan Part 3) give the
        // talkers — Quotable, Smug, Silent — something to do.

        public enum TraitEffect { Feared, Resilient, Respected, Green, Shortcuts, Protected, Spectacle }

        private static readonly Dictionary<string, TraitEffect> Traits = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Feared"] = TraitEffect.Feared, ["Intimidating"] = TraitEffect.Feared, ["Imposing"] = TraitEffect.Feared,
            ["Dangerous"] = TraitEffect.Feared, ["Merciless"] = TraitEffect.Feared, ["Destructive"] = TraitEffect.Feared,
            ["Enormous"] = TraitEffect.Feared, ["Towering"] = TraitEffect.Feared, ["Sheer Mass"] = TraitEffect.Feared,
            ["Dominant"] = TraitEffect.Feared, ["Brutal"] = TraitEffect.Feared, ["Vicious"] = TraitEffect.Feared,
            ["Ruthless"] = TraitEffect.Feared, ["Immovable"] = TraitEffect.Feared, ["Unmovable"] = TraitEffect.Feared,

            ["Resilient"] = TraitEffect.Resilient, ["Determined"] = TraitEffect.Resilient, ["Defiant"] = TraitEffect.Resilient,
            ["Scrappy"] = TraitEffect.Resilient, ["Comeback Story"] = TraitEffect.Resilient, ["Fighter"] = TraitEffect.Resilient,
            ["Tough"] = TraitEffect.Resilient, ["Hungry"] = TraitEffect.Resilient, ["Driven"] = TraitEffect.Resilient,
            ["Relentless"] = TraitEffect.Resilient, ["Stubborn"] = TraitEffect.Resilient, ["Bloody-Minded"] = TraitEffect.Resilient,
            ["Fearless"] = TraitEffect.Resilient, ["Undersized"] = TraitEffect.Resilient, ["Overlooked"] = TraitEffect.Resilient,

            ["Veteran"] = TraitEffect.Respected, ["Legacy"] = TraitEffect.Respected, ["Respected"] = TraitEffect.Respected,
            ["Old School"] = TraitEffect.Respected, ["Ring General"] = TraitEffect.Respected, ["Dependable"] = TraitEffect.Respected,
            ["Reliable"] = TraitEffect.Respected, ["Trusted With Anyone"] = TraitEffect.Respected, ["Weathered"] = TraitEffect.Respected,
            ["Ten Years In"] = TraitEffect.Respected, ["Steady Hand"] = TraitEffect.Respected, ["Legitimate"] = TraitEffect.Respected,

            ["Green"] = TraitEffect.Green, ["Unproven"] = TraitEffect.Green, ["Raw"] = TraitEffect.Green,
            ["Still Learning"] = TraitEffect.Green, ["Untested"] = TraitEffect.Green, ["Young"] = TraitEffect.Green,
            ["Youthful"] = TraitEffect.Green, ["Precocious"] = TraitEffect.Green, ["Improving"] = TraitEffect.Green,

            ["Cheating"] = TraitEffect.Shortcuts, ["Cheater"] = TraitEffect.Shortcuts, ["Takes Shortcuts"] = TraitEffect.Shortcuts,
            ["Dirty Finishes"] = TraitEffect.Shortcuts, ["Weaselly"] = TraitEffect.Shortcuts, ["Manipulative"] = TraitEffect.Shortcuts,
            ["Buys Her Way Out"] = TraitEffect.Shortcuts, ["Calculating"] = TraitEffect.Shortcuts,

            ["Protected"] = TraitEffect.Protected, ["Never Pinned Clean"] = TraitEffect.Protected,

            ["Theatrical"] = TraitEffect.Spectacle, ["Spectacular"] = TraitEffect.Spectacle, ["Showy"] = TraitEffect.Spectacle,
            ["Flashy"] = TraitEffect.Spectacle, ["Glamorous"] = TraitEffect.Spectacle, ["Magnetic"] = TraitEffect.Spectacle,
            ["Celebrity"] = TraitEffect.Spectacle, ["Gravity-Defying"] = TraitEffect.Spectacle, ["Acrobatic"] = TraitEffect.Spectacle,
            ["Spot Machine"] = TraitEffect.Spectacle, ["Stylish"] = TraitEffect.Spectacle, ["Photogenic"] = TraitEffect.Spectacle
        };

        public static IReadOnlyList<TraitEffect> EffectsOf(Wrestler w) =>
            (w.Gimmick?.GimmickTraits ?? [])
                .Select(t => Traits.TryGetValue(t.Trim(), out var e) ? e : (TraitEffect?)null)
                .OfType<TraitEffect>().Distinct().ToList();

        /// <summary>What a trait adds to a result, as a multiplier on top of the type's.</summary>
        private static double TraitFactor(TraitEffect e, ResultKind k) => (e, k) switch
        {
            (TraitEffect.Feared,    ResultKind.DominantWin) => 1.15,
            (TraitEffect.Feared,    ResultKind.CloseLoss or ResultKind.BeatenLoss) => 1.25,
            (TraitEffect.Resilient, ResultKind.CloseLoss)   => 0.75,
            (TraitEffect.Respected, ResultKind.CloseLoss or ResultKind.BeatenLoss or ResultKind.CheapLoss) => 0.85,
            (TraitEffect.Green,     ResultKind.CloseLoss)   => 0.8,
            (TraitEffect.Shortcuts, ResultKind.CheapWin)    => 1.25,
            (TraitEffect.Protected, ResultKind.CloseLoss or ResultKind.BeatenLoss) => 1.3,
            _ => 1.0
        };

        // ── One result, one person ───────────────────────────────────────────

        /// <summary>What the character did to one person's result, and why.</summary>
        public readonly record struct Effect(ResultKind Kind, double Multiplier, string? Why, bool Payoff = false);

        /// <summary>
        /// The multiplier on what a result moves for <paramref name="w"/>, and a sentence when
        /// it is worth saying. <paramref name="beaten"/> is who they beat, for the payoff; empty
        /// for a loss.
        /// </summary>
        public static Effect Of(Wrestler w, ResultKind kind, double stars, bool onATeam,
                                IReadOnlyCollection<Wrestler>? beaten = null)
        {
            var type = w.Gimmick?.Type;
            double m = For(type).Of(kind) * QualityFor(type, stars);
            foreach (var e in EffectsOf(w)) m *= TraitFactor(e, kind);
            if (type == GimmickType.Loner && onATeam) m *= LonerInATeam;

            bool payoff = kind is ResultKind.DominantWin or ResultKind.HardWin &&
                          (beaten ?? []).Any(b => b.Gimmick?.Type is GimmickType.Egotist or GimmickType.Sellout);
            if (payoff) m *= PayoffBonus;

            return new Effect(kind, m, Why(w, kind, m, payoff, onATeam), payoff);
        }

        /// <summary>
        /// The builder's sentence: what this result *will* do, before the bell. Null where the
        /// character does not change the price by a fifth.
        /// </summary>
        public static string? Ahead(Wrestler w, Effect e)
        {
            string name = w.RingName ?? "They";
            string who = $"{name} is {Article(w.Gimmick?.Type)}";
            bool more = e.Multiplier > 1;

            if (e.Payoff && Math.Abs(e.Multiplier - 1) < 0.2)
                return $"{name} beating somebody who never earns anything, clean, is the payoff the crowd has been waiting for.";
            if (Math.Abs(e.Multiplier - 1) < 0.2) return null;

            return (e.Kind, more) switch
            {
                (ResultKind.CloseLoss or ResultKind.BeatenLoss, true) =>
                    $"{who}: a clean loss will cost them about {e.Multiplier:0.0}× what it costs most.",
                (ResultKind.CloseLoss or ResultKind.BeatenLoss or ResultKind.CheapLoss, false) =>
                    $"{who}: this loss will cost them little.",
                (ResultKind.DominantWin, false) =>
                    $"{who}: a one-sided win does less for them than for most.",
                (ResultKind.CheapWin, true) =>
                    $"{who}: a stolen win is the character working — it will count for more than usual.",
                (ResultKind.CheapWin, false) =>
                    $"{who}: a win like this does little for them.",
                (_, true)  => $"{who}: this win will count for more than usual.",
                (_, false) => $"{who}: this win will count for less than usual."
            };
        }

        /// <summary>Said only when the character moved the result by a fifth or more, or paid off a heel.</summary>
        private static string? Why(Wrestler w, ResultKind kind, double m, bool payoff, bool onATeam)
        {
            string name = w.RingName ?? "They";
            var type = w.Gimmick?.Type;

            if (payoff && Math.Abs(m - 1) < 0.2)
                return $"{name} made somebody who never earns anything pay for it — that is the payoff the crowd wanted.";
            if (Math.Abs(m - 1) < 0.2) return null;

            bool more = m > 1;
            string a = Article(type);
            return (type, kind, more) switch
            {
                (GimmickType.Monster, ResultKind.DominantWin, true)  => $"{name} ran through somebody, which is what a monster is for.",
                (GimmickType.Monster, ResultKind.CloseLoss or ResultKind.BeatenLoss, true) => $"{name} is a monster, and monsters are not beaten clean — that cost far more than a loss usually does.",
                (GimmickType.Monster, ResultKind.HardWin or ResultKind.CheapWin, false) => $"{name} had to scrape that one. A monster who struggles is less of one.",
                (GimmickType.Underdog, ResultKind.CloseLoss, false)   => $"{name} came up short, but the underdog taking them to the limit is the character working.",
                (GimmickType.Underdog, ResultKind.HardWin, true)      => $"{name} beat the odds — the win an underdog is for.",
                (GimmickType.Underdog, ResultKind.DominantWin, false) => $"{name} squashed somebody. Nobody believes in an underdog who does that.",
                (_, ResultKind.CheapWin, true) => $"{name} stole it, and for {a} that is the character doing its job.",
                (GimmickType.Comedy, _, false) => $"{name} is a comedy act. The crowd does not read their results as statements either way.",
                (_, ResultKind.CloseLoss or ResultKind.BeatenLoss, true) => $"{name} was beaten clean, and for {a} that costs more than most.",
                (_, ResultKind.CloseLoss, false) => $"{name} lost, but the close loss suits them — it cost little.",
                (GimmickType.Loner, _, false) when onATeam => $"{name} is a loner. Nobody bought them as a team player.",
                (GimmickType.Realist or GimmickType.Traditionalist or GimmickType.Gimmickless, _, _) =>
                    more ? $"{name}'s character is the work, and the work was good: the result counted for more."
                         : $"{name}'s character is the work, and the work was poor: the result counted for less.",
                (_, _, true)  => $"For {a}, that result counted for more than usual.",
                (_, _, false) => $"For {a}, that result counted for less than usual."
            };
        }

        public static string Article(GimmickType? type) => type switch
        {
            GimmickType.Monster        => "a monster",
            GimmickType.Underdog       => "an underdog",
            GimmickType.AntiHero       => "an anti-hero",
            GimmickType.Rebel          => "a rebel",
            GimmickType.CultLeader     => "a cult leader",
            GimmickType.Showman        => "a showman",
            GimmickType.Realist        => "a realist",
            GimmickType.Comedy         => "a comedy act",
            GimmickType.Traditionalist => "a traditionalist",
            GimmickType.Loner          => "a loner",
            GimmickType.Superstar      => "a superstar",
            GimmickType.Egotist        => "an egotist",
            GimmickType.DarkPersona    => "a dark character",
            GimmickType.Sellout        => "a sellout",
            GimmickType.Cryptic        => "a cryptic character",
            GimmickType.Daredevil      => "a daredevil",
            GimmickType.Gimmickless    => "a wrestler with no gimmick",
            _                          => "this character"
        };

        /// <summary>
        /// Applies a multiplier to the result-driven part of a change: the gain of a win, the
        /// cost of a loss. A loser's showcase momentum from a great match is not a cost and is
        /// left alone, and a sign is never flipped.
        /// </summary>
        public static StatusChange Apply(StatusChange change, Effect effect)
        {
            if (Math.Abs(effect.Multiplier - 1) < 1e-9) return change;
            bool win = IsWin(effect.Kind);
            double Scale(double d) => win ? (d > 0 ? d * effect.Multiplier : d) : (d < 0 ? d * effect.Multiplier : d);
            return change with
            {
                OvernessDelta = Scale(change.OvernessDelta),
                MomentumDelta = Scale(change.MomentumDelta),
                Reason        = effect.Why is { } why ? $"{change.Reason} {why}" : change.Reason
            };
        }

        // ── The stage ────────────────────────────────────────────────────────

        /// <summary>
        /// How much more or less of this character the crowd feels on this kind of show. Showmen
        /// and superstars are made for the big night; a comedy act plays best to a house-show
        /// crowd with the kids in it; a realist's matches carry the smaller rooms. Weekly
        /// television is everybody's ordinary stage, 1.0.
        /// </summary>
        public static double Stage(Wrestler w, ShowType type)
        {
            double s = (w.Gimmick?.Type, type) switch
            {
                (GimmickType.Showman,   ShowType.PremiumEvent) => 1.08,
                (GimmickType.Showman,   ShowType.HouseShow)    => 0.97,
                (GimmickType.Superstar, ShowType.PremiumEvent) => 1.06,
                (GimmickType.Superstar, ShowType.HouseShow)    => 0.96,
                (GimmickType.Daredevil, ShowType.PremiumEvent) => 1.04,
                (GimmickType.Comedy,    ShowType.PremiumEvent) => 0.92,
                (GimmickType.Comedy,    ShowType.HouseShow)    => 1.06,
                (GimmickType.Realist or GimmickType.Traditionalist or GimmickType.Gimmickless, ShowType.PremiumEvent) => 0.96,
                (GimmickType.Realist or GimmickType.Traditionalist or GimmickType.Gimmickless, ShowType.HouseShow)    => 1.03,
                _ => 1.0
            };
            if (type == ShowType.PremiumEvent && EffectsOf(w).Contains(TraitEffect.Spectacle)) s *= 1.03;
            return s;
        }

        /// <summary>
        /// How fast the character wears, on top of durability (<see cref="CharacterFreshness"/>).
        /// A joke is got in five seconds and a mystery is spoiled by being seen too often (doc 19
        /// §2.1, §2.3); the character that is the performer, louder, lasts longest.
        /// </summary>
        public static double WearRate(GimmickType? type) => type switch
        {
            GimmickType.Comedy or GimmickType.CultLeader or GimmickType.Cryptic or GimmickType.DarkPersona => 1.2,
            GimmickType.Superstar or GimmickType.Realist or GimmickType.Traditionalist or GimmickType.Gimmickless => 0.85,
            _ => 1.0
        };

        /// <summary>For the profile: what this character is fed by and hurt by, in one sentence.</summary>
        public static string Summary(Wrestler w)
        {
            var type = w.Gimmick?.Type;
            var a = For(type);
            string s = $"{Capitalise(Article(type))}: fed by {a.FedBy}; hurt by {a.HurtBy}.";
            var effects = EffectsOf(w);
            if (effects.Count > 0)
                s += " " + string.Join(" ", effects.Select(TraitLine));
            return s;
        }

        private static string TraitLine(TraitEffect e) => e switch
        {
            TraitEffect.Feared    => "Feared: a dominant win says more, and a clean loss costs more.",
            TraitEffect.Resilient => "Resilient: a close loss costs them little.",
            TraitEffect.Respected => "Respected: losing costs them less — they have nothing left to prove.",
            TraitEffect.Green     => "Green: a close loss to somebody better is part of learning.",
            TraitEffect.Shortcuts => "Takes shortcuts: a stolen win is the character working.",
            TraitEffect.Protected => "Protected: a clean loss is a big event.",
            TraitEffect.Spectacle => "A spectacle: bigger on the big nights.",
            _ => ""
        };

        private static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }
}
