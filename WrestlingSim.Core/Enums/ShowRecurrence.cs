namespace WrestlingSim.Enums
{
    /// <summary>How often a show definition puts a date on the calendar.</summary>
    public enum RecurrenceKind
    {
        /// <summary>Every week on a fixed weekday. Weekly television.</summary>
        Weekly,

        /// <summary>Once a month, on the Nth occurrence of a weekday. Premium events.</summary>
        Monthly
    }

    /// <summary>Which occurrence of a weekday within a month a monthly show lands on.</summary>
    public enum WeekOrdinal
    {
        First,
        Second,
        Third,
        Fourth,
        Last
    }
}
