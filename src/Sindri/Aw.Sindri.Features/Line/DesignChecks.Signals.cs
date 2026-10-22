using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Features.Line;

internal static partial class DesignChecks
{
    private static readonly string[] _runs = ["head", "base+head"];

    // FA §17.23.14's unmeasurable terms, a rule pack: a term without a number or a reference artifact is refused.
    private static readonly string[] _unmeasurable =
    [
        "fast", "faster", "quick", "quickly", "slow", "clean", "cleaner", "intuitive", "easy", "easily", "simple", "robust", "user-friendly", "seamless",
        "snappy", "smooth", "scalable", "performant", "efficient", "reliable", "modern", "good", "better", "nice", "lightweight", "responsive",
    ];

    // DSN-ACC-001, DSN-ACC-002
    private static IEnumerable<LineFinding> Signals(DesignContext c)
    {
        var signals = LineJson.Items(c.Design, "signals").ToList();
        foreach (var item in c.Live.Select(static i => LineJson.Str(i, "id")).OfType<string>())
        {
            var own = signals.Where(s => string.Equals(LineJson.Str(s, "item"), item, StringComparison.Ordinal)).ToList();
            if (!own.Exists(IsComplete))
            {
                yield return DesignContext.Fail("DSN-ACC-001", item, $"acceptance item {item} has no proof signal with an evidence kind, an observable statement and where it runs (head or base+head)", "Add a signal: kind, statement, runs.");
            }
        }

        foreach (var signal in signals.Where(static s => string.Equals(LineJson.Str(s, "runs"), "base+head", StringComparison.Ordinal) && !HasWorkspaceScript(s)))
        {
            var item = LineJson.Str(signal, "item");
            yield return DesignContext.Fail("DSN-ACC-001", item, $"the base+head signal for {item ?? "an item"} names no script inside the workspace", "Give it a 'script': a relative path (no '..', not absolute) of the file that fails at the base and passes at the head.");
        }

        foreach (var signal in signals.Where(Unmeasurable))
        {
            var item = LineJson.Str(signal, "item");
            yield return DesignContext.Fail("DSN-ACC-002", item, $"the signal for {item ?? "an item"} says \"{LineJson.Str(signal, "statement")}\" without a number or a reference artifact", "State a number (a limit, a count, a size) or name the reference artifact it is compared with.");
        }
    }

    // The before/after Step runs this one file in both trees, so it is a path inside the workspace.
    private static bool HasWorkspaceScript(JsonNode signal) =>
        LineJson.Str(signal, "script") is { } path && !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !path.Split('/', '\\').Contains("..", StringComparer.Ordinal);

    private static bool IsComplete(JsonNode signal) =>
        LineJson.Str(signal, "kind") is { Length: > 0 } kind && KebabCase.IsMatch(kind)
        && !string.IsNullOrWhiteSpace(LineJson.Str(signal, "statement"))
        && LineJson.Str(signal, "runs") is { } runs && Array.Exists(_runs, r => string.Equals(r, runs, StringComparison.Ordinal));

    private static bool Unmeasurable(JsonNode signal)
    {
        var statement = LineJson.Str(signal, "statement") ?? string.Empty;
        if (statement.Any(char.IsAsciiDigit) || !string.IsNullOrWhiteSpace(LineJson.Str(signal, "reference")))
        {
            return false;
        }

        var words = Words.Matches(statement.ToLowerInvariant()).Select(static m => m.Value).ToHashSet(StringComparer.Ordinal);
        return _unmeasurable.Any(words.Contains);
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KebabCase { get; }

    [GeneratedRegex("[a-z][a-z-]*[a-z]|[a-z]", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Words { get; }
}
