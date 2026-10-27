using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>The concepts route (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<ContextConceptsView>> ContextConcepts(Uri api, ConceptsQuery query, CancellationToken cancellationToken) =>
        Get<ContextConceptsView>(api, ContextRoutes.Concepts(query), cancellationToken);
}
