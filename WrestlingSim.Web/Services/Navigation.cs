namespace WrestlingSim.Web.Services;

/// <summary>The six things a career is made of, and the top-level navigation.</summary>
public enum NavGroup { Home, Booking, Talent, Stories, World, Business }

/// <summary>One tab in the row under the top navigation.</summary>
public sealed record NavTab(string Label, Func<GameState, bool> IsActive, Action<GameState> Open, int? Count = null);

/// <summary>
/// Where every screen lives.
///
/// The top bar used to list seven screens — Home, Calendar, Shows, Brands, Roster, Titles,
/// Feuds — with Contracts and Rivals reachable only through buttons inside other screens, and
/// the money with no screen at all. A booker did not know where anything was because the
/// navigation was a list of pages rather than a map of the job. This groups them by what the
/// booker is doing: running the calendar, managing people, telling stories, watching the
/// industry, minding the money. Each group's own pages are the tab row under it.
///
/// One table, read by the frame, so a screen added to the app is placed in a group here and
/// nowhere else.
/// </summary>
public static class Navigation
{
    public static readonly (NavGroup Group, string Label, string Glyph)[] Groups =
    [
        (NavGroup.Home,     "Home",     "home"),
        (NavGroup.Booking,  "Booking",  "calendar"),
        (NavGroup.Talent,   "Talent",   "roster"),
        (NavGroup.Stories,  "Stories",  "fire"),
        (NavGroup.World,    "World",    "tv"),
        (NavGroup.Business, "Business", "card"),
    ];

    /// <summary>The group a career screen belongs to.</summary>
    public static NavGroup GroupOf(Screen screen) => screen switch
    {
        Screen.Calendar or Screen.Shows or Screen.Brands or Screen.BookShow => NavGroup.Booking,
        Screen.Roster or Screen.Contracts or Screen.Profile                => NavGroup.Talent,
        Screen.Stories or Screen.Feuds or Screen.Titles                    => NavGroup.Stories,
        Screen.Rivals or Screen.Rival or Screen.News                       => NavGroup.World,
        Screen.Business or Screen.Television                               => NavGroup.Business,
        _                                                                  => NavGroup.Home
    };

    /// <summary>Where a group opens.</summary>
    public static void Open(GameState state, NavGroup group)
    {
        switch (group)
        {
            case NavGroup.Booking:  state.Go(Screen.Calendar); break;
            case NavGroup.Talent:   state.GoTalent(TalentView.Yours); break;
            case NavGroup.Stories:  state.Go(Screen.Stories); break;
            case NavGroup.World:    state.Go(Screen.Rivals); break;
            case NavGroup.Business: state.Go(Screen.Business); break;
            default:                state.Go(Screen.Dashboard); break;
        }
    }

    private static NavTab Tab(string label, Screen screen, int? count = null) =>
        new(label, s => s.Current == screen, s => s.Go(screen), count);

    private static NavTab Talent(string label, TalentView view, int? count = null) =>
        new(label, s => s.Current == Screen.Roster && s.TalentView == view, s => s.GoTalent(view), count);

    /// <summary>The tab row under a group, or empty for a group that is one page.</summary>
    public static IReadOnlyList<NavTab> TabsOf(NavGroup group, GameState state)
    {
        var career = state.Career;
        if (career is null) return [];

        return group switch
        {
            NavGroup.Booking =>
            [
                Tab("Calendar", Screen.Calendar),
                Tab("Your shows", Screen.Shows, career.ShowDefinitions.Count(d => d.Active)),
                Tab("Brands", Screen.Brands),
            ],
            NavGroup.Talent =>
            [
                Talent("Your roster", TalentView.Yours, career.Contracts.Count),
                Talent("The market", TalentView.Market),
                Talent("Everyone", TalentView.Everyone, career.Roster.Count),
                Tab("Contracts and offers", Screen.Contracts),
            ],
            NavGroup.Stories =>
            [
                Tab("Overview", Screen.Stories),
                Tab("Feuds", Screen.Feuds, state.ActiveFeudCount),
                Tab("Titles", Screen.Titles, career.Titles.Active.Count),
            ],
            NavGroup.World =>
            [
                Tab("Promotions", Screen.Rivals, career.Rivals.Count),
                Tab("News", Screen.News),
            ],
            NavGroup.Business =>
            [
                Tab("Money", Screen.Business),
                Tab("Television", Screen.Television),
            ],
            _ => []
        };
    }

    /// <summary>Exhibition's own pages: a sandbox, so a short flat list.</summary>
    public static readonly (Screen Screen, string Label, string Glyph)[] Exhibition =
    [
        (Screen.Exhibition, "Start",        "home"),
        (Screen.Match,      "Match",        "star"),
        (Screen.Segment,    "Segment",      "mic"),
        (Screen.Show,       "Show",         "card"),
        (Screen.Rumble,     "Battle royal", "roster"),
        (Screen.Roster,     "Wrestlers",    "roster"),
        (Screen.Feuds,      "Feuds",        "fire"),
    ];
}
