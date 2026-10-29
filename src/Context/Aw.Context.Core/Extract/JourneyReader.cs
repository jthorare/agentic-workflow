using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aw.Context.Core.Extract;

/// <summary>
/// Reads the journeys in one file (S-FLOW-J): each Playwright 'test("title", ...)' block, or one ui-evidence plan. A step is a test id, a route path
/// or an accessible name, in the order written; a filled value is never read, and a form the reader does not follow adds no step. Pure.
/// </summary>
internal static partial class JourneyReader
{
    private static readonly HashSet<string> _planActions = new(["click", "fill", "check", "select", "selectOption"], StringComparer.Ordinal);

    internal static JourneyRead Read(RepoFile file) =>
        file.Path.EndsWith(".json", StringComparison.Ordinal) ? Plan(file) : Script(file.Text);

    // The path of a URL or route: no origin, query or fragment; null for anything not rooted at '/'.
    internal static string? PathOf(string url)
    {
        var rest = url;
        var scheme = rest.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            var slash = rest.IndexOf('/', scheme + 3);
            rest = slash < 0 ? "/" : rest[slash..];
        }

        var end = rest.IndexOfAny(['?', '#']);
        var path = end < 0 ? rest : rest[..end];
        return path.StartsWith('/') && !path.Contains("${", StringComparison.Ordinal) ? path : null;
    }

    private static JourneyRead Plan(RepoFile file)
    {
        try
        {
            using var document = JsonDocument.Parse(file.Text);
            var root = document.RootElement;
            if (!root.TryGetProperty("steps", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return new JourneyRead([], 1);
            }

            var steps = list.EnumerateArray().Select(PlanStep).ToList();
            return new JourneyRead([new Journey(Text(root, "title") ?? string.Empty, [.. steps.OfType<JourneyStep>()])], steps.Count(s => s is null));
        }
        catch (JsonException)
        {
            return new JourneyRead([], 1);
        }
    }

    // goto's target is a path; click, fill, check and select name a test id. The value a step fills is never read.
    private static JourneyStep? PlanStep(JsonElement step)
    {
        var (action, target) = (Text(step, "action"), Text(step, "target"));
        if (action == "goto")
        {
            return target is not null && PathOf(target) is { } path ? new JourneyStep(StepKind.Route, path) : null;
        }

        return action is not null && _planActions.Contains(action) && !string.IsNullOrEmpty(target) ? new JourneyStep(StepKind.TestId, target) : null;
    }

    private static string? Text(JsonElement step, string name) =>
        step.ValueKind == JsonValueKind.Object && step.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static JourneyRead Script(string text)
    {
        var blocks = TestBlock.Matches(text).ToList();
        List<Journey> journeys = [];
        var unsupported = 0;
        for (var i = 0; i < blocks.Count; i++)
        {
            var start = blocks[i].Index + blocks[i].Length;
            var end = i + 1 < blocks.Count ? blocks[i + 1].Index : text.Length;
            var (steps, skipped) = Body(text[start..end]);
            unsupported += skipped;
            journeys.Add(new Journey(blocks[i].Groups["t"].Value, steps));
        }

        return new JourneyRead(journeys, unsupported);
    }

    private static (List<JourneyStep> Steps, int Unsupported) Body(string body)
    {
        List<JourneyStep> steps = [];
        var unsupported = 0;
        foreach (Match m in Event.Matches(body))
        {
            if (m.Groups["loc"].Success)
            {
                unsupported++;
            }
            else if (m.Groups["goto"].Success || m.Groups["url"].Success)
            {
                var path = PathOf(m.Groups["goto"].Success ? m.Groups["gurl"].Value : m.Groups["uurl"].Value);
                if (path is null)
                {
                    unsupported++;
                }
                else
                {
                    steps.Add(new JourneyStep(StepKind.Route, path));
                }
            }
            else if (Locator(m.Groups["chain"].Value) is { } step)
            {
                steps.Add(step);
            }
            else
            {
                unsupported++;
            }
        }

        return (steps, unsupported);
    }

    // A chained locator reads as one step: its test id when it names one, else its first accessible name.
    private static JourneyStep? Locator(string chain)
    {
        string? name = null;
        foreach (Match part in ChainPart.Matches(chain))
        {
            var args = part.Groups["a"].Value;
            if (part.Groups["k"].Value == "TestId")
            {
                if (Literal.Match(args) is { Success: true } id && id.Groups["v"].Value.Length > 0)
                {
                    return new JourneyStep(StepKind.TestId, id.Groups["v"].Value);
                }
            }
            else if (name is null)
            {
                var found = part.Groups["k"].Value == "Role" ? RoleName.Match(args) : Literal.Match(args);
                name = found.Success && found.Groups["v"].Value.Length > 0 ? found.Groups["v"].Value : null;
            }
        }

        return name is null ? null : new JourneyStep(StepKind.Name, name);
    }

    [GeneratedRegex("\\b(?:test|it)(?:\\.(?:only|skip|fixme|slow))?\\(\\s*(?<q>[\"'`])(?<t>(?:(?!\\k<q>)[^\\\\\\r\\n])*)\\k<q>\\s*,", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TestBlock { get; }

    [GeneratedRegex(
        "(?<goto>\\.goto\\(\\s*(?<gq>[\"'`])(?<gurl>[^\"'`\\r\\n]*)\\k<gq>)"
        + "|(?<url>expect\\(\\s*page\\s*\\)\\.toHaveURL\\(\\s*(?<uq>[\"'`])(?<uurl>[^\"'`\\r\\n]*)\\k<uq>)"
        + "|\\bpage(?<chain>(?:\\.getBy(?:TestId|Role|Label|Text|Placeholder)\\((?:[^()]|\\([^()]*\\))*\\))+)\\s*\\.(?:click|fill|check|uncheck|selectOption)\\("
        + "|(?<loc>\\bpage\\.locator\\()",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex Event { get; }

    [GeneratedRegex("getBy(?<k>TestId|Role|Label|Text|Placeholder)\\((?<a>(?:[^()]|\\([^()]*\\))*)\\)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ChainPart { get; }

    [GeneratedRegex("\\A\\s*(?:\"(?<v>[^\"\\\\\\r\\n]*)\"|'(?<v>[^'\\\\\\r\\n]*)')", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Literal { get; }

    [GeneratedRegex("\\bname:\\s*(?:\"(?<v>[^\"\\\\\\r\\n]*)\"|'(?<v>[^'\\\\\\r\\n]*)')", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RoleName { get; }
}
