namespace WrestlingSim.Models.Person
{
    /// <summary>
    /// The six things a wrestler can get better or worse at, each learned differently (decided
    /// 15 Nov 2026, living-world plan Part 2).
    /// </summary>
    public enum SkillGroup
    {
        /// <summary>Strength and stamina. The gym: grows from training, not from matches.</summary>
        Conditioning,
        /// <summary>Speed, agility, high-flying. Natural athleticism, capped by build, first to go.</summary>
        Athletic,
        /// <summary>Striking, brawling, power.</summary>
        Combat,
        /// <summary>Technical and grappling. Slow to learn and the best to age with.</summary>
        Mat,
        /// <summary>Psychology, selling, ring IQ. Reps and who you work with; the longest-growing.</summary>
        Craft,
        /// <summary>Charisma. A little; the gimmick does more.</summary>
        Presence
    }

    /// <summary>
    /// Where somebody is going, and how quickly they get there. Every array is indexed by
    /// <see cref="SkillGroup"/>.
    ///
    /// The attributes themselves stay where they always were (<see cref="Wrestler.RingSkills"/>,
    /// <see cref="Wrestler.Physical"/>, <see cref="Wrestler.Mental"/>, <see cref="Wrestler.Charisma"/>)
    /// and are moved each month by <see cref="Engine.Development"/>, so the number on a screen is
    /// the number the engine reads.
    /// </summary>
    public class Development
    {
        public const int Groups = 6;

        /// <summary>
        /// What each group would be at its best age, 0–100. Hidden from the player except as
        /// words. Whether they reach it depends on being used.
        /// </summary>
        public double[] Ceiling { get; set; } = new double[Groups];

        /// <summary>How quickly they learn each group. 1 is typical; the real roster spans about 0.5–1.6.</summary>
        public double[] Speed { get; set; } = new double[Groups];

        /// <summary>
        /// What this month's work has exercised, in rough matches' worth per group. Cleared at
        /// each monthly step.
        /// </summary>
        public double[] Worked { get; set; } = new double[Groups];

        /// <summary>Matches worked this month, and the summed level of who they were against.</summary>
        public int Matches { get; set; }
        public double Opposition { get; set; }

        /// <summary>Hurt at any point this month.</summary>
        public bool Hurt { get; set; }

        /// <summary>
        /// Fractions of a point owed to whole-number attributes (strength, psychology and the
        /// rest are stored as integers), by attribute name, so a month's +0.3 is not lost.
        /// </summary>
        public Dictionary<string, double> Carry { get; set; } = new();

        /// <summary>
        /// Each group's level at the start of each month, newest last, whole numbers. What
        /// "since you signed them" and the news are read from.
        /// </summary>
        public List<DevelopmentMark> History { get; set; } = new();
    }

    public class DevelopmentMark
    {
        public DateOnly On { get; set; }
        public int[] Levels { get; set; } = new int[Development.Groups];
    }
}
