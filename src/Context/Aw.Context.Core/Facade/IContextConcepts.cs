using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
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
}
