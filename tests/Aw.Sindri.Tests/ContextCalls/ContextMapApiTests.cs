using System.Net;
using System.Text.Json;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The map routes (S-3a): GET /sindri/context/map, GET /sindri/context/map/export, POST /sindri/context/map/import (protected).</summary>
public sealed class ContextMapApiTests
{
    private static string Body(string path, string? hash = null, (string Challenge, string Typed)? answer = null) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["path"] = path,
            ["hash"] = hash,
            ["answer"] = answer is { } a ? new Dictionary<string, string>(StringComparer.Ordinal) { ["challenge"] = a.Challenge, ["typed"] = a.Typed } : null,
        });

    /// <summary>The preview's content hash; the preview carries no challenge any more (owner decision q93).</summary>
    private static string HashOf(string previewBody) =>
        JsonDocument.Parse(previewBody).RootElement.GetProperty("preview").GetProperty("hash").GetString() ?? string.Empty;

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Without_a_map_status_is_SND_CONTEXT_030()
    {
        await using var host = await Approved(sessions: false, TestContext.Current.CancellationToken);

        var status = await host.Http.Ask(Get("/sindri/context/map", Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-030"), (status.Status, status.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Without_a_map_the_publish_status_is_SND_CONTEXT_030()
    {
        await using var host = await Approved(sessions: false, TestContext.Current.CancellationToken);

        var status = await host.Http.Ask(Get("/sindri/context/map/publish", Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-030"), (status.Status, status.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task After_an_import_the_publish_status_is_published_by_nobodys_hand_with_the_maps_sentence()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: false, ct);
        var file = FixtureMap.WriteTo(host.Home);
        var hash = HashOf(await PreviewBody(host, file));
        _ = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash), Human(host.Token.Value)));

        var status = await host.Http.Ask(Get("/sindri/context/map/publish", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, status.Status);
        var root = JsonDocument.Parse(status.Body).RootElement;
        Assert.Equal(("published", false, 1), (root.GetProperty("state").GetString(), root.GetProperty("stale").GetBoolean(), root.GetProperty("version").GetInt32()));
        Assert.Equal("Map v1 is current.", root.GetProperty("lines")[0].GetString());
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_human_previews_then_imports_with_the_hash_alone_and_the_import_is_recorded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: true, ct);
        var file = FixtureMap.WriteTo(host.Home);

        var preview = await host.Http.Ask(Post("/sindri/context/map/import", Body(file), Human(host.Token.Value)));
        var hash = HashOf(preview.Body);
        var bySession = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash), (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)));
        var imported = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash), Human(host.Token.Value)));
        var status = await host.Http.Ask(Get("/sindri/context/map", Human(host.Token.Value)));
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextMapImported, ct);

        Assert.Equal(HttpStatusCode.OK, preview.Status);
        Assert.False(JsonDocument.Parse(preview.Body).RootElement.TryGetProperty("challenge", out var challenge) && challenge.ValueKind == JsonValueKind.Object);
        Assert.Equal((HttpStatusCode.Forbidden, "SND-CONTEXT-020"), (bySession.Status, bySession.Code));   // the context engine's session gate (group F), not the command chain's
        Assert.Equal(HttpStatusCode.OK, imported.Status);
        Assert.Contains("\"status\":\"current\"", imported.Body, StringComparison.Ordinal);
        Assert.Contains("\"nodes\":20", status.Body, StringComparison.Ordinal);
        Assert.Contains(hash, recorded.Value!.Envelope.Payload, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_changed_file_is_SND_CONTEXT_034_and_a_foreign_origin_or_no_caller_is_refused_and_nothing_is_written()
    {
        await using var host = await Approved(sessions: false, TestContext.Current.CancellationToken);
        var file = FixtureMap.WriteTo(host.Home);

        var hash = HashOf(await PreviewBody(host, file));
        await File.AppendAllTextAsync(file, "# edited\n", TestContext.Current.CancellationToken);
        var changed = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash), Human(host.Token.Value)));
        var origin = await host.Http.Ask(Post("/sindri/context/map/import", Body(file), [.. Human(host.Token.Value), ("Origin", "http://evil.example")]));
        var status = await host.Http.Ask(Get("/sindri/context/map", Human(host.Token.Value)));

        Assert.Equal("SND-CONTEXT-034", changed.Code);
        Assert.Equal(HttpStatusCode.Forbidden, origin.Status);   // the loopback guard refuses a foreign browser Origin before the command chain
        Assert.Equal("SND-CONTEXT-030", status.Code);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Export_lands_in_the_exports_folder_a_session_can_not_export_and_reads_only_its_repos_scope()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: true, ct);
        await Import(host);

        var export = await host.Http.Ask(Get("/sindri/context/map/export", Human(host.Token.Value)));
        var outside = await host.Http.Ask(Get("/sindri/context/map/export?path=%2Ftmp%2Fx.json", Human(host.Token.Value)));
        var scoped = await host.Http.Ask(Get("/sindri/context/map", (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)));
        var other = await host.Http.Ask(Get("/sindri/context/map", (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "other")));
        var sessionExport = await host.Http.Ask(Get("/sindri/context/map/export", (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-CONTEXT-020"), (sessionExport.Status, sessionExport.Code));   // spec §3: an export is a whole-map path
        Assert.Equal(HttpStatusCode.OK, export.Status);
        Assert.Contains(Path.Combine(host.Home, "context", "exports"), JsonDocument.Parse(export.Body).RootElement.GetProperty("path").GetString(), StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-033"), (outside.Status, outside.Code));
        Assert.Contains("\"nodes\":17", scoped.Body, StringComparison.Ordinal);   // its 13 implemented/hierarchy nodes, the 3 actions on its surfaces and the thing they read or write (spec §3 "and their cascade")
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (other.Status, other.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Map_status_with_an_anchor_adds_the_lenses_coverage_and_without_one_has_none()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: false, ct);
        var tabs = Path.Combine(host.Home, "tabs-map.json");
        await File.WriteAllTextAsync(tabs, MapTabsRepo.MapJson, ct);
        await Import(host, tabs);
        var anchor = Uri.EscapeDataString("feature:tabs.tab-manager");

        var plain = await host.Http.Ask(Get("/sindri/context/map", Human(host.Token.Value)));
        var withAnchor = await host.Http.Ask(Get("/sindri/context/map?anchor=" + anchor, Human(host.Token.Value)));
        var lenses = await host.Http.Ask(Get("/sindri/context/lenses?anchor=" + anchor, Human(host.Token.Value)));
        var unknown = await host.Http.Ask(Get("/sindri/context/map?anchor=nope", Human(host.Token.Value)));

        Assert.False(JsonDocument.Parse(plain.Body).RootElement.TryGetProperty("coverage", out _));
        var coverage = JsonDocument.Parse(withAnchor.Body).RootElement.GetProperty("coverage");
        var lensesCoverage = System.Text.Json.Nodes.JsonNode.Parse(lenses.Body)!["coverage"]!.AsObject();
        Assert.True(lensesCoverage.Remove("map"));   // the repo-file part of the lenses' coverage (C13) is theirs alone: status counts the map's own scope
        Assert.Equal(lensesCoverage.ToJsonString(), coverage.GetRawText());
        Assert.Equal((6, 2, 6, 2), (coverage.GetProperty("scope").GetInt32(), coverage.GetProperty("intent").GetProperty("covered").GetInt32(),
            coverage.GetProperty("product").GetProperty("confirmed").GetInt32(), coverage.GetProperty("code").GetProperty("implemented").GetInt32()));
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (unknown.Status, unknown.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task The_context_purge_service_runs_the_purge()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Approved(sessions: false, ct);
        await using var scope = host.App.Services.CreateAsyncScope();

        var purge = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity().Value == "context-purge");
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease

        Assert.True((await purge.Run(new ScheduledRun(grant, DateTimeOffset.UnixEpoch), ct)).IsSuccess);
    }

    internal static async Task Import(GoldenHost host, string? mapFile = null)
    {
        var file = mapFile ?? FixtureMap.WriteTo(host.Home);
        var hash = HashOf(await PreviewBody(host, file));
        var imported = await host.Http.Ask(Post("/sindri/context/map/import", Body(file, hash), Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, imported.Status);
    }

    private static async Task<string> PreviewBody(GoldenHost host, string file) =>
        (await host.Http.Ask(Post("/sindri/context/map/import", Body(file), Human(host.Token.Value)))).Body;

    /// <summary>A host whose approved profile maps the fixture map's repo and 'other'.</summary>
    internal static async Task<GoldenHost> Approved(bool sessions, CancellationToken ct)
    {
        var yaml = $"schemaVersion: 1\nuser: golden\nrepos:\n  - {{ name: {FixtureMap.Repo}, path: /work/{FixtureMap.Repo} }}\n  - {{ name: other, path: /work/other }}\n"
            + $"context:\n  sessions: {{ enabled: {(sessions ? "true" : "false")} }}\n";
        var host = await GoldenHost.Start(temporary: true, ct, yaml);
        var hash = await host.LiveHash(ct);
        var approved = await host.Http.Ask(Post("/sindri/profile/approve", $$"""{"hash":"{{hash.Value}}"}""", Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, approved.Status);
        return host;
    }
}
