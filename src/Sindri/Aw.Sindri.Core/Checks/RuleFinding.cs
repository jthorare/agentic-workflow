using System.Text.Json.Nodes;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Checks;

/// <summary>One finding of a build or ship rule (line spec §7.2, §7.5): the rule that failed and where. A secret's finding names the file and line, never the match.</summary>
/// <param name="Rule">The rule id ('BLD-SCOPE-001').</param>
/// <param name="Message">What is wrong and, where it helps, the fix.</param>
/// <param name="Path">The file it concerns, or null.</param>
/// <param name="Line">The line in that file, or null.</param>
/// <param name="Detail">More for the reader (the tail of a failed command's output), already scrubbed; null for none.</param>
public sealed record RuleFinding(string Rule, string Message, string? Path = null, int? Line = null, string? Detail = null)
{
    /// <summary>The findings as the 'fact:findings' array: each is <c language="text">{ rule, message, path?, line?, detail? }</c>.</summary>
    /// <param name="findings">The findings.</param>
    /// <returns>The array.</returns>
    public static JsonArray ToFact(IEnumerable<RuleFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        return new JsonArray([.. findings.Select(static f => (JsonNode?)f.ToJson())]);
    }

    /// <summary>This finding with its message, path and detail passed through <paramref name="scrubber"/>: a commit subject or a file name can carry a secret too.</summary>
    /// <param name="scrubber">Takes secrets out of text.</param>
    /// <returns>The scrubbed finding.</returns>
    public RuleFinding Scrubbed(ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(scrubber);
        return this with { Message = scrubber.Scrub(Message), Path = Path is null ? null : scrubber.Scrub(Path), Detail = Detail is null ? null : scrubber.Scrub(Detail) };
    }

    private JsonObject ToJson()
    {
        var node = new JsonObject { ["rule"] = Rule, ["message"] = Message };
        if (Path is not null)
        {
            node["path"] = Path;
        }

        if (Line is { } line)
        {
            node["line"] = line;
        }

        if (!string.IsNullOrEmpty(Detail))
        {
            node["detail"] = Detail;
        }

        return node;
    }
}
