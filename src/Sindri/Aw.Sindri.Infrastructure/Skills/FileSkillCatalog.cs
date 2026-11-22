using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Skills;

/// <summary>The skill catalog on disk (issue 3787, piece 4): one listing per '&lt;skill&gt;/SKILL.md' under the skills folder, with its front matter 'description'.</summary>
/// <param name="skillsFolder">The skills folder (<see cref="SkillFolders.Resolve"/>).</param>
internal sealed class FileSkillCatalog(string skillsFolder) : ISkillCatalogPort
{
    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<SkillListing>>> List(CancellationToken cancellationToken) => throw new NotSupportedException($"red: {skillsFolder}");
}
