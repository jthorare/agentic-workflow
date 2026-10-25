using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Testing;

/// <summary>
/// A scripted <see cref="ICodeIndex"/> for the surfaces that read it (Sindri's CLI and HTTP routes): repos with a status, symbols and call edges set up by the test. Each edge carries its own coverage; the answer's comes from <see cref="CallCoverage.Of"/> with the symbol's language read from its file extension (.cs full, else partial).
/// It keeps the facade's scope rule: a repo outside the call's <see cref="CodeIndexScope"/>, or one with no index, answers the same SND-CODEIDX-013 (SND-CODEIDX-011 for a
/// ref that names no symbol), and <see cref="Status"/> lists only the scope's repos. Build, FileSymbols, Similar and Neighbors answer 013 (Embed is scripted by <see cref="WithEmbeddings"/>): no reader scripts them yet.
/// </summary>
public sealed class FakeCodeIndex : ICodeIndex
{
    private readonly Dictionary<RepoName, Repo> _repos = [];
    private readonly List<CodeIndexScope> _scopes = [];
    private readonly List<RepoName> _embeds = [];
    private readonly HashSet<RepoName> _buildable = [];

    /// <summary>The scopes the calls came with, in order (a test asserts a session's scope held only its repo).</summary>
    public IReadOnlyList<CodeIndexScope> Scopes => _scopes;

    /// <summary>Adds a repo with a readable index.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="commit">The commit the index describes.</param>
    /// <param name="symbols">Its symbols.</param>
    /// <returns>This fake.</returns>
    public FakeCodeIndex WithIndex(RepoName repo, CommitSha commit, params SymbolHit[] symbols)
    {
        _repos[repo] = new Repo(repo, commit, [.. symbols]);
        return this;
    }

    /// <summary>Scripts the edges one symbol answers for Callers or Callees.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="subject">The symbol asked about.</param>
    /// <param name="direction">Callers or callees.</param>
    /// <param name="unresolved">The unresolved count.</param>
    /// <param name="edges">The edges.</param>
    /// <returns>This fake.</returns>
    public FakeCodeIndex WithEdges(RepoName repo, SymbolRef subject, CallDirection direction, int unresolved, params CallEdge[] edges)
    {
        _repos[repo].Edges[(subject, direction)] = (unresolved, [.. edges]);
        return this;
    }

    /// <summary>Marks a repo's graph layer as unavailable (SND-CODEIDX-008): the structure still answers.</summary>
    /// <param name="repo">The repo.</param>
    /// <returns>This fake.</returns>
    public FakeCodeIndex WithGraphUnavailable(RepoName repo)
    {
        _repos[repo].GraphDown = true;
        return this;
    }

    /// <summary>Scripts a repo's embeddings layer: <see cref="Status"/> lists it, and <see cref="Embed"/> leaves it in <paramref name="afterEmbed"/> (or answers <see cref="EmbedError"/>).</summary>
    /// <param name="repo">The repo; it needs a readable index.</param>
    /// <param name="state">The layer's state now.</param>
    /// <param name="afterEmbed">The state an embed run leaves it in.</param>
    /// <returns>This fake.</returns>
    public FakeCodeIndex WithEmbeddings(RepoName repo, LayerState state, LayerState afterEmbed = LayerState.Ok)
    {
        _repos[repo].Embeddings = state;
        _repos[repo].AfterEmbed = afterEmbed;
        return this;
    }

    /// <summary>The repos <see cref="Embed"/> was called for, in order.</summary>
    public IReadOnlyList<RepoName> Embeds => _embeds;

    /// <summary>When set, <see cref="Embed"/> answers this error instead of embedding.</summary>
    public Error? EmbedError { get; set; }

    /// <summary>Makes <see cref="ProbeLayers"/> report the repo's graph layer as buildable now (the box was fixed).</summary>
    /// <param name="repo">The repo.</param>
    /// <returns>This fake.</returns>
    public FakeCodeIndex WithGraphBuildable(RepoName repo)
    {
        _ = _buildable.Add(repo);
        return this;
    }

    /// <summary>Marks a repo's index as damaged.</summary>
    /// <param name="repo">The repo.</param>
    /// <returns>This fake.</returns>
    public FakeCodeIndex WithDamaged(RepoName repo)
    {
        _repos[repo] = new Repo(repo, Commit: null, []) { Damaged = true };
        return this;
    }

