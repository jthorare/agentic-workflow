using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Egress by caller (retrieval spec §7): on every context read route a session reads a model's label inside the fence, a human reads it clean beside trust 'untrusted'.</summary>
public sealed class ContextRoutesFenceApiTests : IDisposable
{
    private const string Fenced = "<untrusted source=\\\"map\\\">Drafted B</untrusted>";   // as it sits inside a JSON string
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    public static TheoryData<string, string> Routes => new()
    {
        { "node", "/sindri/context/node?key=feature%3Ab" },
        { "neighbors", "/sindri/context/neighbors?key=feature%3Aa&depth=1" },
        { "path", "/sindri/context/path?from=feature%3Aa&to=feature%3Ab" },
        { "code", "/sindri/context/code?anchor=feature%3Ab" },
        { "subgraph", "/sindri/context/subgraph?nodes=feature%3Aa&depth=1" },
        { "whole", "/sindri/context/map/whole?detail=nodes&repo=aw-split-panes" },
        { "lenses", "/sindri/context/lenses?anchor=feature%3Aa" },
        { "history", "/sindri/context/history?anchor=feature%3Ab" },
    };

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Query_results_stay_fenced_for_a_session_and_a_human_alike()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await ImportedHost();
        const string Url = "/sindri/context/query?node=feature%3Aa";

        var session = await host.Http.Ask(Get(Url, [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)]));
        var human = await host.Http.Ask(Get(Url, Human(host.Token.Value)));

        Assert.Contains(Fenced, session.Body, StringComparison.Ordinal);
        Assert.Contains(Fenced, human.Body, StringComparison.Ordinal);
    }

    // why: a file map entry names its features by key only (FileFeature has no label), so there is no model text to fence on this route.
    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task The_file_map_lists_a_files_features_by_key_and_carries_no_label_for_either_caller()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await ImportedHost();
        const string Url = "/sindri/context/filemap?repo=aw-split-panes&path=src%2Flayout";

        var session = await host.Http.Ask(Get(Url, [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)]));
        var human = await host.Http.Ask(Get(Url, Human(host.Token.Value)));

        Assert.True(session.Status == System.Net.HttpStatusCode.OK && human.Status == System.Net.HttpStatusCode.OK, $"{session.Status} {session.Body} / {human.Status} {human.Body}");
        Assert.All([session.Body, human.Body], body =>
        {
            Assert.Contains("\"key\":\"feature:a\"", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Drafted B", body, StringComparison.Ordinal);
        });
    }

    [Theory(Timeout = 120_000), Trait("Lane", "integration"), MemberData(nameof(Routes))]
    public async Task A_session_reads_the_model_label_fenced_and_a_human_reads_it_clean(string route, string url)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await ImportedHost();

        var session = await host.Http.Ask(Get(url, [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)]));
        var human = await host.Http.Ask(Get(url, Human(host.Token.Value)));

        Assert.True(session.Status == System.Net.HttpStatusCode.OK && human.Status == System.Net.HttpStatusCode.OK, $"{route}: {session.Status} {session.Body} / {human.Status} {human.Body}");
        Assert.True(session.Body.Contains(Fenced, StringComparison.Ordinal), $"{route} (session) carries no fenced label: {session.Body}");
        Assert.True(human.Body.Contains("Drafted B", StringComparison.Ordinal) && !human.Body.Contains("<untrusted", StringComparison.Ordinal), $"{route} (human) is not clean: {human.Body}");
        Assert.Contains("\"trust\":\"untrusted\"", human.Body, StringComparison.Ordinal);
    }

    private async Task<GoldenHost> ImportedHost()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await ContextHosts.Approved(_repo, sessions: true, ct,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: FixtureMap.Repo);
        var file = Path.Combine(host.Home, "drafted-map.yaml");
        await File.WriteAllTextAsync(file, string.Join('\n',
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:a\", label: \"A\"}",
            "  - {key: \"feature:b\", label: \"Drafted B\", provenance: model, confidence: 0.5}",
            "  - {key: \"code:src/layout/SplitPane.ts\", label: \"a.ts\", provenance: deterministic}",
            "edges:",
            "  - {from: \"feature:b\", to: \"feature:a\", type: part-of}",
            "  - {from: \"code:src/layout/SplitPane.ts\", to: \"feature:a\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/layout/SplitPane.ts\"}]}",
            "  - {from: \"code:src/layout/SplitPane.ts\", to: \"feature:b\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/layout/SplitPane.ts\"}]}") + "\n", ct);
        await ContextMapApiTests.Import(host, file);
        return host;
    }
}
