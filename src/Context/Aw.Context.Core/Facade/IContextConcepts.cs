using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Pages;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>The concept layer's reads: the product's concepts, the top band of the map, and one concept's members, a page at a time. A session sees only its repo's scope. IContextEngine extends it.</summary>
public interface IContextConcepts
{
    /// <summary>One page of the concepts of the map <paramref name="query"/> names, with their sizes, the links between them and the reserved 'other' concept.</summary>
    /// <param name="query">Which map, whose repo, the filter and the page.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view (state 'empty' when the map has no concept yet), or SND-CONTEXT-001/003/004/006/020/030/044/045/120.</returns>
    public ValueTask<Result<ConceptsView>> Concepts(ConceptsQuery query, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>One page of one concept's members: its features, records and decisions.</summary>
    /// <param name="query">The concept, which map, whose repo and the page.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view, or SND-CONTEXT-003 for a concept the map lacks, else as <see cref="Concepts"/>.</returns>
    public ValueTask<Result<ConceptMembersView>> ConceptMembers(ConceptMembersQuery query, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>The glossary of the map <paramref name="query"/> names: the words that characterize each concept, and for each word the concepts it characterizes.</summary>
    /// <param name="query">Which map and whose repo.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view (state 'empty' when no concept carries a term), else as <see cref="Concepts"/>.</returns>
    public ValueTask<Result<ConceptGlossaryView>> ConceptGlossary(ConceptGlossaryQuery query, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>The product's pages with their actions and the concepts each expresses: the owner's named pages and the map's suggested ones.</summary>
    /// <param name="query">The filter, the slice and which map.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view, or SND-CONTEXT-257 for a page the map lacks, SND-CONTEXT-258 for a concept or repo it lacks, else as <see cref="Concepts"/>.</returns>
    public ValueTask<Result<PagesView>> Pages(PagesQuery query, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Checks the approved profile's pages against the map (the doctor row 'context:pages').</summary>
    /// <param name="scope">The approved scope, or null when none is approved.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The health row.</returns>
    public ValueTask<Result<MapHealth>> CheckPages(ContextScope? scope, CancellationToken cancellationToken);
}
