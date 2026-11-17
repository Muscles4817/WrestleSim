using WrestlingSim.Enums;
using WrestlingSim.Models;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// How worn a character is, 0–1 — <see cref="Gimmick.Freshness"/>, which the roster has always
    /// carried and nothing read until docs/gimmick-plan.md Part 4.
    ///
    /// **Not the same thing as act sameness** (<see cref="AudienceModel"/>). Sameness is weeks:
    /// this wrestler's recent booking has been samey, and something new takes it straight back
    /// off. Freshness is months: the *character* has said what it has to say (doc 17 §3.3), and
    /// a good week does not undo that. Booking with something new in it slows the wear — a
    /// character in a story that moves has new things to say — but only rest restores it here.
    /// Turns, repackages and a new context restore it too, when they exist (plan Parts 5–6).
    ///
    /// **It costs nothing until it is worn.** Above <see cref="Worn"/> the character works as
    /// shipped; below it, the gimmick's appeal falls, to <see cref="FloorFactor"/> at zero. Every
    /// shipped and generated character starts at 0.5 or more, so day one of a save draws exactly
    /// what it drew before this existed, and what moves is what happens after.
    ///
    /// Every rate here is a design estimate against doc 17 §4.1 ("a new character: fresh for
    /// 6–18 months") and the roster's own durability bands. Nobody has measured these.
    /// </summary>
    public static class CharacterFreshness
    {
        /// <summary>Below this the character is wearing thin and the crowd's interest in it falls.</summary>
        public const double Worn = 0.45;

        /// <summary>What is left of a gimmick's appeal when the character is completely spent.</summary>
        public const double FloorFactor = 0.6;

        /// <summary>Off screen this long before rest starts to count: a missed week is not an absence.</summary>
        public const int GraceDays = 28;

        /// <summary>Away from the screen, the way back to <see cref="RestCeiling"/> halves in four months.</summary>
        public const double RestHalfLifeDays = 120;

        /// <summary>
        /// Where rest stops. A return is not a debut: the audience remembers the character, and
        /// it takes something new — a turn, a repackage — to make it new again.
        /// </summary>
        public const double RestCeiling = 0.9;

        /// <summary>
        /// Wear from one appearance on a television or premium card with nothing new in it, by
        /// how long the roster says the gimmick lasts. Short-term reaches <see cref="Worn"/> from
        /// a typical start in about four months of weekly television, medium in about ten, long
        /// in about twenty.
        /// </summary>
        public static double BaseWear(GimmickDurability? durability) => durability switch
        {
            GimmickDurability.ShortTerm => 0.025,
            GimmickDurability.LongTerm  => 0.005,
            _                           => 0.010
        };

        /// <summary>
        /// How fast this wrestler's character wears per appearance, before novelty. Charisma keeps
        /// a character alive (a 5 wears at 0.7×, a 0 at 1.3×); a house show is seen by few.
        /// </summary>
        public static double WearPerAppearance(Wrestler w, ShowType type) =>
            BaseWear(w.Gimmick?.Durability)
            * CharacterTypes.WearRate(w.Gimmick?.Type)
            * (1.3 - 0.12 * Math.Clamp(w.Charisma, 0, 5))
            * (type == ShowType.HouseShow ? 0.3 : 1.0);

        /// <summary>
        /// One appearance. <paramref name="novelty"/> is the same 0–1 the audience model reads
        /// for the night: an appearance that moved a story wears the character less.
        /// </summary>
        public static void Wear(Wrestler w, ShowType type, double novelty)
        {
            if (w.Gimmick is not { } g) return;
            double wear = WearPerAppearance(w, type) * (1 - 0.6 * Math.Clamp(novelty, 0, 1));
            g.Freshness = Math.Clamp(g.Freshness - wear, 0, 1);
        }

        /// <summary>
        /// <paramref name="days"/> have passed, ending <paramref name="today"/>. Anybody off the
        /// screen for longer than <see cref="GraceDays"/> recovers for the part of those days
        /// that lies past the grace — so a step of any length gives the same answer as a day at a
        /// time, which the clock needs because it jumps when a show is run early.
        /// </summary>
        public static void Rest(IEnumerable<Wrestler> people, DateOnly today, int days)
        {
            if (days <= 0) return;
            foreach (var w in people)
            {
                if (w.Gimmick is not { } g || g.Freshness >= RestCeiling) continue;

                // Nobody has seen them in this world yet: a debut is handled by sameness, and
                // the character is whatever it was shipped as.
                if (w.LastAppearance is not { } seen) continue;

                int away = today.DayNumber - seen.DayNumber;
                int resting = Math.Min(days, away - GraceDays);
                if (resting <= 0) continue;

                double gap = RestCeiling - g.Freshness;
                g.Freshness = RestCeiling - gap * Math.Pow(0.5, resting / RestHalfLifeDays);
            }
        }

        /// <summary>How much of the gimmick's appeal the crowd still feels: 1.0 until it is worn.</summary>
        public static double Factor(Wrestler w) => Factor(w.Gimmick?.Freshness ?? 1.0);

        public static double Factor(double freshness) =>
            freshness >= Worn ? 1.0 : 1.0 - (1.0 - FloorFactor) * (Worn - Math.Max(0, freshness)) / Worn;

        /// <summary>The character's state in the booker's words.</summary>
        public static string Words(double freshness) => freshness switch
        {
            >= 0.85 => "Fresh",
            >= 0.65 => "Established",
            >= Worn => "Getting familiar",
            >= 0.25 => "Wearing thin",
            _       => "Said everything it has to say"
        };

        /// <summary>A sentence for the profile: where the character is and what helps.</summary>
        public static string Advice(double freshness) => freshness switch
        {
            >= 0.85 => "The audience has not had enough of this character yet.",
            >= 0.65 => "The character works. Stories that move keep it that way longer.",
            >= Worn => "The audience knows exactly what this character does. Samey booking will wear it out; time away restores it.",
            _       => "The crowd's interest in the character is falling. Time off television is the only cure the booker has today; turns and repackages are coming."
        };

        /// <summary>For the show report, when tonight pushed somebody over the line.</summary>
        public static string WornNote(Wrestler w) =>
            $"{w.RingName}: the character is wearing thin — the crowd has seen everything it does. Time away would restore it.";
    }
}
