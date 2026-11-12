using System.Globalization;
using System.Text;
using Avalonia.Media;

namespace Aw.Spike.T3335;

internal readonly record struct Run(string Text, int Col, uint Fg, uint Bg, bool Bold, bool Dim, bool Underline, bool Reverse);

/// <summary>Minimal SGR parser for `tmux capture-pane -e`: colours (16, 256, truecolor), bold, dim, underline, reverse. Other escapes are dropped.</summary>
internal static class AnsiGrid
{
    public const uint Default = 0;

    private static readonly uint[] Base16 =
    [
        0xFF000000, 0xFFCD3131, 0xFF0DBC79, 0xFFE5E510, 0xFF2472C8, 0xFFBC3FBC, 0xFF11A8CD, 0xFFE5E5E5,
        0xFF666666, 0xFFF14C4C, 0xFF23D18B, 0xFFF5F543, 0xFF3B8EEA, 0xFFD670D6, 0xFF29B8DB, 0xFFFFFFFF,
    ];

    public static List<Run>[] Parse(string text, int rows)
    {
        var lines = text.Split('\n');
        var result = new List<Run>[rows];
        uint fg = Default, bg = Default;
        bool bold = false, dim = false, ul = false, rev = false;
        for (var r = 0; r < rows; r++)
        {
            var runs = new List<Run>();
            result[r] = runs;
            if (r >= lines.Length)
            {
                continue;
            }
            var line = lines[r];
            var sb = new StringBuilder();
            var startCol = 0;
            var col = 0;
            void Flush()
            {
                if (sb.Length > 0)
                {
                    runs.Add(new Run(sb.ToString(), startCol, fg, bg, bold, dim, ul, rev));
                    sb.Clear();
                }
            }
            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '\u001b' && i + 1 < line.Length && line[i + 1] == '[')
                {
                    var j = i + 2;
                    while (j < line.Length && (char.IsDigit(line[j]) || line[j] == ';' || line[j] == ':'))
                    {
                        j++;
                    }
                    if (j < line.Length && line[j] == 'm')
                    {
                        Flush();
                        var ps = line.AsSpan(i + 2, j - i - 2).ToString().Split(';', ':');
                        ApplySgr(ps, ref fg, ref bg, ref bold, ref dim, ref ul, ref rev);
                        startCol = col;
                    }
                    i = j;
                    continue;
                }
                if (ch == '\r')
                {
                    continue;
                }
                if (sb.Length == 0)
                {
                    startCol = col;
                }
                sb.Append(ch);
                col++;
            }
            Flush();
        }
        return result;
    }

    private static void ApplySgr(string[] ps, ref uint fg, ref uint bg, ref bool bold, ref bool dim, ref bool ul, ref bool rev)
    {
        if (ps.Length == 1 && ps[0].Length == 0)
        {
            ps = ["0"];
        }
        for (var i = 0; i < ps.Length; i++)
        {
            if (!int.TryParse(ps[i], NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            {
                continue;
            }
            switch (n)
            {
                case 0: fg = Default; bg = Default; bold = dim = ul = rev = false; break;
                case 1: bold = true; break;
                case 2: dim = true; break;
                case 4: ul = true; break;
                case 7: rev = true; break;
                case 22: bold = dim = false; break;
                case 24: ul = false; break;
                case 27: rev = false; break;
                case >= 30 and <= 37: fg = Base16[n - 30]; break;
                case 39: fg = Default; break;
                case >= 40 and <= 47: bg = Base16[n - 40]; break;
                case 49: bg = Default; break;
                case >= 90 and <= 97: fg = Base16[n - 90 + 8]; break;
                case >= 100 and <= 107: bg = Base16[n - 100 + 8]; break;
                case 38 or 48:
                    var isFg = n == 38;
                    if (i + 1 < ps.Length && ps[i + 1] == "5" && i + 2 < ps.Length && int.TryParse(ps[i + 2], out var idx))
                    {
                        var c = Palette256(idx);
                        if (isFg) { fg = c; } else { bg = c; }
                        i += 2;
                    }
                    else if (i + 1 < ps.Length && ps[i + 1] == "2" && i + 4 < ps.Length
                        && int.TryParse(ps[i + 2], out var r) && int.TryParse(ps[i + 3], out var g) && int.TryParse(ps[i + 4], out var b))
                    {
                        var c = 0xFF000000u | ((uint)(r & 255) << 16) | ((uint)(g & 255) << 8) | (uint)(b & 255);
                        if (isFg) { fg = c; } else { bg = c; }
                        i += 4;
                    }
                    break;
                default: break;
            }
        }
    }

    private static uint Palette256(int i)
    {
        if (i < 16) { return Base16[Math.Max(0, i)]; }
        if (i >= 232)
        {
            var v = (uint)(8 + ((i - 232) * 10));
            return 0xFF000000u | (v << 16) | (v << 8) | v;
        }
        i -= 16;
        static uint Lvl(int x) => x == 0 ? 0u : (uint)(55 + (x * 40));
        return 0xFF000000u | (Lvl(i / 36) << 16) | (Lvl(i / 6 % 6) << 8) | Lvl(i % 6);
    }

    public static IBrush Brush(uint argb) => new SolidColorBrush(Color.FromUInt32(argb));
}
