using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// The concepts and glossary answers, read from <see cref="IConceptAnswerStore"/> when one is kept under the read's key (issue 3676), else built from the map by
/// <see cref="ConceptBuilder"/> and <see cref="ConceptGlossaryBuilder"/> and kept. A concepts search ('q') is built every time: each typed text would be a row of its own.
/// The store is a cache: when it can't answer or can't keep, the read builds the answer as it did before.
/// </summary>
/// <param name="reader">Reads the map the caller may see.</param>
/// <param name="store">The kept answers.</param>
public sealed class ConceptAnswers(ConceptReader reader, IConceptAnswerStore store)
{
    /// <summary>One page of the concepts of the map the caller may read, as kept: no row has a last change yet (the read lays it over with <see cref="KeptConcepts.With"/>).</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="query">Which map, whose repo, the filter and the page.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The kept answer, or the read's refusal.</returns>
    public async ValueTask<Result<KeptConcepts>> Concepts(ContextScope scope, ConceptsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);
        var basis = query.Q is null ? await Basis(scope, query.Read, cancellationToken).ConfigureAwait(false) : null;
        var key = basis is null ? null : ConceptAnswerKey.Concepts(basis, query, scope);
        if (key is not null && await store.Concepts(key, cancellationToken).ConfigureAwait(false) is { IsSuccess: true, Value: { } kept })
        {
            return kept;
        }

        var read = await reader.Read(scope, query.Read, query.Repo, cancellationToken).ConfigureAwait(false);
        var built = read.IsError ? read.Error : Kept(read.Value, query);
        if (built.IsSuccess && basis is not null && key is not null)
        {
            _ = await store.Keep(key, basis, built.Value, cancellationToken).ConfigureAwait(false);   // a row not kept is built again next time
        }

        return built;
    }

    /// <summary>The glossary of the map the caller may read.</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="query">Which map and whose repo.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The view, or the read's refusal.</returns>
    public async ValueTask<Result<ConceptGlossaryView>> Glossary(ContextScope scope, ConceptGlossaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);
        var basis = await Basis(scope, query.Read, cancellationToken).ConfigureAwait(false);
        var key = basis is null ? null : ConceptAnswerKey.Glossary(basis, query, scope);
        if (key is not null && await store.Glossary(key, cancellationToken).ConfigureAwait(false) is { IsSuccess: true, Value: { } kept })
        {
            return kept;
        }

        var read = await reader.Read(scope, query.Read, query.Repo, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var built = ConceptGlossaryBuilder.Build(read.Value.Basis, read.Value.Graph, read.Value.Text, read.Value.Pins);
        if (basis is not null && key is not null)
        {
            _ = await store.Keep(key, basis, built, cancellationToken).ConfigureAwait(false);
        }

        return built;
    }

    /// <summary>
    /// Builds and keeps the answers the app reads first, right after a map version became current: the first page of the concepts and the glossary, for the owner in the app
    /// (a human over HTTP), of the whole map and, when the profile has several repos, of each. It never fails the publish: a read that is refused or a store that can't keep
    /// leaves the read to build the answer itself.
    /// </summary>
    /// <param name="scope">The publisher's scope: the profile's context section and repos.</param>
    /// <param name="cancellationToken">Cancels the fill (the version stays published).</param>
    /// <returns>A task that completes when the fill ends.</returns>
    public async ValueTask Fill(ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var app = scope with { Caller = new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Http) };
        IReadOnlyList<string?> repos = scope.Repos.Count > 1 ? [null, .. scope.Repos.Select(r => r.Name.Value)] : [null];
        try
        {
            foreach (var repo in repos)
            {
                _ = await Concepts(app, new ConceptsQuery(MapRead.Current, new ConceptPage(), repo), cancellationToken).ConfigureAwait(false);
                _ = await Glossary(app, new ConceptGlossaryQuery(MapRead.Current, repo), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // the version is published; the reads build what the fill didn't keep
        }
    }

    // The map-derived answer and its rows' code paths; the read's basis carries no change times, so no kept row holds one.
    private static Result<KeptConcepts> Kept(ConceptRead read, ConceptsQuery query)
    {
        var view = ConceptBuilder.Build(read.Basis, query, read.Graph, read.Text, read.Pins);
        return view.IsError ? view.Error : new KeptConcepts(view.Value, ConceptBuilder.CodePathsOf(view.Value.Concepts.Select(c => c.Id), read.Graph));
    }

    // The stored map the read names, when the caller may read it at all: a refused caller is refused by the read, never answered from a kept row.
    private async ValueTask<ConceptAnswerBasis?> Basis(ContextScope scope, MapRead read, CancellationToken cancellationToken) =>
        MapAccess.Gate(scope) is null && await store.Basis(read, cancellationToken).ConfigureAwait(false) is { IsSuccess: true, Value: { } basis } ? basis : null;
}
