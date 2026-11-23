using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="ISkillCatalogPort"/> over seeded skills, held to <see cref="SkillCatalogPortContract"/>.</summary>
/// <param name="skills">The skills.</param>
public sealed class FakeSkillCatalog(IReadOnlyList<SkillCatalogSeed> skills) : ISkillCatalogPort
{
    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<SkillListing>>> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<SkillListing> listed = [.. skills.Select(static s => new SkillListing(s.Name, s.Description ?? string.Empty)).OrderBy(static s => s.Name, StringComparer.Ordinal)];
        return ValueTask.FromResult(Results.Success(listed));
    }
}