    public ValueTask<Result<BuildReport>> Build(CodeIndexScope scope, RepoName repo, CommitSha commit, IProgress<string> progress, CancellationToken cancellationToken)
        => Fail<BuildReport>(scope, CodeIndexErrors.IndexNotBuilt);

    public ValueTask<Result<EmbedReport>> Embed(CodeIndexScope scope, RepoName repo, IProgress<string> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _scopes.Add(scope);
        _embeds.Add(repo);
        if (EmbedError is { } error)
        {
            return ValueTask.FromResult<Result<EmbedReport>>(error);
        }

        if (scope.Repos.All(r => r.Name != repo) || Readable(repo) is not { } index)
        {
            return Fail<EmbedReport>(CodeIndexErrors.IndexNotBuilt);
        }

        index.Embeddings = index.AfterEmbed;
        return Ok(new EmbedReport(repo, Embedded: 1, Embeddable: 1, Carried: 0, Made: 1, index.Embeddings ?? LayerState.Ok));
    }

    public ValueTask<Result<StatusAnswer>> Status(CodeIndexScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _scopes.Add(scope);
        return Ok(new StatusAnswer([.. scope.Repos.Select(r => StatusOf(r.Name))]));
    }

    public ValueTask<Result<IReadOnlyList<RecordedHead>>> Heads(CodeIndexScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _scopes.Add(scope);
        return Ok<IReadOnlyList<RecordedHead>>([.. scope.Repos.Select(r => new RecordedHead(r.Name, Readable(r.Name)?.Commit))]);
    }

    /// <summary>How many times <see cref="ProbeLayers"/> was called.</summary>
    public int ProbeCalls { get; private set; }

    public ValueTask<Result<IReadOnlyList<LayerProbe>>> ProbeLayers(CodeIndexScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ProbeCalls++;
        _scopes.Add(scope);
        return Ok<IReadOnlyList<LayerProbe>>([.. scope.Repos.Select(r => new LayerProbe(r.Name, _buildable.Contains(r.Name) ? [LayerName.Graph] : []))]);
    }

    public ValueTask<Result<FindSymbolsAnswer>> FindSymbols(CodeIndexScope scope, SymbolQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);
        _scopes.Add(scope);
        if (string.IsNullOrWhiteSpace(query.Text))
        {
            return Fail<FindSymbolsAnswer>(CodeIndexErrors.QueryInvalid);
        }

        var repos = scope.Repos.Where(r => query.Repo is null || r.Name == query.Repo).Select(r => Readable(r.Name)).OfType<Repo>().ToList();
        if (repos.Count == 0)
        {
            return Fail<FindSymbolsAnswer>(CodeIndexErrors.IndexNotBuilt);
        }

