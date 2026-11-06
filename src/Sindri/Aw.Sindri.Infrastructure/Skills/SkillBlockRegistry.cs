using Aw.Sindri.Core.Skills;

namespace Aw.Sindri.Infrastructure.Skills;

/// <summary>The skills folder read once, when first asked (serve start), and the trial read of another folder.</summary>
/// <param name="folder">The skills folder serve resolved.</param>
internal sealed class SkillBlockRegistry(string folder) : ISkillBlocks
{
    private readonly Lazy<SkillBlocks> _current = new(() => SkillManifestLoader.Load(folder));

    /// <inheritdoc/>
    public IReadOnlyList<LoadedSkillBlock> Loaded => _current.Value.Loaded;

    /// <inheritdoc/>
    public IReadOnlyList<SkippedSkillBlock> Skipped => _current.Value.Skipped;

    /// <inheritdoc/>
    public SkillBlocks LoadFolder(string trialFolder) => SkillManifestLoader.Load(trialFolder);
}
