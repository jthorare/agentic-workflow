using System.Net;
using System.Text;
using System.Text.Json;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Fakes;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Host.Text;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// The onyx source wired into Sindri (context spec §5, §8.8): the profile names it with a secret pointer, Composition
/// resolves the pointer, queries fuse it with repo-docs, 'context sources status' shows its
/// state, and an Onyx that doesn't answer degrades only itself.
/// </summary>
public sealed class OnyxContextTests : IDisposable
{
    private const string KeyVariable = "SINDRI_TEST_ONYX_KEY";
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_query_fuses_onyx_with_repo_docs_and_status_shows_the_key()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var onyx = await FakeOnyxServer.Start();
        await using var host = await Approved(onyx, ct);

        var answer = await host.Http.Ask(Get("/sindri/context/query?q=split%20pane%20orientation", Human(host.Token.Value)));
        var sources = await host.Http.Ask(Get("/sindri/context/sources", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var refs = Sources(answer.Body);
        Assert.Contains("onyx", refs, StringComparer.Ordinal);
        Assert.Contains("repo-docs", refs, StringComparer.Ordinal);
        Assert.Contains("\"state\":\"unchecked\"", answer.Body, StringComparison.Ordinal);   // onyx citations stay version unchecked (no Versions route)
        Assert.Contains("\"aliases\":[\"SPL-", answer.Body, StringComparison.Ordinal);   // the Linear identifier beside its UUID key
        var row = Row(sources.Body, "onyx");
        Assert.Equal("ok", row.GetProperty("state").GetString());
        Assert.Contains("key: admin, service account", row.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(onyx.Token, answer.Body + sources.Body, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_planted_identifier_in_a_permalink_is_never_in_json_text_or_http_and_a_clean_one_comes_through()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxPlant = json => json.Replace("/issue/SPL-103", "/issue/MRN-1234567", StringComparison.Ordinal) });
        await using var host = await Approved(onyx, ct);

        var answer = await host.Http.Ask(Get("/sindri/context/query?q=split%20pane%20orientation", Human(host.Token.Value)));
        var text = ContextText.Render(AwJson.Deserialize<ContextResult>(Encoding.UTF8.GetBytes(answer.Body), SliceHarness.Json).Value);

        Assert.Equal(HttpStatusCode.OK, answer.Status);   // the body is what 'context query --json' prints
        Assert.Contains("linear:issue/0e5a1b00-0000-4000-8000-000000000103", answer.Body, StringComparison.Ordinal);   // the citation and its key stay
        Assert.DoesNotContain("1234567", answer.Body + text, StringComparison.Ordinal);
        Assert.Contains("\"permalink\":\"https://linear.app/fixture-labs/issue/SPL-", answer.Body, StringComparison.Ordinal);   // an unchanged allowed link still comes through
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task An_onyx_that_doesnt_answer_degrades_only_itself()
    {
        var ct = TestContext.Current.CancellationToken;
        var onyx = await FakeOnyxServer.Start();
        await using var host = await Approved(onyx, ct);
        await onyx.DisposeAsync();

        var answer = await host.Http.Ask(Get("/sindri/context/query?q=split%20pane%20orientation", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        using var result = JsonDocument.Parse(answer.Body);
        var degraded = result.RootElement.GetProperty("degraded").EnumerateArray().Single();
        Assert.Equal(("onyx", "SND-CONTEXT-010"), (degraded.GetProperty("source").GetString(), degraded.GetProperty("code").GetString()));
        Assert.Equal(["repo-docs"], Sources(answer.Body));
    }

    private static List<string> Sources(string body)
    {
        using var result = JsonDocument.Parse(body);
        return [.. result.RootElement.GetProperty("cites").EnumerateObject()
            .SelectMany(c => c.Value.GetProperty("refs").EnumerateArray().Select(r => r.GetProperty("source").GetString()!))   // why: every ref names its source
            .Distinct(StringComparer.Ordinal)];
    }

    private static JsonElement Row(string body, string source)
    {
        using var view = JsonDocument.Parse(body);
        return view.RootElement.GetProperty("sources").EnumerateArray().Single(r => r.GetProperty("source").GetString() == source).Clone();
    }

    /// <summary>A host whose approved profile maps the fixture repo and names <paramref name="onyx"/> behind an env: pointer, with repo-docs refreshed.</summary>
    private async Task<GoldenHost> Approved(FakeOnyxServer onyx, CancellationToken ct, Dictionary<string, string>? environment = null, string pointer = "env:" + KeyVariable)
    {
        var yaml = $"schemaVersion: 1\nuser: golden\nrepos:\n  - {{ name: split-panes, path: \"{_repo.Path}\" }}\n"
            + "context:\n  docs: { globs: [\"docs/**/*.md\"] }\n  sources:\n"
            + $"    onyx: {{ url: \"{onyx.Url}\", token: \"{pointer}\", version: \"{FakeOnyxServer.PinnedVersion}\" }}\n"
            + "    linear: { teams: [SPL] }\n";
        var host = await GoldenHost.Start(temporary: true, ct, yaml, environment ?? new Dictionary<string, string>(StringComparer.Ordinal) { [KeyVariable] = onyx.Token });
        var hash = (await host.LiveHash(ct)).Value;
        var approved = await host.Http.Ask(Post("/sindri/profile/approve", await ApiScenarios.Confirmed(host, hash), Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, approved.Status);
        await using var scope = host.App.Services.CreateAsyncScope();
        var refresh = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity().Value == "context-docs-refresh");
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease
        Assert.True((await refresh.Run(new ScheduledRun(grant, DateTimeOffset.UnixEpoch), ct)).IsSuccess);
        return host;
    }
}
