using System.Text.RegularExpressions;
using Aw.Evidence.Core.Ids;
using Aw.Kernel;

namespace Aw.Evidence.Core.Scripts;

/// <summary>
/// What a click, fill or press step's target names: a bare test id, 'role:&lt;role&gt;|&lt;accessible name&gt;' or 'text:&lt;text&gt;'.
/// A trailing '#&lt;n&gt;' (1-based, hugging the name: 'Day#2') picks the nth of several matches; without it exactly one must match.
/// One grammar for the schema, the write guard and the browser.
/// </summary>
public abstract partial record StepTarget
{
    private const string RolePrefix = "role:";
    private const string TextPrefix = "text:";

    /// <summary>The human-readable part the write guard reads: a test id, an accessible name or a text (the '#&lt;n&gt;' dropped).</summary>
    public abstract string Label { get; }

    /// <summary>Whether <paramref name="target"/> carries a 'role:' or 'text:' prefix.</summary>
    /// <param name="target">The step's target.</param>
    /// <returns>Whether it is prefixed.</returns>
    public static bool IsPrefixed(string target) =>
        target.StartsWith(RolePrefix, StringComparison.Ordinal) || target.StartsWith(TextPrefix, StringComparison.Ordinal);

    /// <summary>Reads a target.</summary>
    /// <param name="target">The step's target.</param>
    /// <returns>The parsed target, or AW-EVIDENCE-002 naming the rule it breaks.</returns>
    public static Result<StepTarget> Parse(string target)
    {
        // "Role:button|Save" or " text:Save" would silently become a test id and miss: refuse the near-miss.
        if (!IsPrefixed(target) && NearMiss.IsMatch(target))
        {
            return EvidenceErrors.ScriptInvalid.With($"target prefix must be exactly lower-case \"role:\" or \"text:\" with no leading space: {target}");
        }

        if (target.StartsWith(RolePrefix, StringComparison.Ordinal))
        {
            return ParseRole(target);
        }

        if (target.StartsWith(TextPrefix, StringComparison.Ordinal))
        {
            var (text, nth, problem) = SplitNth(target[TextPrefix.Length..]);
            return problem is not null
                ? EvidenceErrors.ScriptInvalid.With(problem)
                : string.IsNullOrWhiteSpace(text)
                    ? EvidenceErrors.ScriptInvalid.With($"text target must be \"text:<text>[#<n>]\": {target}")
                    : new TextTarget(text, nth);
        }

        return TestId.TryFrom(target, out var id)
            ? new TestIdTarget(id)
            : EvidenceErrors.ScriptInvalid.With("target must not be blank");
    }

    private static Result<StepTarget> ParseRole(string target)
    {
        var rest = target[RolePrefix.Length..];
        var bar = rest.IndexOf('|', StringComparison.Ordinal);
        var role = bar < 0 ? string.Empty : rest[..bar].Trim();
        var (name, nth, problem) = SplitNth(bar < 0 ? string.Empty : rest[(bar + 1)..]);
        if (problem is not null)
        {
            return EvidenceErrors.ScriptInvalid.With(problem);
        }

        return !RoleName.IsMatch(role) || string.IsNullOrWhiteSpace(name)
            ? EvidenceErrors.ScriptInvalid.With($"role target must be \"role:<lower-case role>|<accessible name>[#<n>]\": {target}")
            : new RoleTarget(role, name, nth);
    }

    // "Shift #12" (a space before '#') is a plain name; the index must hug the name: "Day#2".
    private static (string Label, int? Nth, string? Problem) SplitNth(string label)
    {
        var match = TrailingIndex.Match(label);
        if (!match.Success)
        {
            return (label, null, null);
        }

        return int.TryParse(match.Groups["n"].Value, out var nth) && nth >= 1
            ? (match.Groups["label"].Value, nth, null)
            : (label, null, $"#<n> must be a 1-based integer: {label}");
    }

    [GeneratedRegex("^(?<label>.*\\S)#(?<n>\\d+)\\z", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex TrailingIndex { get; }

    [GeneratedRegex("^\\s*(role|text):", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex NearMiss { get; }

    [GeneratedRegex("^[a-z]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex RoleName { get; }
}
