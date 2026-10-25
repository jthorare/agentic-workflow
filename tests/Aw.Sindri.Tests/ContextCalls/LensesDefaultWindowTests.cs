using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>A lenses call that names no window (the UI, a script, the HTTP route) gets every item; the 100-item default belongs to the MCP tools.</summary>
public sealed class LensesDefaultWindowTests
{
    private static readonly NodeKey _node = NodeKey.From("feature:tabs.tab-manager");

    private static LensesView Lenses(int files)
    {
        var link = LinkId.From("edge:0123456789abcdef");
        var list = Enumerable.Range(0, files)
            .Select(i => new LensFile($"src/tabs/f{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}.ts", [_node], [], Provenance.Model, 0.74, Confirmed: false, Suggested: true, link, "f", NodeKind.Code, Trust.Untrusted))
            .ToList();
        return new LensesView(1, new LensAnchor(_node, "Tab manager", NodeKind.Feature, Trust.Trusted), "v13", Draft: false, 2, DateTimeOffset.UnixEpoch,
            new LensBands(
                new IntentBand([], [], new IntentCounts(0, 0, Orphaned: null)),
                new ProductBand([], [], [], new ProductCounts(0, 0), []),
                new CodeBand(list, LensSymbols.Unavailable, new CodeCounts(files, 0, files))),
            [], [],
            new LensCoverage(6, new CoverageIntent(4, 6), new CoverageProduct(3, 6), new CoverageCode(2, 6, "file", 1), []),
            new LensSearched([], "v13", 2, [], [], new SearchedBands(new BandSearched(Empty: false), new BandSearched(Empty: false), new BandSearched(Empty: false))),
            [], new Dictionary<string, Citation>(StringComparer.Ordinal));
    }

    private sealed class One(LensesView view) : IService<GetContextLensesRequest, LensesView>
    {
        public ValueTask<Result<LensesView>> Handle(GetContextLensesRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(view));
    }

    private sealed class Many : IService<GetContextLensesSetRequest, LensesSetView>
    {
        public ValueTask<Result<LensesSetView>> Handle(GetContextLensesSetRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static async Task<LensesView> Ask(int? limit, int? offset)
    {
        var service = new GetContextLensesAnswerService(new One(Lenses(150)), new Many());
        var answer = await service.Handle(new GetContextLensesAnswerRequest([_node.Value], Draft: false, Depth: null, MapVersion: null, Hops: null, limit, offset), TestContext.Current.CancellationToken);
        return answer.Value.One!;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_call_that_names_no_window_gets_all_150_files_and_no_page()
    {
        var view = await Ask(limit: null, offset: null);

        Assert.Equal(150, view.Bands.Code.Files.Count);
        Assert.Null(view.Page);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_named_limit_windows_the_files_and_says_there_is_more()
    {
        var view = await Ask(limit: 100, offset: 0);

        Assert.Equal(100, view.Bands.Code.Files.Count);
        Assert.True(view.Page!.More);
    }
}
