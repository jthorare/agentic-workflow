using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Judge.Core.Traits;

/// <summary>Reads 'git diff --src-prefix=a/ --dst-prefix=b/' output line by line into per-file hunks.</summary>
internal sealed partial class DiffReader
{
    private readonly List<(string Path, List<DiffHunk> Hunks)> _files = [];
    private List<DiffHunk>? _hunks;
    private (int Start, int Count, List<string> Lines)? _open;

    [GeneratedRegex(@"^@@ -\d+(?:,\d+)? \+(?<start>\d+)(?:,(?<count>\d+))? @@", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HunkHeader { get; }

    internal static IReadOnlyList<FileDiff> Read(string diff)
    {
        var reader = new DiffReader();
        foreach (var line in diff.Split('\n'))
        {
            reader.Take(line);
        }

        reader.CloseHunk();
        return [.. reader._files.Where(file => file.Hunks.Count > 0).Select(file => new FileDiff(file.Path, file.Hunks))];
    }

    private void Take(string line)
    {
        if (line.StartsWith("diff --git ", StringComparison.Ordinal))
        {
            CloseHunk();
            _hunks = null;
        }
        else if (_open is null && line.StartsWith("+++ ", StringComparison.Ordinal))
        {
            StartFile(line[4..]);
        }
        else if (_hunks is not null && HunkHeader.Match(line) is { Success: true } header)
        {
            OpenHunk(line, header);
        }
        else if (_open is { } open && line.Length > 0 && line[0] is ' ' or '+' or '-' or '\\')
        {
            open.Lines.Add(line);
        }
    }

    // A deleted file ('+++ /dev/null') and a quoted path have no 'b/' target: skipped.
    private void StartFile(string target)
    {
        _hunks = target.StartsWith("b/", StringComparison.Ordinal) ? [] : null;
        if (_hunks is not null)
        {
            _files.Add((target[2..], _hunks));
        }
    }

    private void OpenHunk(string header, Match match)
    {
        CloseHunk();
        var count = match.Groups["count"].Success ? int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture) : 1;
        _open = (int.Parse(match.Groups["start"].Value, CultureInfo.InvariantCulture), count, [header]);
    }

    private void CloseHunk()
    {
        if (_hunks is not null && _open is (var start, var count, var lines))
        {
            _hunks.Add(new DiffHunk(start, start + Math.Max(count, 1) - 1, string.Join('\n', lines)));
        }

        _open = null;
    }
}
