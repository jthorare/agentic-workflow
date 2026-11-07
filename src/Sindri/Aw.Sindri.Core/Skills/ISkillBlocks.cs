namespace Aw.Sindri.Core.Skills;

/// <summary>The skill blocks serve loaded at start (skill flows spec §3.2), and the way to read one other folder for a trial ('sindri skill run --skills &lt;dir&gt;').</summary>
public interface ISkillBlocks
{
    /// <summary>The skills folder serve resolved at start (it may not exist).</summary>
    public string Folder { get; }

    /// <summary>The blocks that loaded from the skills folder serve resolved once at start, sorted by id.</summary>
    public IReadOnlyList<LoadedSkillBlock> Loaded { get; }

    /// <summary>The blocks that did not load, each with the reason ('skills:block &lt;id&gt; is not loaded: &lt;reason&gt;'); one 'no skills folder' entry when there is none.</summary>
    public IReadOnlyList<SkippedSkillBlock> Skipped { get; }

    /// <summary>Reads one folder for a single trial and changes nothing else: <see cref="Loaded"/> and <see cref="Skipped"/> stay as they were.</summary>
    /// <param name="folder">A skills folder: the one that holds '&lt;skill&gt;/block.yaml'.</param>
    /// <returns>What that folder holds.</returns>
    public SkillBlocks LoadFolder(string folder);
}
