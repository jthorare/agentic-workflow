using System.Net;
using System.Text.Json;
using Aw.Context.Testing.Fixtures;
using Aw.Sindri.Tests.ApiGoldens;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>GET /sindri/context/code on a host over the CodeLensRepo (context spec, scoping station C3): the shape, the lexical rows and the refusals.</summary>
public sealed class ContextCodeApiTests : IDisposable
{
    private readonly CodeLensRepo _repo = CodeLensRepo.Build();

    public void Dispose() => _repo.Dispose();

    private async Task<GoldenHost> Host()
    {
        var host = await ContextHosts.Approved(_repo.Path, sessions: true, TestContext.Current.CancellationToken, name: CodeLensRepo.Repo, contextExtra: "  code: { testGlobs: [\"**/tests/**\", \"**/*.test.*\"] }\n");
        await ContextMapApiTests.Import(host, CodeLensRepo.WriteMap(host.Home, CodeLensRepo.MapYaml()));
        return host;
    }

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Code_lists_files_lexical_tests_things_and_schema_files_with_the_searched_rows()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var answer = await host.Http.Ask(Get("/sindri/context/code?anchor=feature%3Asharing", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var view = Json(answer.Body);
        Assert.Equal(1, view.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(["src/sharing/links.ts", "src/sharing/routes.ts"], view.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("path").GetString()).ToArray(), StringComparer.Ordinal);
        var tests = view.GetProperty("tests").EnumerateArray().ToDictionary(t => t.GetProperty("path").GetString()!, t => t.GetProperty("basis").GetString(), StringComparer.Ordinal);
        Assert.Equal(("edge", "imports", "sibling"), (tests["tests/edge.test.ts"], tests["tests/links.test.ts"], tests["tests/routes.test.ts"]));
        Assert.Equal(["migrations/002_sharing.sql"], view.GetProperty("data").GetProperty("files").EnumerateArray().Select(f => f.GetProperty("path").GetString()).ToArray());
        Assert.Equal("unavailable", view.GetProperty("callers").GetProperty("state").GetString());
        Assert.Equal(["map", "code-index", "lexical-tests", "lexical-schema"], view.GetProperty("searched").GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("id").GetString()).ToArray());
        Assert.DoesNotContain("PRIVATE=1", answer.Body, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Code_refuses_an_unknown_anchor_as_003_and_takes_fifty_anchors()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var unknown = await host.Http.Ask(Get("/sindri/context/code?anchor=feature%3Anope", Human(host.Token.Value)));
        var many = await host.Http.Ask(Get("/sindri/context/code?" + string.Join('&', Enumerable.Range(0, 50).Select(i => "anchor=feature%3Anope." + i.ToString(System.Globalization.CultureInfo.InvariantCulture))), Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (unknown.Status, unknown.Code));
        Assert.Equal("SND-CONTEXT-003", many.Code);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Code_with_an_unretained_map_version_is_120_and_with_a_draft_beside_it_006()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var gone = await host.Http.Ask(Get("/sindri/context/code?anchor=feature%3Asharing&mapVersion=v99", Human(host.Token.Value)));
        var both = await host.Http.Ask(Get("/sindri/context/code?anchor=feature%3Asharing&mapVersion=v1&draft=true", Human(host.Token.Value)));

        Assert.Equal("SND-CONTEXT-120", gone.Code);
        Assert.Equal("SND-CONTEXT-006", both.Code);
    }
}
