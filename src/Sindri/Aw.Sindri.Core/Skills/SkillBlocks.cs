namespace Aw.Sindri.Core.Skills;

/// <summary>What reading a skills folder found: the blocks that loaded and the ones that were skipped.</summary>
/// <param name="Loaded">The loaded blocks, sorted by id.</param>
/// <param name="Skipped">The skipped blocks.</param>
public sealed record SkillBlocks(IReadOnlyList<LoadedSkillBlock> Loaded, IReadOnlyList<SkippedSkillBlock> Skipped);
