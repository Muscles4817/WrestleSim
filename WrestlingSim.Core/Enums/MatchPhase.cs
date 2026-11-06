namespace WrestlingSim.Enums
{
    /// <summary>One stage of a match, before any beat has been chosen to fill it.</summary>
    public enum MatchPhase
    {
        /// <summary>The bell. Establishes the tone and nothing else.</summary>
        Opening,

        /// <summary>The protagonist looks good. Doc 18 §2.3's second stage.</summary>
        Shine,

        /// <summary>The turn. One moment, and the match changes hands.</summary>
        Cutoff,

        /// <summary>The control section. Where a match is actually built.</summary>
        Heat,

        /// <summary>A tease inside the heat. Bought back, not given.</summary>
        HopeSpot,

        /// <summary>The valley. A long match needs one or nobody can work it.</summary>
        Rest,

        /// <summary>Character work in the middle of a beating.</summary>
        Escalation,

        /// <summary>The payoff. What every hope spot was borrowing against.</summary>
        Comeback,

        /// <summary>Near falls. The finishing stretch.</summary>
        Stretch,

        /// <summary>Somebody outside the match gets involved.</summary>
        Outside,

        /// <summary>Two of them work the third. Doc 18 §2.5's best story, first half.</summary>
        Alliance,

        /// <summary>And the moment it breaks, which §2.5 calls the peak.</summary>
        Betrayal,

        /// <summary>The third man is put somewhere, so the next stretch can be two-handed.</summary>
        Disposal,

        /// <summary>And comes back, which is what makes the disposal a payoff and not a reset.</summary>
        Return,

        /// <summary>A fresh partner rotates in. The heat gets deeper rather than longer.</summary>
        Rotation,

        /// <summary>The tandem offence a hot tag buys. The payoff's payoff.</summary>
        Tandem,

        /// <summary>Everybody in, nobody legal, and the referee has lost it.</summary>
        AllFour,

        /// <summary>The partner breaks the count. A tag match's answer to the pin break.</summary>
        Save,

        /// <summary>The end.</summary>
        Finish
    }
}
