using WrestlingSim.Enums;
using WrestlingSim.Models;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// What a roster picker should already be filtered to when it opens.
    ///
    /// Booking a match means picking several names from one list, and the list is the whole
    /// roster every time. The overwhelmingly common case is that everybody in a match comes
    /// from one division, so making the booker narrow the list by hand once per slot is
    /// asking the same question repeatedly and taking the same answer.
    /// </summary>
    public static class RosterFilters
    {
        /// <summary>
        /// The division a picker should open on, given who is already booked, or null to
        /// open on the whole roster.
        ///
        /// Two cases return null, and the second is the one worth stating:
        ///
        /// * **Nobody is booked yet.** There is nothing to match, so the first pick is made
        ///   from everybody.
        /// * **The people already booked disagree.** An intergender match is a legitimate
        ///   booking, and once a booker has made one the filter must not quietly hide half of
        ///   what they have already chosen from them. Defaulting to the first name's division
        ///   there would answer a question the booker has visibly already answered the other
        ///   way.
        ///
        /// This is a *default*, never a gate. The picker's own division chips still override
        /// it, which is what makes booking an intergender match one tap rather than
        /// impossible.
        /// </summary>
        public static Division? DivisionFor(IEnumerable<Wrestler> booked)
        {
            Division? seen = null;

            foreach (var w in booked)
            {
                if (seen is null) seen = w.Division;
                else if (seen != w.Division) return null;
            }

            return seen;
        }
    }
}
