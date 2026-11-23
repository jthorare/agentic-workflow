using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="IRuleCatalogPort"/> over seeded rules, held to <see cref="RuleCatalogPortContract"/>. Every seed is an 'engineering' rule with status 'active'.</summary>
/// <param name="rules">The rules.</param>
public sealed class FakeRuleCatalog(IReadOnlyList<RuleCatalogSeed> rules) : IRuleCatalogPort
{
    /// <inheritdoc />
    public ValueTask<Result<RuleCatalogListing>> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<RuleListing> listed = [.. rules.Select(static r => new RuleListing(r.Id, "engineering", r.Section, r.Text, "active", r.Checks)).OrderBy(static r => r.Id, StringComparer.Ordinal)];
        return ValueTask.FromResult(Results.Success(new RuleCatalogListing(listed, Skipped: 0)));
    }
}
