using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Skills;

/// <summary>The ```aw-result block of a skill's SKILL.md (skill flows spec §3.1): the keys and artifact file names an output's 'from' may use.</summary>
internal static partial class SkillAwResult
{
    private const string ResultFence = "```aw-result";

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

    internal static Shape? Read(string? markdown)
    {
        if (markdown is null || markdown.IndexOf(ResultFence, StringComparison.Ordinal) is not (>= 0 and var start))
        {
            return null;
        }

        var body = markdown[(start + ResultFence.Length)..];
        var end = body.IndexOf("```", StringComparison.Ordinal);
        try
        {
            using var document = JsonDocument.Parse(end < 0 ? body : body[..end]);
            var keys = document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.EnumerateObject().Select(static p => p.Name).ToHashSet(StringComparer.Ordinal) : [];
            var artifacts = document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("artifacts", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Select(static a => (a.GetString() ?? string.Empty)[((a.GetString() ?? string.Empty).LastIndexOf('/') + 1)..]).ToHashSet(StringComparer.Ordinal)
                : [];
            return new Shape(keys, artifacts);
        }
        catch (JsonException)
        {
            return null;
        }
    }


    [GeneratedRegex("^artifacts\\[(?<name>.+)\\]\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ArtifactName { get; }

    internal sealed record Shape(HashSet<string> Keys, HashSet<string> Artifacts);
}
