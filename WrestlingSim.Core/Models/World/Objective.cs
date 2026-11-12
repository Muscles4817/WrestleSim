namespace WrestlingSim.Models.World
{
    /// <summary>
    /// What a promotion wants. Weights, each at least zero; only the balance between them
    /// matters. docs/rival-ai-plan.md §5.
    ///
    /// This is data, not behaviour. Nothing in the planner knows what kind of owner it is
    /// planning for — it is handed one of these and picks the moves that serve it. A promotion
    /// can be given its own in place of its founder motive's default
    /// (<see cref="RivalPromotion.Objective"/>), which is how a custom database will say what a
    /// named promotion is for.
    /// </summary>
    public sealed record Objective(
        // Profit over the period it plans for, and the value of the contracts it holds.
        // Not cash in the bank: scored as a balance, cutting the roster looks like winning.
        double Wealth,
        // What its audience would make the next television deal worth. Counts for more as
        // the renewal gets closer. Nothing below television.
        double Franchise,
        // How strongly its cards pull a crowd.
        double Stars,
        // How good its shows are, on the same 0–100 rating as the player's.
        double Quality,
        // Its name for good shows, which lifts every later crowd.
        double Reputation,
        // The owner's chosen wrestlers at the top of the card.
        double Favour,
        // Continuity: not losing the people its audience is used to. Charged for letting
        // somebody go, by how long they have been there — never for filling a hole.
        double Stability);

    /// <summary>
    /// How a promotion sees the future and how it carries risk. Two different things, kept as
    /// separate fields because a competent owner who gambles and an incompetent one who
    /// misreads the numbers are different promotions.
    /// </summary>
    public sealed record Disposition(
        // ── Competence: how well it forecasts ──
        // How far ahead it looks.
        int HorizonDays,
        // Multiplies the gate and merchandise it expects. 1.0 is sober; 1.25 believes its
        // cards sell a quarter more than they do.
        double Optimism,
        // ── Temperament: how it carries risk, knowingly ──
        // How much money-time it wants in hand before it worries.
        int RunwayFloorDays,
        // How hard it reacts once below that.
        double RiskAversion);

    /// <summary>
    /// One decision a promotion made, with what it believed and what it turned down — the
    /// decision debugger. The player sees the reason; the rest is for whoever is asking why the
    /// world did what it did.
    /// </summary>
    public sealed class RivalDecision
    {
        public required DateOnly Date { get; init; }

        /// <summary>signed, released, bid for, re-signed, let go, added, dropped.</summary>
        public required string Move { get; init; }
        public required string WrestlerId { get; init; }
        public decimal Annual { get; init; }

        /// <summary>The largest reason, in words.</summary>
        public required string Reason { get; init; }

        /// <summary>How much better than doing nothing it judged the move.</summary>
        public double Score { get; init; }

        /// <summary>What each thing it cares about contributed, by name.</summary>
        public Dictionary<string, double> Terms { get; init; } = new();

        /// <summary>What it expected of itself at the time: house, audience, money-time left.</summary>
        public string Believed { get; init; } = "";

        /// <summary>The best alternatives it scored and did not take.</summary>
        public List<string> Rejected { get; init; } = new();
    }
}
