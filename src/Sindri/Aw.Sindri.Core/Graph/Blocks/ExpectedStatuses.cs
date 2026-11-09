using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The statuses an 'http' node's 'expect' allows: '200,201', '200-299', '2xx' or any mix, default every 2xx.</summary>
internal sealed class ExpectedStatuses
{
    private readonly IReadOnlyList<(int From, int To)> _ranges;

    private ExpectedStatuses(IReadOnlyList<(int From, int To)> ranges, string described)
    {
        _ranges = ranges;
        Described = described;
    }

    /// <summary>The allowed statuses in the words the config used, or '200-299' by default.</summary>
    internal string Described { get; }

    /// <summary>Reads <paramref name="text"/>.</summary>
    /// <param name="text">The 'expect' value, or null for the default.</param>
    /// <returns>The statuses, or why the text is not a list of them.</returns>
    internal static Result<ExpectedStatuses> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ExpectedStatuses([(200, 299)], "200-299");
        }

        var ranges = new List<(int From, int To)>();
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (ReadRange(part) is not { } range)
            {
                return SindriErrors.GraphBlockFailed.With($"'expect' lists statuses such as 200, 200-299 or 2xx, separated by commas; '{part}' is none of those");
            }

            ranges.Add(range);
        }

        return ranges.Count == 0
            ? SindriErrors.GraphBlockFailed.With("'expect' is empty (fix: list a status such as 200, or leave 'expect' out for any 2xx)")
            : new ExpectedStatuses(ranges, text.Trim());
    }

    /// <summary>Whether <paramref name="status"/> is allowed.</summary>
    /// <param name="status">The answer's status.</param>
    /// <returns>True when it is in one of the ranges.</returns>
    internal bool Allows(int status) => _ranges.Any(r => status >= r.From && status <= r.To);

    private static (int From, int To)? ReadRange(string part)
    {
        if (part.Length == 3 && char.IsAsciiDigit(part[0]) && part.EndsWith("xx", StringComparison.OrdinalIgnoreCase))
        {
            var hundred = (part[0] - '0') * 100;
            return (hundred, hundred + 99);
        }

        var ends = part.Split('-');
        return ends.Length is 1 or 2 && ends.All(static e => int.TryParse(e, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            ? (int.Parse(ends[0], CultureInfo.InvariantCulture), int.Parse(ends[^1], CultureInfo.InvariantCulture))
            : null;
    }
}