        var hits = repos.SelectMany(r => r.Symbols.Where(s => Matches(query, s))).OrderBy(static s => s.File, StringComparer.Ordinal).ThenBy(static s => s.StartLine).ToList();
        var limit = query.Limit;
        return Ok(new FindSymbolsAnswer(Stamp(repos[0]), [.. hits.Take(limit)], hits.Count > limit));
    }

    public ValueTask<Result<FileSymbolsAnswer>> FileSymbols(CodeIndexScope scope, RepoName repo, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(paths);
        _scopes.Add(scope);
        if (scope.Repos.All(r => r.Name != repo) || Readable(repo) is not { } index)
        {
            return Fail<FileSymbolsAnswer>(CodeIndexErrors.IndexNotBuilt);
        }

        return Ok(new FileSymbolsAnswer(Stamp(index), [.. paths.Select(p => new FileSymbolsEntry(p, [.. index.Symbols.Where(s => s.File == p).OrderBy(static s => s.StartLine)]))]));
    }

    public ValueTask<Result<CallsAnswer>> Callers(CodeIndexScope scope, SymbolRef symbol, CallDepth depth, bool includeImplementations, CancellationToken cancellationToken)
        => Calls(scope, symbol, CallDirection.Callers);

    public ValueTask<Result<CallsAnswer>> Callees(CodeIndexScope scope, SymbolRef symbol, CallDepth depth, CancellationToken cancellationToken)
        => Calls(scope, symbol, CallDirection.Callees);

    public ValueTask<Result<SimilarAnswer>> Similar(CodeIndexScope scope, SymbolRef symbol, CancellationToken cancellationToken)
        => Fail<SimilarAnswer>(scope, CodeIndexErrors.IndexNotBuilt);

    public ValueTask<Result<NeighborsAnswer>> Neighbors(CodeIndexScope scope, RepoName repo, string text, int k, CancellationToken cancellationToken)
        => Fail<NeighborsAnswer>(scope, CodeIndexErrors.IndexNotBuilt);

    private ValueTask<Result<CallsAnswer>> Calls(CodeIndexScope scope, SymbolRef symbol, CallDirection direction)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _scopes.Add(scope);
        var repo = RepoOf(symbol);
        if (repo is not { } name || scope.Repos.All(r => r.Name != name) || Readable(name) is not { } index)
        {
            return Fail<CallsAnswer>(CodeIndexErrors.IndexNotBuilt);
        }

        if (!index.Symbols.Exists(s => s.Ref.Equals(symbol)))
        {
            return Fail<CallsAnswer>(CodeIndexErrors.SymbolNotFound);
        }

        var (unresolved, edges) = index.Edges.TryGetValue((symbol, direction), out var scripted) ? scripted : (0, []);
        var subject = index.Symbols.First(s => s.Ref.Equals(symbol));
        var language = subject.File.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? Coverage.Full : Coverage.Partial;
        return Ok(new CallsAnswer(Stamp(index), symbol, edges, unresolved, Truncated: false, CallCoverage.Of(language, edges)));
    }

    private Repo? Readable(RepoName name) => _repos.TryGetValue(name, out var repo) && !repo.Damaged ? repo : null;

    private RepoStatus StatusOf(RepoName name)
        => !_repos.TryGetValue(name, out var repo) ? new RepoStatus(name, IndexState.Missing, Commit: null, BuiltAt: null, [], Counts: null)
            : repo.Damaged ? new RepoStatus(name, IndexState.Damaged, Commit: null, BuiltAt: null, [], Counts: null)
            : new RepoStatus(name, IndexState.Ok, repo.Commit, BuiltAt, LayersOf(repo), new IndexCounts(Files: 1, repo.Symbols.Count, Edges: repo.Edges.Count));

    private static List<LayerStatus> LayersOf(Repo repo)
    {
        var layers = new List<LayerStatus> { new(LayerName.Structure, LayerState.Ok, Code: null, "ok") };
        if (repo.GraphDown)
        {
            layers.Add(GraphOf(repo));
        }

        if (repo.Embeddings is { } state)
        {
            layers.Add(new LayerStatus(LayerName.Embeddings, state, Code: null, state.ToString().ToLowerInvariant()));
        }

        return layers;
    }

    private static DateTimeOffset BuiltAt { get; } = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static AnswerStamp Stamp(Repo repo) => new(repo.Name, repo.Commit!.Value, BuiltAt, IndexState.Ok, GraphOf(repo));

    private static LayerStatus GraphOf(Repo repo) => repo.GraphDown
        ? new LayerStatus(LayerName.Graph, LayerState.Unavailable, "SND-CODEIDX-008", "graphify is missing")
        : new LayerStatus(LayerName.Graph, LayerState.Ok, Code: null, "ok");

    private static RepoName? RepoOf(SymbolRef symbol)
    {
        var text = symbol.Value;
        return RepoName.TryFrom(text[..text.IndexOf(':', StringComparison.Ordinal)], out var name) ? name : null;
    }

    private static bool Matches(SymbolQuery query, SymbolHit symbol) => query.Match == SymbolMatch.Prefix
        ? symbol.Name.StartsWith(query.Text, StringComparison.Ordinal)
        : symbol.Name == query.Text || symbol.Ref.Value == query.Text || symbol.File + "#" + symbol.Name == query.Text || symbol.File == query.Text;

    private static ValueTask<Result<T>> Ok<T>(T value) => ValueTask.FromResult<Result<T>>(value);

    private static ValueTask<Result<T>> Fail<T>(ErrorDefinition error) => ValueTask.FromResult<Result<T>>(error.With(error.Title));

    private ValueTask<Result<T>> Fail<T>(CodeIndexScope scope, ErrorDefinition error)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _scopes.Add(scope);
        return Fail<T>(error);
    }

    private sealed record Repo(RepoName Name, CommitSha? Commit, List<SymbolHit> Symbols)
    {
        internal bool Damaged { get; init; }

        internal bool GraphDown { get; set; }

        internal LayerState? Embeddings { get; set; }

        internal LayerState AfterEmbed { get; set; } = LayerState.Ok;

        internal Dictionary<(SymbolRef, CallDirection), (int Unresolved, List<CallEdge> Edges)> Edges { get; } = [];
    }
}
