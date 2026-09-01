using System;
using System.Collections.Generic;
using WrestlingSim.Enums;
using WrestlingSim.Models;

namespace WrestlingSim.UI
{
    public static class MainMenu
    {
        private const int InnerW = 54;

        public static void Render()
        {
            ConsoleUi.Clear();
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine();
            DrawHeader();
            Console.WriteLine();
            DrawMenuBox();
            Console.WriteLine();
        }

        // ─── Header ──────────────────────────────────────────────────────────────

        private static void DrawHeader()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            HLine('╔', '═', '╗');
            HBlank();
            HCenter("★  W R E S T L I N G   S I M  ★");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            HCenter("B O O K I N G   S Y S T E M");
            Console.ForegroundColor = ConsoleColor.Yellow;
            HBlank();
            HLine('╚', '═', '╝');
            Console.ResetColor();
        }

        // ─── Menu ────────────────────────────────────────────────────────────────

        private static void DrawMenuBox()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            HLine('┌', '─', '┐');
            MBlank();
            Console.ResetColor();

            MenuItem("1", "Book a Match");
            MBlank();
            MenuItem("2", "Book a Segment");
            MBlank();
            MenuItem("3", "Book a Show");
            MBlank();
            MenuItem("4", "View Wrestlers");
            MBlank();
            MenuItem("5", "View Feuds");
            MBlank();
            MenuItem("6", "Exit");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            MBlank();
            HLine('└', '─', '┘');
            Console.ResetColor();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  Select option: ");
            Console.ResetColor();
        }

        private static void MenuItem(string key, string label)
        {
            // Inner layout: "    [ x ]   {label}{trailing}"
            // 4 + 5 + 3 + label + trailing = InnerW
            int trailing = InnerW - 4 - 5 - 3 - label.Length;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("│    ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"[ {key} ]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"   {label}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(new string(' ', Math.Max(0, trailing)) + "│");
            Console.ResetColor();
        }

        private static void MBlank()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("│" + new string(' ', InnerW) + "│");
            Console.ResetColor();
        }

        // ─── Wrestler Roster ─────────────────────────────────────────────────────

        public static void RenderWrestlers(List<Wrestler> wrestlers)
        {
            ConsoleUi.Clear();
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine();

            const int nW = 18, aW = 8, sW = 11, pW = 5, skW = 6, cW = 8;
            // Row inner: 2(pad) + nW + 2 + aW + 2 + sW + 2 + pW + 2 + skW + 2 + cW + 2(pad) = cols + 14
            int rW = nW + aW + sW + pW + skW + cW + 14;

            Console.ForegroundColor = ConsoleColor.Yellow;
            HLineW('╔', '═', '╗', rW);
            HCenterW("WRESTLER ROSTER", rW);
            HLineW('╠', '═', '╣', rW);
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                $"║  {"NAME",-nW}  {"ALIGN",-aW}  {"STYLE",-sW}  {"POP",pW}  {"SKILL",skW}  {"CHA",cW}  ║");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Yellow;
            HLineW('╠', '═', '╣', rW);
            Console.ResetColor();

            foreach (var w in wrestlers)
            {
                string name  = Fit(w.RingName ?? "?", nW);
                string align = Fit(w.Gimmick?.NaturalAlignment.ToString() ?? "-", aW);
                string style = Fit(w.Style.ToString(), sW);
                string pop   = w.Overness.ToString().PadLeft(pW);
                string skill = w.RingSkills.GetOverallSkill().ToString("F2").PadLeft(skW);
                string cha   = w.Charisma.ToString("F1").PadLeft(cW);

                ConsoleColor alignColor = w.Gimmick?.NaturalAlignment switch
                {
                    Alignment.Face    => ConsoleColor.Green,
                    Alignment.Heel    => ConsoleColor.Red,
                    Alignment.Tweener => ConsoleColor.Yellow,
                    _                 => ConsoleColor.Gray
                };

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("║  ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(name + "  ");
                Console.ForegroundColor = alignColor;
                Console.Write(align + "  ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{style}  {pop}  {skill}  {cha}  ");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("║");
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            HLineW('╚', '═', '╝', rW);
            Console.ResetColor();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  Press any key to return to the main menu...");
            Console.ResetColor();
            ConsoleUi.AnyKey();
        }

        // ─── Shared helpers ──────────────────────────────────────────────────────

        private static void HLine(char l, char fill, char r) =>
            Console.WriteLine(l + new string(fill, InnerW) + r);

        private static void HLineW(char l, char fill, char r, int w) =>
            Console.WriteLine(l + new string(fill, w) + r);

        private static void HBlank() =>
            Console.WriteLine("║" + new string(' ', InnerW) + "║");

        private static void HCenter(string text)
        {
            int pad = (InnerW - text.Length) / 2;
            string content = (new string(' ', pad) + text).PadRight(InnerW);
            Console.WriteLine("║" + content + "║");
        }

        private static void HCenterW(string text, int w)
        {
            int pad = (w - text.Length) / 2;
            string content = (new string(' ', pad) + text).PadRight(w);
            Console.WriteLine("║" + content + "║");
        }

        private static string Fit(string s, int len) =>
            s.Length > len ? s[..(len - 1)] + "…" : s.PadRight(len);
    }
}
