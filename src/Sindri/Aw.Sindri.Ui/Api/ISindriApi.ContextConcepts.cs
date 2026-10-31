using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>The app's reads of the API (<see cref="ISindriApi"/>, continued): the product's concepts, the first layer of the context map.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/context/concepts: the product's core concepts with their sizes. A server without the route answers 404, which the page reads as "no concepts".</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">The repo and the page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The concepts, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ContextConceptsView>> ContextConcepts(Uri api, ConceptsQuery query, CancellationToken cancellationToken);

    /// <summary>GET /sindri/context/pages: the product's pages with their actions, one slice at a time. A server without the route answers 404.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">The repo, the concept and the slice.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pages, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<PagesView>> ContextPages(Uri api, PagesQuery query, CancellationToken cancellationToken);

    /// <summary>GET /sindri/context/concepts/glossary: each concept's top terms. A server without the route answers 404, which the page reads as "no terms".</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">The repo and the map version.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The glossary, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ConceptGlossaryView>> ContextConceptGlossary(Uri api, ConceptGlossaryQuery query, CancellationToken cancellationToken);

    /// <summary>GET /sindri/context/pins/suggest: a concept's terms with the records each would newly link, the features a search finds, or the concept each line of a pasted list matches (sent 25 lines at a time, the answers joined). A server without the route answers 404.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">The repo and the one thing to suggest.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The suggestions, the API's refusal (SND-CONTEXT-179 for a request it can't read), or SND-UI-002/003.</returns>
    public Task<Result<PinSuggestView>> ContextPinSuggest(Uri api, PinSuggestQuery query, CancellationToken cancellationToken);
}
