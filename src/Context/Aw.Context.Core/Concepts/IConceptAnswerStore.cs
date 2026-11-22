using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// The concepts and glossary answers built for a map version, stored under a key made of everything the answer reads (issue 3676): a read answers from the stored
/// row instead of loading and walking the whole map. Publish fills the rows; a read whose key has none builds the answer once and keeps it.
/// </summary>
public interface IConceptAnswerStore
{
    /// <summary>The stored map that <paramref name="read"/> names, read without loading its graph: its version, the override layer and the intent layer's counter.</summary>
    /// <param name="read">Which map.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The basis, null when the store holds no such version or can't tell (a store from before the answers table: every read builds), or the store's error.</returns>
    public ValueTask<Result<ConceptAnswerBasis?>> Basis(MapRead read, CancellationToken cancellationToken);

    /// <summary>The concepts answer kept under <paramref name="key"/>.</summary>
    /// <param name="key">The answer's key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The answer, null when none is kept, or the store's error.</returns>
    public ValueTask<Result<ConceptsView?>> Concepts(string key, CancellationToken cancellationToken);

    /// <summary>The glossary answer kept under <paramref name="key"/>.</summary>
    /// <param name="key">The answer's key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The answer, null when none is kept, or the store's error.</returns>
    public ValueTask<Result<ConceptGlossaryView?>> Glossary(string key, CancellationToken cancellationToken);

    /// <summary>Keeps a concepts answer, and drops rows that can't be asked for again (of versions the store no longer holds) and the oldest past the store's bound.</summary>
    /// <param name="key">The answer's key.</param>
    /// <param name="basis">The basis the answer was built on.</param>
    /// <param name="view">The answer.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or the store's error.</returns>
    public ValueTask<Result<Unit>> Keep(string key, ConceptAnswerBasis basis, ConceptsView view, CancellationToken cancellationToken);

    /// <summary>Keeps a glossary answer, dropping stale rows like the concepts' <see cref="Keep(string, ConceptAnswerBasis, ConceptsView, CancellationToken)"/>.</summary>
    /// <param name="key">The answer's key.</param>
    /// <param name="basis">The basis the answer was built on.</param>
    /// <param name="view">The answer.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or the store's error.</returns>
    public ValueTask<Result<Unit>> Keep(string key, ConceptAnswerBasis basis, ConceptGlossaryView view, CancellationToken cancellationToken);
}
