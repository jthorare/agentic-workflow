namespace Aw.Sindri.Ui.Terminal;

/// <summary>The colours and attributes the next characters take: the state SGR codes (ESC [ ... m) change.</summary>
internal sealed class TerminalPen
{
    /// <summary>The foreground colour.</summary>
    internal TerminalColor Fg { get; private set; } = TerminalColor.Default;

    /// <summary>The background colour.</summary>
    internal TerminalColor Bg { get; private set; } = TerminalColor.Default;

    /// <summary>The attributes.</summary>
    internal TerminalStyle Style { get; private set; } = TerminalStyle.None;

    /// <summary>Applies the parameters of one SGR sequence, the text between ESC [ and m.</summary>
    /// <param name="parameters">Numbers split by ; or :. An empty slot is 0.</param>
    internal void Apply(ReadOnlySpan<char> parameters)
    {
        var values = Numbers(parameters);
        for (var k = 0; k < values.Count; k++)
        {
            var code = Math.Max(values[k], 0);
            if (code is 38 or 48)
            {
                ApplyExtended(code == 38, values, ref k);
            }
            else
            {
                ApplySimple(code);
            }
        }
    }

    private static List<int> Numbers(ReadOnlySpan<char> parameters)
    {
        var values = new List<int>(8);
        var current = 0;
        var has = false;
        foreach (var ch in parameters)
        {
            if (ch is >= '0' and <= '9')
            {
                current = Math.Min((current * 10) + (ch - '0'), 100000);
                has = true;
            }
            else if (ch is ';' or ':')
            {
                values.Add(has ? current : -1);
                current = 0;
                has = false;
            }
        }

        values.Add(has ? current : -1);
        return values;
    }

    private void ApplySimple(int code)
    {
        switch (code)
        {
            case 0:
                Fg = TerminalColor.Default;
                Bg = TerminalColor.Default;
                Style = TerminalStyle.None;
                break;
            case 1 or 3 or 4 or 7:
                Style |= StyleOf(code);
                break;
            case 22 or 23 or 24 or 27:
                Style &= ~StyleCleared(code);
                break;
            case 39:
                Fg = TerminalColor.Default;
                break;
            case 49:
                Bg = TerminalColor.Default;
                break;
            default:
                ApplyPalette(code);
                break;
        }
    }

    private static TerminalStyle StyleCleared(int off) => off switch
    {
        22 => TerminalStyle.Bold,
        23 => TerminalStyle.Italic,
        24 => TerminalStyle.Underline,
        _ => TerminalStyle.Inverse,
    };

    private static TerminalStyle StyleOf(int on) => on switch
    {
        1 => TerminalStyle.Bold,
        3 => TerminalStyle.Italic,
        4 => TerminalStyle.Underline,
        _ => TerminalStyle.Inverse,
    };

    private void ApplyPalette(int code)
    {
        if (code is >= 30 and <= 37)
        {
            Fg = TerminalColor.Indexed(code - 30);
        }
        else if (code is >= 40 and <= 47)
        {
            Bg = TerminalColor.Indexed(code - 40);
        }
        else if (code is >= 90 and <= 97)
        {
            Fg = TerminalColor.Indexed(code - 90 + 8);
        }
        else if (code is >= 100 and <= 107)
        {
            Bg = TerminalColor.Indexed(code - 100 + 8);
        }
    }

    private void ApplyExtended(bool foreground, List<int> values, ref int k)
    {
        var color = ReadExtended(values, ref k);
        if (color is null)
        {
            return;
        }

        if (foreground)
        {
            Fg = color.Value;
        }
        else
        {
            Bg = color.Value;
        }
    }

    // values[k] is 38 or 48. Reads "5;n" or "2;r;g;b"; the colon form may carry an empty colour-space slot before r.
    private static TerminalColor? ReadExtended(List<int> values, ref int k)
    {
        var mode = k + 1 < values.Count ? values[k + 1] : 0;
        if (mode == 5 && k + 2 < values.Count)
        {
            var slot = values[k + 2];
            k += 2;
            return slot is >= 0 and <= 255 ? TerminalColor.Indexed(slot) : null;
        }

        if (mode == 2)
        {
            var start = k + 2;
            if (start + 3 < values.Count && values[start] < 0)
            {
                start++;
            }

            if (start + 2 < values.Count)
            {
                k = start + 2;
                return TerminalColor.FromRgb(Clamp(values[start]), Clamp(values[start + 1]), Clamp(values[start + 2]));
            }
        }

        k = values.Count;
        return null;
    }

    private static int Clamp(int channel) => Math.Clamp(channel, 0, 255);
}
