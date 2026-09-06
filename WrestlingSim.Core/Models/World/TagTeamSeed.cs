namespace WrestlingSim.Models.World
{
    /// <summary>
    /// A standing team the promotion already has on day one, as it is written in
    /// JSON/TagTeams.json.
    ///
    /// Teams are shipped data for the same reason the roster is: the alternative is a
    /// promotion where every tag match is two singles wrestlers who have never met, which
    /// is a legitimate booking decision but a terrible starting position. A division needs
    /// acts the audience already reads as acts.
    ///
    /// Everything here is stored relative to the day the career begins rather than as an
    /// absolute date, because the player picks that date. "Four years together, teamed
    /// last week" has to mean the same thing whether the save opens in 1997 or 2031.
    /// </summary>
    public class TagTeamSeed
    {
        public string Name { get; set; } = "";

        /// <summary>
        /// Members by <see cref="Wrestler.RealName"/> — the one field a gimmick change does
        /// not move, and the field <see cref="Wrestler.Id"/> is derived from.
        /// </summary>
        public List<string> Members { get; set; } = new();

        /// <summary>How long the team has existed when the career opens.</summary>
        public int FormedDaysAgo { get; set; }

        /// <summary>
        /// How recently they last worked together. Kept inside
        /// <see cref="TagTeam.GraceDays"/> for a team that is still going, so a save does
        /// not begin by charging an established act for time off it never had.
        /// </summary>
        public int LastTeamedDaysAgo { get; set; }

        public int MatchesTogether { get; set; }

        /// <summary>
        /// Where the pair actually is, 0–1. Stored rather than recomputed from
        /// <see cref="MatchesTogether"/> so a seeded team can say something the match count
        /// alone cannot — two hundred nights together and still only well drilled, or
        /// twenty and already moving as one.
        /// </summary>
        public double Chemistry { get; set; }

        /// <summary>
        /// Resolves this seed against a live roster. Returns null when a member is not on
        /// it — the same rule <see cref="Persistence.SaveSerializer"/> applies on load: a
        /// team one of whose members is missing is not a team.
        /// </summary>
        public TagTeam? Bind(IReadOnlyDictionary<string, Wrestler> byRealName, DateOnly today)
        {
            var members = new List<Wrestler>();
            foreach (var name in Members)
            {
                if (!byRealName.TryGetValue(name, out var w)) return null;
                members.Add(w);
            }

            if (members.Count < 2) return null;

            return new TagTeam
            {
                Name            = Name,
                Members         = members,
                Formed          = today.AddDays(-Math.Max(0, FormedDaysAgo)),
                MatchesTogether = Math.Max(0, MatchesTogether),
                LastTeamed      = today.AddDays(-Math.Max(0, LastTeamedDaysAgo)),
                Chemistry       = Math.Clamp(Chemistry, 0, 1)
            };
        }

        /// <summary>
        /// The teams a new career starts with, bound to the roster instances it will
        /// actually book. Mirrors <see cref="TitleRegistry.SeedDefaults"/>: shipped data,
        /// applied once at the moment the career is created, and thereafter the player's
        /// to break up.
        ///
        /// Deliberately not applied on load. A team the player disbanded must stay
        /// disbanded, so a save's team list is authoritative even when it is empty.
        /// </summary>
        public static List<TagTeam> SeedDefaults(IEnumerable<Wrestler> roster, DateOnly today)
        {
            var byRealName = new Dictionary<string, Wrestler>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in roster) byRealName.TryAdd(w.RealName, w);

            return DataLoaders.LoadEmbeddedTagTeams()
                .Select(seed => seed.Bind(byRealName, today))
                .OfType<TagTeam>()
                .ToList();
        }
    }
}
