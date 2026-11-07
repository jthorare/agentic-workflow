using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Skills;

/// <summary>The ```aw-result block of a skill's SKILL.md (skill flows spec §3.1): the keys and artifact file names an output's 'from' may use.</summary>
internal static partial class SkillAwResult
{
    internal static void Check(SkillBlockManifest manifest, Shape? shape, bool hasSkillMarkdown)
    {
        if (!hasSkillMarkdown)
        {
            return;
        }

        if (shape is null)
        {
            throw new ManifestProblemException("SKILL.md has no aw-result block");
        }

        if (shape.Problem is { } problem)
        {
            throw new ManifestProblemException(problem);
        }

        foreach (var output in manifest.Outputs)
        {
            var named = ArtifactName.Match(output.From);
            if (named.Success ? !shape.Artifacts.Contains(named.Groups["name"].Value) : !shape.Keys.Contains(output.From))
            {
                throw new ManifestProblemException($"output '{output.Key}' reads '{output.From}', which the aw-result block in SKILL.md doesn't have");
            }
        }

        if (manifest.Outcomes.From is { } verdict && !shape.Keys.Contains(verdict))
        {
            throw new ManifestProblemException($"outcomes read '{verdict}', which the aw-result block in SKILL.md doesn't have");
        }
    }

    // The same reading as scripts/check-skill-blocks.mjs: the fences inside the '## Result block' section, and exactly one of them.
    internal static Shape? Read(string? markdown)
    {
        if (markdown is null || ResultSection.Match(markdown) is not { Success: true } section)
        {
            return null;
        }

        var fences = Fence.Matches(section.Groups["body"].Value);
        if (fences.Count != 1)
        {
            return fences.Count == 0 ? null : new Shape([], [], $"SKILL.md's Result block has {fences.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} aw-result blocks (need exactly one)");
        }

        try
        {
            using var document = JsonDocument.Parse(fences[0].Groups["json"].Value);
            var keys = document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.EnumerateObject().Select(static p => p.Name).ToHashSet(StringComparer.Ordinal) : [];
            var artifacts = document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("artifacts", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Where(static a => a.ValueKind == JsonValueKind.String).Select(static a => a.GetString() ?? string.Empty).Select(static a => a[(a.LastIndexOf('/') + 1)..]).ToHashSet(StringComparer.Ordinal)
                : [];
            return new Shape(keys, artifacts);
        }
        catch (JsonException e)
        {
            return new Shape([], [], "the aw-result block in SKILL.md is not valid JSON (" + e.Message + ")");
        }
    }

    [GeneratedRegex("^## Result block[ \\t]*\\r?$(?<body>[\\s\\S]*?)(?=^## |\\z)", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ResultSection { get; }

    [GeneratedRegex("^```aw-result[ \\t]*\\r?\\n(?<json>[\\s\\S]*?)^```[ \\t]*\\r?$", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Fence { get; }

    [GeneratedRegex("^artifacts\\[(?<name>.+)\\]\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArtifactName { get; }

    internal sealed record Shape(HashSet<string> Keys, HashSet<string> Artifacts, string? Problem = null);
}
