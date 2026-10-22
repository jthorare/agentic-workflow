using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.CodeIndex.Features;

/// <summary>
/// The code index's facade over the index files (spec §7): the build and the reads that need only the structure layer.
/// A repo outside the scope, or one with no index, answers the same SND-CODEIDX-013 (Callers and Callees of a ref that names no symbol: 011).
/// Similar answers from the clone rows; Neighbors embeds its text with the model the index was built with and answers from the stored vectors.
/// </summary>
/// <param name="build">The build slice.</param>
/// <param name="store">The index files.</param>
/// <param name="embedder">Embeds a Neighbors query with the model the index was embedded with.</param>
public sealed partial class CodeIndexFacade(IService<BuildCodeIndexRequest, BuildReport> build, IIndexStore store, IEmbedder embedder) : ICodeIndex
{
    /// <inheritdoc />
    public async ValueTask<Result<BuildReport>> Build(CodeIndexScope scope, RepoName repo, CommitSha commit, IProgress<string> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var registered = scope.Repos.FirstOrDefault(r => r.Name == repo);
        return registered is null
            ? CodeIndexErrors.RepoNotFound.With("no such repo is registered")
            : await build.Handle(new BuildCodeIndexRequest(registered, commit, progress), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<StatusAnswer>> Status(CodeIndexScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var rows = new List<RepoStatus>(scope.Repos.Count);
        foreach (var repo in scope.Repos)
        {
            var header = await store.ReadHeader(repo.Name, cancellationToken).ConfigureAwait(false);
            if (header.IsError && !IsDamaged(header.Error))
            {
                return header.Error;
            }

            rows.Add(header.IsError ? new RepoStatus(repo.Name, IndexState.Damaged, Commit: null, BuiltAt: null, [], Counts: null)
                : header.Value is { } live ? new RepoStatus(repo.Name, IndexState.Ok, live.Meta.Commit, live.Meta.BuiltAt, [.. live.Layers.Select(Status)], live.Meta.Counts)
                : new RepoStatus(repo.Name, IndexState.Missing, Commit: null, BuiltAt: null, [], Counts: null));
        }

        return new StatusAnswer(rows);
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<RecordedHead>>> Heads(CodeIndexScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var heads = new List<RecordedHead>(scope.Repos.Count);
        foreach (var repo in scope.Repos)
        {
            var header = await store.ReadHeader(repo.Name, cancellationToken).ConfigureAwait(false);
            if (header.IsError && !IsDamaged(header.Error))
            {
                return header.Error;
            }

            heads.Add(new RecordedHead(repo.Name, header.IsError ? null : header.Value?.Meta.Commit));
        }

        return heads;
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<LayerProbe>>> ProbeLayers(CodeIndexScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        // No later layer can be built yet, so nothing is buildable; the graph and embeddings units fill this in.
        return ValueTask.FromResult<Result<IReadOnlyList<LayerProbe>>>(scope.Repos.Select(r => new LayerProbe(r.Name, [])).ToList());
    }

    /// <inheritdoc />
    public async ValueTask<Result<FindSymbolsAnswer>> FindSymbols(CodeIndexScope scope, SymbolQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);
        if (!IsValid(query))
        {
            return CodeIndexErrors.QueryInvalid.With("the query is empty, too long, or its limit is outside 1 to 200");
        }

        var found = new Found();
        foreach (var repo in scope.Repos.Where(r => query.Repo is null || r.Name == query.Repo))
        {
            var searched = await SearchOne(repo, query, found, cancellationToken).ConfigureAwait(false);
            if (searched.IsError)
            {
                return searched.Error;
            }
        }

        if (found.Stamp is null)
        {
            return found.Failure ?? CodeIndexErrors.IndexNotBuilt.With("no index for this repo yet");
        }

        var ordered = found.Hits.OrderBy(h => h.File, StringComparer.Ordinal).ThenBy(h => h.StartLine).ToList();
        return new FindSymbolsAnswer(found.Stamp, [.. ordered.Take(query.Limit)], found.Truncated || ordered.Count > query.Limit);
    }

    private static bool IsValid(SymbolQuery query)
        => !string.IsNullOrWhiteSpace(query.Text) && query.Text.Length <= InputLimits.QueryCharacters && query.Limit is >= 1 and <= CodeIndexRules.MaxSymbolHits;

    private async ValueTask<Result<Unit>> SearchOne(CodeIndexRepo repo, SymbolQuery query, Found found, CancellationToken cancellationToken)
    {
        var opened = await store.OpenRead(repo.Name, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            found.Failure ??= opened.Error;
            return Unit.Value;
        }

        await using (opened.Value.ConfigureAwait(false))
        {
            var page = await opened.Value.FindSymbols(query, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            found.Stamp ??= Stamp(repo.Name, opened.Value.Header, LayerName.Structure);
            found.Hits.AddRange(page.Value.Hits);
            found.Truncated |= page.Value.Truncated;
            return Unit.Value;
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<FileSymbolsAnswer>> FileSymbols(CodeIndexScope scope, RepoName repo, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count > CodeIndexRules.MaxFileSymbolPaths)
        {
            return CodeIndexErrors.QueryInvalid.With("at most 200 paths per call");
        }

        var opened = await Open(scope, repo, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        await using (opened.Value.ConfigureAwait(false))
        {
            var files = await opened.Value.FileSymbols(paths, cancellationToken).ConfigureAwait(false);
            return files.IsError ? files.Error : new FileSymbolsAnswer(Stamp(repo, opened.Value.Header, LayerName.Structure), files.Value);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<CallsAnswer>> Callers(CodeIndexScope scope, SymbolRef symbol, CallDepth depth, bool includeImplementations, CancellationToken cancellationToken)
        => Walk(scope, new WalkQuery(symbol, CallDirection.Callers, depth, includeImplementations), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CallsAnswer>> Callees(CodeIndexScope scope, SymbolRef symbol, CallDepth depth, CancellationToken cancellationToken)
        => Walk(scope, new WalkQuery(symbol, CallDirection.Callees, depth, IncludeImplementations: false), cancellationToken);

    private async ValueTask<Result<CallsAnswer>> Walk(CodeIndexScope scope, WalkQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var symbol = query.Symbol;
        if (RepoOf(symbol) is not { } repo)
        {
            return CodeIndexErrors.SymbolNotFound.With("the ref names no repo");
        }

        var opened = await Open(scope, repo, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        await using (opened.Value.ConfigureAwait(false))
        {
            var walk = await opened.Value.Walk(symbol, query.Direction, query.Depth, query.IncludeImplementations, cancellationToken).ConfigureAwait(false);
            if (walk.IsError)
            {
                return walk.Error;
            }

            var layer = walk.Value.Coverage == Coverage.Full ? LayerName.Structure : LayerName.Graph;
            return new CallsAnswer(Stamp(repo, opened.Value.Header, layer), symbol, walk.Value.Edges, walk.Value.UnresolvedCount, walk.Value.Truncated, walk.Value.Coverage);
        }
    }

    private async ValueTask<Result<IIndexReader>> Open(CodeIndexScope scope, RepoName repo, CancellationToken cancellationToken)
        => scope.Repos.Any(r => r.Name == repo)
            ? await store.OpenRead(repo, cancellationToken).ConfigureAwait(false)
            : CodeIndexErrors.IndexNotBuilt.With("no index for this repo yet");

    private static RepoName? RepoOf(SymbolRef symbol)
    {
        var text = symbol.Value;
        return RepoName.TryFrom(text[..text.IndexOf(':', StringComparison.Ordinal)], out var repo) ? repo : null;
    }

    private static bool IsDamaged(Error error) => string.Equals(error.Code, CodeIndexErrors.IndexDamaged.Code, StringComparison.Ordinal);

    private static AnswerStamp Stamp(RepoName repo, IndexHeader header, LayerName layer)
        => new(repo, header.Meta.Commit, header.Meta.BuiltAt, IndexState.Ok, header.Layers.Where(l => l.Layer == layer).Select(Status).FirstOrDefault() ?? new LayerStatus(layer, LayerState.Unavailable, Code: null, "not built"));

    private static LayerStatus Status(LayerRecord layer) => new(layer.Layer, layer.State, layer.Code, layer.Detail);

    private sealed record WalkQuery(SymbolRef Symbol, CallDirection Direction, CallDepth Depth, bool IncludeImplementations);

    private sealed class Found
    {
        public AnswerStamp? Stamp { get; set; }

        public Error? Failure { get; set; }

        public List<SymbolHit> Hits { get; } = [];

        public bool Truncated { get; set; }
    }

    private static class InputLimits
    {
        public const int QueryCharacters = 512;
    }
}
