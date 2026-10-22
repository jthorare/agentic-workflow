using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Features;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.CodeIndex.Testing;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Aw.CodeIndex.Tests.Build;

/// <summary>The build slice and its facade over the fakes of the ports; a test arranges the commits and the parser, then builds and reads.</summary>
internal sealed class BuildHarness
{
    public static readonly RepoName Name = RepoName.From("api");
    public static readonly CommitSha First = CommitSha.From("1111111111111111111111111111111111111111");
    public static readonly CommitSha Second = CommitSha.From("2222222222222222222222222222222222222222");

    public FakeIndexStore Store { get; } = new();

    public FakeRepoSnapshot Snapshot { get; } = new();

    public FakeSymbolParser Parser { get; init; } = new();

    public FakeParseQuarantine Quarantine { get; } = new();

    public FakeHeavyJobLock Heavy { get; init; } = new();

    public FakeRepoHeads Heads { get; } = new();

    public FakeBodyScrubber Scrubber { get; init; } = new();

    public FakeGraphRunner? Graph { get; init; }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    public FakeEmbedder Embedder { get; init; } = new();

    public CodeIndexSettings Settings { get; init; } = CodeIndexSettings.Default;

    public CodeIndexRepo Repo => new(Name, "/work/api", Settings);

    public CodeIndexScope Scope => new([Repo], new CodeIndexCaller(CallerKind.Service, Repo: null));

    public BuildCodeIndexService Service => new(Heads, Snapshot, new StructureWriter(Snapshot, [Parser], Quarantine, Scrubber), new BuildLayers(new EmbeddingsLayer(Embedder, Clock), new GraphLayer(Snapshot, Clock, Graph)), Store, Heavy, Clock);

    public CodeIndexFacade Facade => new(Service, Store, Embedder, Graph);

    public async Task<BuildReport> Build(CommitSha commit, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = await Service.Handle(new BuildCodeIndexRequest(Repo, commit, progress ?? new Progress<string>()), cancellationToken);
        return result.Value;
    }

    public async Task<Result<BuildReport>> TryBuild(CommitSha commit, CancellationToken cancellationToken = default)
        => await Service.Handle(new BuildCodeIndexRequest(Repo, commit, new Progress<string>()), cancellationToken);

    public async Task<IndexHeader?> Header() => (await Store.ReadHeader(Name, CancellationToken.None)).Value;
}
