using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Context.Core.Linear;

/// <summary>An issue's human key, 'ENG-12': the team's key and the issue's number in that team. It is what the owner types and what Linear's permalinks carry, not the issue's uuid.</summary>
public sealed partial record LinearIssueKey
{
    private LinearIssueKey(string team, int number)
    {
        Team = team;
        Number = number;
    }

    /// <summary>The team's key, 'ENG': a capital letter then up to nine capital letters or digits.</summary>
    public string Team { get; }

    /// <summary>The issue's number in its team, 1 to 999,999,999.</summary>
    public int Number { get; }

    [GeneratedRegex("^(?<team>[A-Z][A-Z0-9]{0,9})-(?<number>[1-9][0-9]{0,8})\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    /// <summary>Reads a key from text.</summary>
    /// <param name="text">The key, 'ENG-12'.</param>
    /// <returns>The key, or SND-CONTEXT-006 when the text isn't a team key, a dash and a number.</returns>
    public static Result<LinearIssueKey> Parse(string? text)
    {
        var match = text is null ? null : Shape.Match(text);
        return match is { Success: true }
            ? new LinearIssueKey(match.Groups["team"].Value, int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture))
            : ContextErrors.QueryInvalid.With($"'{text}' isn't an issue key (a team key, a dash and a number: ENG-12)");
    }

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Team}-{Number}");
}
