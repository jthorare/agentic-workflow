using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The context engine's text shape honours the text-shape contract, over a started host whose profile maps the fixture repo and whose map is imported.</summary>
[Lane(TestLane.Integration)]
public sealed class ContextTextShapeContractTests : ScopingTextShapeContract, IAsyncLifetime
{
    private readonly FixtureRepo _fixture = FixtureRepo.Build();
    private GoldenHost? _host;
    private AsyncServiceScope _scope;

    protected override RepoName Repo => RepoName.From(Context.Testing.Map.FixtureMap.Repo);

    public async ValueTask InitializeAsync()
    {
        _host = await ContextHosts.Approved(_fixture, sessions: true, TestContext.Current.CancellationToken,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: Context.Testing.Map.FixtureMap.Repo);
        await ContextMapApiTests.Import(_host);
        _scope = _host.App.Services.CreateAsyncScope();
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        _fixture.Dispose();
    }

    /// <summary>A text that uses a node's whole label anchors on that node: the engine resolves it, so it is not left out for being no suggestion.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_text_naming_a_nodes_label_gets_that_node_as_an_anchor()
    {
        var anchors = await Create().Anchors(Repo, "How does the Tab manager restore a layout?", TestContext.Current.CancellationToken);

        Assert.True(anchors.IsSuccess);
        Assert.Contains("feature:tabs.tab-manager", anchors.Value, StringComparer.Ordinal);
    }

    protected override IScopingTextShape Create() => _scope.ServiceProvider.GetRequiredService<IScopingTextShape>();
}
