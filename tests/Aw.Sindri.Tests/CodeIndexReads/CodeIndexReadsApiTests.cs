using System.Net;
using System.Text.Json;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>The code-index read routes (CI-10a): the scope the handlers hand the facade, the uniform not-found, and what a host with no index store answers.</summary>
public sealed class CodeIndexReadsApiTests
{
    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_session_reaches_the_facade_with_every_profile_repo_like_a_human()
    {
        await using var host = await CodeIndexScenarios.Start(TestContext.Current.CancellationToken);

        _ = await host.Http.Ask(Get("/sindri/code-index/status", ItemScenarios.Session("demo")));
        _ = await host.Http.Ask(Get("/sindri/code-index/status", ItemScenarios.Human(host)));

        var scopes = ((FakeCodeIndex)host.App.Services.GetRequiredService<ICodeIndex>()).Scopes;
        Assert.Equal(["demo", "other"], [.. scopes[0].Repos.Select(static r => r.Name.Value)]);   // owner q93 (2026-10-23): CIX-L56
        Assert.Equal((CallerKind.Session, "demo"), (scopes[0].Caller.Kind, scopes[0].Caller.Repo?.Value));
        Assert.Equal(["demo", "other"], [.. scopes[1].Repos.Select(static r => r.Name.Value)]);
        Assert.Equal(CallerKind.Human, scopes[1].Caller.Kind);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_session_reads_another_profile_repo_and_a_repo_outside_the_profile_is_still_404()
    {
        await using var host = await CodeIndexScenarios.Start(TestContext.Current.CancellationToken);
        var session = ItemScenarios.Session("demo");

        var other = await host.Http.Ask(Get("/sindri/code-index/status?repo=other", session));
        var nope = await host.Http.Ask(Get("/sindri/code-index/status?repo=nope", session));
        var otherSymbol = await host.Http.Ask(Get("/sindri/code-index/callers?ref=" + Uri.EscapeDataString(CodeIndexScenarios.Secret.Value), session));
        var neverSymbol = await host.Http.Ask(Get("/sindri/code-index/callers?ref=" + Uri.EscapeDataString("nope:src/X.cs#X.Y"), session));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.NotFound), (other.Status, nope.Status));
        Assert.Equal(HttpStatusCode.OK, otherSymbol.Status);
        Assert.Equal(HttpStatusCode.NotFound, neverSymbol.Status);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task With_no_index_store_wired_every_repo_is_missing_and_a_read_is_013()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, TestContext.Current.CancellationToken);

        var status = await host.Http.Ask(Get("/sindri/code-index/status", ItemScenarios.Human(host)));
        var symbols = await host.Http.Ask(Get("/sindri/code-index/symbols?query=Run", ItemScenarios.Human(host)));

        Assert.Equal(["missing", "missing"], [.. JsonDocument.Parse(status.Body).RootElement.GetProperty("repos").EnumerateArray().Select(static r => r.GetProperty("state").GetString())]);
        Assert.Equal((HttpStatusCode.NotFound, "SND-CODEIDX-013"), (symbols.Status, symbols.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Without_an_approved_profile_no_repo_is_mapped_and_the_read_is_refused()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken, ItemScenarios.ProfileYaml);

        var status = await host.Http.Ask(Get("/sindri/code-index/status", ItemScenarios.Human(host)));

        Assert.Equal("SND-PROFILE-005", status.Code);
    }
}
