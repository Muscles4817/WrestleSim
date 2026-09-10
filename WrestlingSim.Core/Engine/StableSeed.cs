using System.Globalization;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// A seed that is the same on every run.
    ///
    /// The corpus tests — the 34,200-pairing investment distribution, the singles matrix, the
    /// tag matrix — derive a per-cell seed from the cell's identity so that every cell gets a
    /// different match and the sample covers the engine rather than one lucky draw. They all
    /// did that with <c>HashCode.Combine</c>, and <c>System.HashCode</c> is seeded from a
    /// random value once per process. It is documented as not stable across runs, and it is
    /// not: three consecutive runs of the identical commit gave p05 0.7459, 0.7457 and 0.7455.
    ///
    /// So every one of those tests was measuring a *fresh random sample* each run. The tests
    /// themselves were fine — arguably better than fine, since passing under a new sample
    /// every run for their whole life is a stronger guarantee than passing on one pinned
    /// corpus. What was not fine was quoting their output to four decimal places in a build
    /// log, a commit message and a PR description as though a reader could reproduce it. They
    /// could not, and neither could I.
    ///
    /// FNV-1a over the invariant string form of each part: stable across runs, processes and
    /// machines, and good enough for spreading seeds.
    ///
    /// It lives in Core rather than in the test project because the generated match sheet
    /// needs the same property for the same reason. A brief is seeded from its own contents
    /// so that saving a plan and reloading it regenerates the same match; with a per-process
    /// hash the beats would change every time the app restarted.
    /// </summary>
    public static class StableSeed
    {
        public static int From(params object?[] parts)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (var part in parts)
                {
                    foreach (char c in Text(part))
                    {
                        h ^= c;
                        h *= 16777619;
                    }
                    h ^= 0xFF;          // separator, so ("ab","c") and ("a","bc") differ
                    h *= 16777619;
                }
                return (int)(h & 0x7FFFFFFF);
            }
        }

        private static string Text(object? part) => part switch
        {
            null => "\0",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => part.ToString() ?? "\0"
        };
    }
}
