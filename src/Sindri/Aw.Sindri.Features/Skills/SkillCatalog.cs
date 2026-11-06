using Aw.Sindri.Core.Skills;

namespace Aw.Sindri.Features.Skills;

/// <summary>The skill blocks serve loaded: read once, on first use after start, from the one <see cref="SkillsFolder"/>; a trial of an edited folder loads beside it and leaves this set alone (skill flows spec §3.2).</summary>
internal sealed class SkillCatalog(SkillsFolder folder, Func<string, SkillBlocks> loader) : ISkillCatalog
{
    private readonly Lazy<SkillBlocks> _loaded = new(() => loader(folder.Path), LazyThreadSafetyMode.ExecutionAndPublication);

    public string Folder => folder.Path;

    public SkillBlocks Blocks => _loaded.Value;

    public SkillBlocks LoadFolder(string directory) => loader(directory);
}
