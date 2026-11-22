using System.Text.RegularExpressions;

namespace Aw.Redaction;

/// <summary>The [REDACTED:rule] placeholders already in a text, sorted and non-overlapping, so a hit inside one is
/// found by binary search (a text full of placeholders stays linear).</summary>
internal sealed class Placeholders
{
    private readonly int[] _starts;
    private readonly int[] _ends;

    private Placeholders()
    {
        _starts = [];
        _ends = [];
    }

    /// <summary>No placeholder (a text without "[REDACTED:" holds none).</summary>
    public static Placeholders None { get; } = new();

    public Placeholders(MatchCollection matches)
    {
        _starts = new int[matches.Count];
        _ends = new int[matches.Count];
        for (var i = 0; i < matches.Count; i++)
        {
            _starts[i] = matches[i].Index;
            _ends[i] = matches[i].Index + matches[i].Length;
        }
    }

    /// <summary>True when [<paramref name="start"/>, <paramref name="end"/>) lies inside one placeholder.</summary>
    public bool Contain(int start, int end)
    {
        var at = Array.BinarySearch(_starts, start);
        var candidate = at >= 0 ? at : ~at - 1;
        return candidate >= 0 && end <= _ends[candidate];
    }
}
