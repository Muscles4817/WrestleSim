namespace WrestlingSim.Enums
{
    /// <summary>
    /// How good a match, segment or show was, in bands.
    ///
    /// Every rating in the app was gold. A two-star match and a five-star one were the same
    /// colour, differing only in how many glyphs they drew, and the 0–100 score beside them
    /// was gold at 42 and gold at 95 — so the one screen whose entire job is telling a booker
    /// whether the show was any good said it in a colour that means "good" whatever happened.
    /// </summary>
    public enum Grade
    {
        /// <summary>Below two stars. This one hurt.</summary>
        Poor,

        /// <summary>It happened. Nobody will remember it, and nobody is angry.</summary>
        Fine,

        /// <summary>A good match. What a card should mostly be made of.</summary>
        Good,

        /// <summary>Very good. The one people talk about on the way home.</summary>
        Great,

        /// <summary>Four and a half and up. The reason anybody books a main event.</summary>
        Classic
    }

    public static class Grades
    {
        /// <summary>
        /// The band a star rating falls in.
        ///
        /// Star thresholds rather than score ones, and the 0–100 overloads convert rather than
        /// carrying their own cut-offs, because <c>MatchEngine</c> defines the star rating as
        /// the score over twenty. Two sets of thresholds on two views of one number is how a
        /// score reading "Great" ends up beside stars painted for a good one.
        /// </summary>
        public static Grade OfStars(double stars) => stars switch
        {
            >= 4.5  => Grade.Classic,
            >= 3.75 => Grade.Great,
            >= 3.0  => Grade.Good,
            >= 2.0  => Grade.Fine,
            _       => Grade.Poor
        };

        /// <summary>The same bands, for a 0–100 score. See <see cref="OfStars"/>.</summary>
        public static Grade OfScore(double score) => OfStars(score / 20.0);
    }

    public static class GradeExtensions
    {
        public static string Label(this Grade g) => g switch
        {
            Grade.Classic => "Classic",
            Grade.Great   => "Very good",
            Grade.Good    => "Good",
            Grade.Fine    => "Forgettable",
            _             => "Poor"
        };

        /// <summary>
        /// The class a rating is painted with.
        ///
        /// Unlike the overness ramp this does not simply fade out at the bottom: a wrestler
        /// with a low reading is a jobber doing their job, and a match with one is a mistake
        /// the booker made. The bottom band is the warning colour for that reason.
        /// </summary>
        public static string Tone(this Grade g) => g switch
        {
            Grade.Classic => "grade--classic",
            Grade.Great   => "grade--great",
            Grade.Good    => "grade--good",
            Grade.Fine    => "grade--fine",
            _             => "grade--poor"
        };
    }
}
