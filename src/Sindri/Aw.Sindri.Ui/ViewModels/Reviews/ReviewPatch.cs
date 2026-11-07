using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>Reads a file's unified patch into hunks and numbered lines (review page spec §6.4).</summary>
internal static partial class ReviewPatch
{
    [GeneratedRegex(@"^@@ -(?<os>\d+)(?:,(?<ol>\d+))? \+(?<ns>\d+)(?:,(?<nl>\d+))? @@", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Header { get; }

    /// <summary>The hunks of a patch; text before the first "@@" line is ignored.</summary>
    /// <param name="patch">The patch.</param>
    /// <param name="path">The file's path, for the lines' focus keys.</param>
    /// <returns>The hunks.</returns>
    internal static List<ReviewDiffHunkViewModel> Hunks(string patch, string path)
    {
        var reader = new Reader(path);
        foreach (var text in patch.Split('\n'))
        {
            reader.Take(text);
        }

        reader.Flush();
        return reader.Hunks;
    }

    private static int Number(Match match, string group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);

    private sealed class Reader(string path)
    {
        private string? _header;
        private int _from;
        private int _to;
        private int _old;
        private int _new;
        private List<ReviewDiffLineViewModel> _lines = [];

        internal List<ReviewDiffHunkViewModel> Hunks { get; } = [];

        internal void Take(string text)
        {
            if (Header.Match(text) is { Success: true } match)
            {
                Flush();
                _header = text;
                (_old, _new) = (Number(match, "os"), Number(match, "ns"));
                (_from, _to) = (_new, _new + Math.Max(match.Groups["nl"].Success ? Number(match, "nl") : 1, 1) - 1);
            }
            else if (_header is not null && text.Length > 0)
            {
                Line(text);
            }
        }

        internal void Flush()
        {
            if (_header is not null)
            {
                Hunks.Add(new ReviewDiffHunkViewModel(_header, _from, _to, _lines));
                _lines = [];
                _header = null;
            }
        }

        private void Line(string text)
        {
            switch (text[0])
            {
                case '+':
                    _lines.Add(new ReviewDiffLineViewModel(ReviewDiffLineKind.Added, oldNumber: null, _new++, text[1..], path));
                    break;
                case '-':
                    _lines.Add(new ReviewDiffLineViewModel(ReviewDiffLineKind.Removed, _old++, newNumber: null, text[1..], path));
                    break;
                case ' ':
                    _lines.Add(new ReviewDiffLineViewModel(ReviewDiffLineKind.Context, _old++, _new++, text[1..], path));
                    break;
                default:
                    break;   // "\ No newline at end of file"
            }
        }
    }
}
