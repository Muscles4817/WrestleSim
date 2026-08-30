namespace WrestlingSim.UI
{
    /// <summary>Shared console rendering and input helpers for the booking flows.</summary>
    public static class ConsoleUi
    {
        public const int HeaderWidth = 56;

        // ── Rendering ────────────────────────────────────────────────────────

        public static void DrawHeader(string title)
        {
            int pad = (HeaderWidth - title.Length) / 2;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ╔" + new string('═', HeaderWidth) + "╗");
            Console.WriteLine("  ║" + new string(' ', pad) + title + new string(' ', HeaderWidth - pad - title.Length) + "║");
            Console.WriteLine("  ╚" + new string('═', HeaderWidth) + "╝");
            Console.ResetColor();
        }

        public static void Rule(string label, int width = 40)
        {
            Console.WriteLine($"\n  ── {label} " + new string('─', Math.Max(1, width - label.Length)));
        }

        public static void Write(string text, ConsoleColor colour)
        {
            Console.ForegroundColor = colour;
            Console.Write(text);
            Console.ResetColor();
        }

        public static void WriteLine(string text, ConsoleColor colour)
        {
            Console.ForegroundColor = colour;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        public static void Warn(string message)
        {
            WriteLine($"\n  {message}", ConsoleColor.Red);
            AnyKey();
        }

        public static void Pause(string message = "Press any key to continue...")
        {
            Console.WriteLine($"\n  {message}");
            AnyKey();
        }

        /// <summary>
        /// Console.Clear throws when there is no attached console (piped input, CI).
        /// Screen clearing is cosmetic, so losing it is better than crashing.
        /// </summary>
        public static void Clear()
        {
            try { Console.Clear(); }
            catch (IOException) { /* no console buffer — carry on */ }
        }

        /// <summary>
        /// Console.ReadKey throws on redirected input, so fall back to a line read.
        /// </summary>
        public static void AnyKey()
        {
            if (Console.IsInputRedirected) Console.ReadLine();
            else Console.ReadKey(true);
        }

        public static string Fit(string s, int len) =>
            s.Length > len ? s[..(len - 1)] + "…" : s.PadRight(len);

        public static string Truncate(string s, int max) =>
            s.Length > max ? s[..(max - 1)] + "…" : s;

        public static string Bar(double value, double max, int width = 20)
        {
            int filled = Math.Clamp((int)Math.Round(value / max * width), 0, width);
            return new string('█', filled) + new string('░', width - filled);
        }

        // ── Input ────────────────────────────────────────────────────────────

        public static string Ask(string prompt)
        {
            Console.Write($"  {prompt}: ");
            return (Console.ReadLine() ?? "").Trim();
        }

        public static bool YesNo(string prompt, bool defaultYes = false)
        {
            Console.Write($"  {prompt} ({(defaultYes ? "Y/n" : "y/N")}): ");
            string input = (Console.ReadLine() ?? "").Trim();
            if (input.Length == 0) return defaultYes;
            return input.StartsWith("y", StringComparison.OrdinalIgnoreCase);
        }

        public static int AskInt(string prompt, int fallback)
        {
            Console.Write($"  {prompt}: ");
            return int.TryParse(Console.ReadLine(), out int n) ? n : fallback;
        }

        /// <summary>
        /// Numbered picker. Returns null if the player enters 0 or anything invalid,
        /// so every list is escapable.
        /// </summary>
        public static T? Choose<T>(
            string prompt,
            IReadOnlyList<T> options,
            Func<T, string> label,
            Func<T, string>? detail = null,
            string cancelLabel = "cancel") where T : class
        {
            for (int i = 0; i < options.Count; i++)
            {
                Write($"  [{i + 1,2}] ", ConsoleColor.DarkGray);
                Write(Fit(label(options[i]), 30), ConsoleColor.White);
                if (detail != null)
                    Write(Truncate(detail(options[i]), 44), ConsoleColor.DarkGray);
                Console.WriteLine();
            }

            Console.Write($"\n  {prompt} (0 to {cancelLabel}): ");
            if (int.TryParse(Console.ReadLine(), out int c) && c >= 1 && c <= options.Count)
                return options[c - 1];

            return null;
        }

        /// <summary>Numbered picker over an enum's values. Null if cancelled.</summary>
        public static TEnum? ChooseEnum<TEnum>(string prompt) where TEnum : struct, Enum
        {
            var values = Enum.GetValues<TEnum>();

            for (int i = 0; i < values.Length; i++)
            {
                Write($"  [{i + 1,2}] ", ConsoleColor.DarkGray);
                WriteLine(values[i].ToString()!, ConsoleColor.White);
            }

            Console.Write($"\n  {prompt} (0 to cancel): ");
            if (int.TryParse(Console.ReadLine(), out int c) && c >= 1 && c <= values.Length)
                return values[c - 1];

            return null;
        }
    }
}
