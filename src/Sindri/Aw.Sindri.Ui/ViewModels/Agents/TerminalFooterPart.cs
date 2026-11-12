using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>One part of the footer, between two " · " separators (DG2 draws a hairline between parts). The first part reads in the muted ink.</summary>
/// <param name="Runs">The runs in order.</param>
/// <param name="IsFirst">True for the first part.</param>
/// <param name="IsLast">True for the last part, which has no divider after it.</param>
internal sealed partial record TerminalFooterPart(IReadOnlyList<TerminalFooterRun> Runs, bool IsFirst, bool IsLast)
{
    /// <summary>Splits a footer string into parts and runs. Key names: F6, Esc, n, the chords, and Enter before " to ".</summary>
    /// <param name="footer">The footer text.</param>
    /// <returns>The parts.</returns>
    internal static IReadOnlyList<TerminalFooterPart> Parse(string footer)
    {
        var parts = new List<TerminalFooterPart>();
        foreach (var piece in footer.Split(" · ", StringSplitOptions.RemoveEmptyEntries))
        {
            var runs = new List<TerminalFooterRun>();
            var at = 0;
            foreach (Match m in KeyName.Matches(piece))
            {
                if (m.Index > at)
                {
                    runs.Add(new TerminalFooterRun(Text: piece[at..m.Index], IsKey: false));
                }

                runs.Add(new TerminalFooterRun(Text: m.Value, IsKey: true));
                at = m.Index + m.Length;
            }

            if (at < piece.Length)
            {
                runs.Add(new TerminalFooterRun(Text: piece[at..], IsKey: false));
            }

            parts.Add(new TerminalFooterPart(Runs: runs, IsFirst: parts.Count == 0, IsLast: false));
        }

        return parts.Count == 0 ? parts : [.. parts[..^1], parts[^1] with { IsLast = true }];
    }

    [GeneratedRegex(@"(?<![\w+])(F6|Esc|n|Ctrl\+Shift\+L|Cmd\+Shift\+L|Shift\+PgUp|Enter(?= to ))(?![\w+])", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KeyName { get; }
}
