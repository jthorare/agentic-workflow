using System.Security.Cryptography;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Skills;

namespace Aw.Sindri.Features.GetSkills;

/// <summary>Lists the skill blocks serve loaded at start, with the path each came from and the reason for any that didn't load (skill flows spec §7).</summary>
internal sealed class GetSkillsService(ISkillCatalog catalog) : IService<GetSkillsRequest, GetSkillsResponse>
{
    public ValueTask<Result<GetSkillsResponse>> Handle(GetSkillsRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var blocks = catalog.Blocks;
        var skipped = blocks.Skipped.Select(s => new SkillNotLoadedView(s.Name, s.Path, s.Reason)).ToList();
        return ValueTask.FromResult<Result<GetSkillsResponse>>(new GetSkillsResponse([.. blocks.Loaded.Select(View)], skipped.Count == 0 ? null : skipped));
    }

    private static SkillBlockView View(LoadedSkillBlock block)
    {
        var manifest = block.Manifest;
        var descriptor = block.Descriptor;
        return new SkillBlockView(
            manifest.Name, block.Skill, descriptor.Id.Value, descriptor.Version, manifest.Sentence,
            [.. manifest.Inputs.Select(i => new SkillInputView(i.Key, i.Type == SkillValueType.Path ? "path" : "text", i.Label, i.Required))],
            [.. descriptor.Outputs.Select(o => o.Fact.Value)], [.. descriptor.Outcomes.Select(o => o.Value)],
            manifest.Pushes, SkillSha(block.Path), block.Path);
    }

    /// <summary>The SKILL.md sha256 next to the block.yaml; empty when the file is gone since the load.</summary>
    private static string SkillSha(string blockYaml)
    {
        var skillFile = Path.Combine(Path.GetDirectoryName(blockYaml) ?? string.Empty, "SKILL.md");
        return File.Exists(skillFile) ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(skillFile))) : string.Empty;
    }
}
