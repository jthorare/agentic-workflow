using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>The concept glossary route (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<ConceptGlossaryView>> ContextConceptGlossary(Uri api, ConceptGlossaryQuery query, CancellationToken cancellationToken) =>
        Get<ConceptGlossaryView>(api, ContextRoutes.ConceptGlossary(query), cancellationToken);
}
