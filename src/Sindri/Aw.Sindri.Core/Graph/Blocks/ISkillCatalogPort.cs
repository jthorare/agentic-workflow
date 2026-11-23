using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where 'skill_catalog' reads the toolkit's skills (issue 3787, piece 4). Read-only: the Infrastructure adapter reads each skill folder's SKILL.md.</summary>
public interface ISkillCatalogPort
{
    /// <summary>Every skill, by folder name, sorted by name; a skill whose SKILL.md has no description comes with an empty one.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The skills; none when the skills folder is missing.</returns>
    public ValueTask<Result<IReadOnlyList<SkillListing>>> List(CancellationToken cancellationToken);
}
