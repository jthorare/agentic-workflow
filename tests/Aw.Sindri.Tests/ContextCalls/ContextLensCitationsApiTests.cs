using System.Net;
using System.Text.Json;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Records;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Records;
using Aw.Sindri.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>#1702: GET /sindri/context/lenses carries a stored record's citation, so the app can offer "Open in Linear".</summary>
public sealed class ContextLensCitationsApiTests : IDisposable
{
    private const string Key = "linear:issue/0e5a1b00-0000-4000-8000-000000000a02";
    private const string Url = "https://linear.app/split-panes/issue/SPL-103/keep-the-pane-in-view";
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_stored_linear_record_in_the_anchors_scope_comes_out_cited_with_its_permalink_and_identifier()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ContextHosts.Approved(_repo, sessions: true, ct,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); },
            name: FixtureMap.Repo, contextExtra: "  sources:\n    linear: { teams: [SPL] }\n");
        var file = Path.Combine(host.Home, "intent-map.yaml");
        await File.WriteAllTextAsync(file, string.Join('\n',
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:in\", label: \"In scope\"}",
            "  - {key: \"code:src/in.ts\", label: \"in.ts\", provenance: deterministic}",
            $"  - {{key: \"record:{Key}\", label: \"Keep the pane in view\"}}",
            "edges:",
            "  - {from: \"code:src/in.ts\", to: \"feature:in\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/in.ts\"}]}",
            $"  - {{from: \"record:{Key}\", to: \"feature:in\", type: mentions}}") + "\n", ct);
        await ContextMapApiTests.Import(host, file);
        var stored = RecordSamples.Record(Key) with { Url = Url };
        var commit = await host.App.Services.GetRequiredService<IRecordStore>().Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(stored, "text")), ct);
        Assert.True(commit.IsSuccess);

        var lenses = await host.Http.Ask(Get("/sindri/context/lenses?anchor=feature%3Ain", [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)]));

        Assert.Equal(HttpStatusCode.OK, lenses.Status);
        var answer = JsonDocument.Parse(lenses.Body).RootElement;
        var record = answer.GetProperty("bands").GetProperty("intent").GetProperty("records")[0];
        Assert.Equal("C1", Assert.Single(record.GetProperty("cites").EnumerateArray()).GetString());
        var cite = answer.GetProperty("cites").GetProperty("C1");
        Assert.Equal(Url, cite.GetProperty("permalink").GetString());
        Assert.Equal("SPL-103", Assert.Single(cite.GetProperty("aliases").EnumerateArray()).GetString());
    }
}
