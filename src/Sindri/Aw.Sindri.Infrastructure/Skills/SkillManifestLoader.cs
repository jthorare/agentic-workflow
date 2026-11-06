using Aw.Sindri.Core.Skills;
using Aw.Sindri.Infrastructure.Profile;

namespace Aw.Sindri.Infrastructure.Skills;

/// <summary>Reads every 'skills/*/block.yaml' under one folder into skill blocks (skill flows spec §3.2). A manifest that fails the checks is skipped with a reason, never thrown; a missing folder is an empty list with one 'no skills folder' reason.</summary>
internal static class SkillManifestLoader
{
    private const string ManifestFile = "block.yaml";

    /// <summary>Loads the folder.</summary>
    /// <param name="skillsFolder">The skills folder: the one that holds '&lt;skill&gt;/block.yaml'.</param>
    /// <returns>The blocks that loaded (sorted by id) and the ones skipped.</returns>
    internal static SkillBlocks Load(string skillsFolder)
    {
        ArgumentNullException.ThrowIfNull(skillsFolder);
        if (skillsFolder.Length == 0 || !Directory.Exists(skillsFolder))
        {
            return new SkillBlocks([], [new SkippedSkillBlock("skills", skillsFolder, "no skills folder")]);
        }

        var loaded = new List<LoadedSkillBlock>();
        var skipped = new List<SkippedSkillBlock>();
        foreach (var folder in Directory.EnumerateDirectories(skillsFolder).Order(StringComparer.Ordinal))
        {
            var manifest = Path.Combine(folder, ManifestFile);
            if (!File.Exists(manifest))
            {
                continue;   // a skill without a manifest is not a block
            }

            var parsed = One(Path.GetFileName(folder), manifest, Path.Combine(folder, "SKILL.md"));
            loaded.AddRange(parsed.Loaded);
            skipped.AddRange(parsed.Skipped);
        }

        return Unique(loaded, skipped);
    }

    private static SkillBlocks One(string skill, string manifest, string markdownPath)
    {
        string text;
        string? markdown;
        string? sha;
        try
        {
            text = File.ReadAllText(manifest);
            var bytes = File.Exists(markdownPath) ? File.ReadAllBytes(markdownPath) : null;
            markdown = bytes is null ? null : System.Text.Encoding.UTF8.GetString(bytes);
            sha = bytes is null ? null : Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));   // why: the bytes on disk, so the agent home's copy compares equal
        }
        catch (IOException e)
        {
            return new SkillBlocks([], [new SkippedSkillBlock(skill, manifest, "it can't be read: " + e.Message)]);
        }

        var problem = YamlToJson.TryConvert(text, out var json);
        return problem is not null
            ? new SkillBlocks([], [new SkippedSkillBlock(skill, manifest, "it isn't valid YAML: " + problem)])
            : WithSha(SkillManifestParser.Parse(skill, manifest, System.Text.Encoding.UTF8.GetString(json), markdown), sha);
    }

    private static SkillBlocks WithSha(SkillBlocks parsed, string? sha) =>
        new([.. parsed.Loaded.Select(b => b with { SkillMdSha = sha })], parsed.Skipped);

    // Ids and derived fact keys are unique across all manifests (spec §3.1 Checks); the later duplicate is skipped.
    private static SkillBlocks Unique(List<LoadedSkillBlock> loaded, List<SkippedSkillBlock> skipped)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<LoadedSkillBlock>();
        foreach (var block in loaded.OrderBy(static b => b.Manifest.Name, StringComparer.Ordinal))
        {
            var facts = block.Descriptor.Outputs.Select(static o => o.Fact.Value).ToList();
            if (!ids.Add(block.Manifest.Name))
            {
                skipped.Add(new SkippedSkillBlock(block.Manifest.Name, block.Path, "another manifest already has a block with this id"));
            }
            else if (facts.FirstOrDefault(f => !keys.Add(f)) is { } clash)
            {
                skipped.Add(new SkippedSkillBlock(block.Manifest.Name, block.Path, $"its fact key {clash} is already another block's"));
            }
            else
            {
                kept.Add(block);
            }
        }

        return new SkillBlocks(kept, skipped);
    }
}
