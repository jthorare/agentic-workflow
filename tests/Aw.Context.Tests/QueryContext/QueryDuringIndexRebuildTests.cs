using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Features.QueryContext;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Linking;
using Aw.Context.Testing.Records;
using Aw.Context.Testing.Vectors;
using Aw.Context.Tests.Map;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.QueryContext;

/// <summary>
/// context_query while the repo's code index builds (issue 2945): the symbol shape answers what it is (a file path field, or "rebuilding"
/// with the job's progress), and a query whose map read never returns answers a typed error within <c xml:lang="en">context.queryBound</c>.
/// </summary>
public sealed class QueryDuringIndexRebuildTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task A_symbol_that_is_not_a_file_path_is_refused_without_the_filemap_fix()
    {
        using var world = new MapWorld();
        _ = await world.ImportFixture();

        var result = await world.Engine.Query(new ContextQuery(Text: null, Symbol: "createCoreOnCallUnit"), MapWorld.Human, MapWorld.None);

        Assert.True(result.IsError);
        Assert.NotEqual(ContextErrors.UnknownAnchor.Code, result.Error.Code, StringComparer.Ordinal);
        Assert.DoesNotContain("filemap", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(ContextErrors.SymbolNotAPath.Code, result.Error.Code);
        Assert.Contains("file path", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_symbol_query_while_the_code_index_builds_answers_index_rebuilding_with_progress()
    {
        var building = new BuildingCodeIndex("extracting 6,700 of 14,016 files");
        using var world = new MapWorld(configure: services => services.AddSingleton<ICodeIndexBuildState>(building));
        _ = await world.ImportFixture();

        var result = await world.Engine.Query(new ContextQuery(Text: null, Symbol: "src/nope.ts"), MapWorld.Human, MapWorld.None);

        Assert.True(result.IsError);
        Assert.NotEqual(ContextErrors.UnknownAnchor.Code, result.Error.Code, StringComparer.Ordinal);
        Assert.Equal(ContextErrors.CodeIndexRebuilding.Code, result.Error.Code);
        Assert.Contains("rebuilding", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("6,700 of 14,016", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_free_text_query_whose_map_read_never_returns_answers_within_the_bound()
    {
        using var release = new CancellationTokenSource();
        var recordStore = new FakeRecordStore();
        var pipeline = new QueryPipeline(new FakeScrubberFactory(), new SequentialIdGenerator(), TimeProvider.System, new FakeIntentStore(), new FakeModelPort(),
            new RecordSearch(new FakeLexicalLeg(), recordStore, new FakeQueryEmbedder(), new FakeVectorMatrixCache(recordStore), new FakeVectorScan()));
        var request = new QueryContextRequest(new ContextQuery(Text: "how do split panes resize", Symbol: null), Engines.Human("""{"queryBound":"200ms"}"""));

        var running = pipeline.Run(request, new PipelineInputs(new HangingMap(release.Token), new FixedSourceCatalog()), CancellationToken.None).AsTask();
        try
        {
            var first = await Task.WhenAny(running, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

            Assert.Same(running, first);   // the bound, not the 5 s guard, ends the query
            var result = await running;
            Assert.True(result.IsError);
            Assert.Equal(ContextErrors.QueryTimedOut.Code, result.Error.Code);
        }
        finally
        {
            await release.CancelAsync();
            _ = await Task.WhenAny(running);   // let a hung read end; its outcome is not the test's
        }
    }

    /// <summary>A code index that reports a build with a fixed progress line for every repo.</summary>
    private sealed class BuildingCodeIndex(string progress) : ICodeIndexBuildState
    {
        public ValueTask<CodeIndexBuildStatus> Read(ContextRepoName repo, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new CodeIndexBuildStatus(building: true, progress));
    }

    /// <summary>A map read that ignores the request token and waits until the test releases it (a store call that holds forever).</summary>
    private sealed class HangingMap(CancellationToken release) : IMapProvider
    {
        public async ValueTask<Result<VisibleMap>> Read(ContextScope scope, MapRead read, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, release).ConfigureAwait(false);
            return ContextErrors.NoMap.With("released");
        }
    }
}
