using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Locks;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// The S-4 routes on a started host over the fixture repo (README X63; context spec §8.5, §8.7): a draft from the context-rebuild
/// service, review actions (the app, the terminal and an agent session, with no typed challenge since owner q93, 2026-10-23), promote,
/// scoping proposals, lenses and filemap, and the exit-code errors 040 and 048.
/// </summary>
public sealed class ContextReviewApiTests : IDisposable
{
    private readonly FixtureRepo _repo = FixtureRepo.Build();
    private readonly string _locks = Directory.CreateTempSubdirectory("aw-s4-locks-").FullName;

    public void Dispose()
    {
        _repo.Dispose();
        Directory.Delete(_locks, recursive: true);
    }

    private string LockPath => Path.Combine(_locks, "heavy-job.lock");

    /// <summary>Fake clusters (graphify is a heavy-lane test) and a lock directory of the test's own, whatever the heavy wrapper exported.</summary>
    private Action<IServiceCollection> Wiring(TimeSpan? wait = null) => services =>
    {
        _ = services.RemoveAll<IClusterSource>();
        _ = services.AddSingleton<IClusterSource>(new FakeClusterSource());
        _ = services.RemoveAll<HeavyLockSettings>();
        _ = services.AddSingleton(new HeavyLockSettings(LockPath, 2, Held: false));
        _ = services.RemoveAll<IHeavyJobLock>();
        _ = services.AddSingleton<IHeavyJobLock>(sp => new SindriHeavyJobLock(sp.GetRequiredService<Core.Locks.IHeavySlots>(), wait ?? TimeSpan.FromSeconds(5)));
    };

    private async Task<GoldenHost> Host(bool sessions, TimeSpan? wait = null) =>
        await ContextHosts.Approved(_repo, sessions, TestContext.Current.CancellationToken, Wiring(wait));

    private static (string Name, string Value)[] Terminal(GoldenHost host) => Human(host.Token.Value);

    private static (string Name, string Value)[] App(GoldenHost host) => [.. Human(host.Token.Value, client: "ui").Where(h => h.Name != SindriHeaders.Terminal)];

    private static (string Name, string Value)[] Session() => [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "split-panes")];

    private static async Task<Kernel.Result<ScheduledResult>> Run(GoldenHost host, string service)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var run = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity().Value == service);
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease
        return await run.Run(new ScheduledRun(grant, DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken);
    }

    private static string Review(string verb, string? node = null, string? override_ = null, string? path = null, string? label = null, string? other = null, string[]? overrides = null) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["verb"] = verb,
            ["node"] = node,
            ["override"] = override_,
            ["path"] = path,
            ["label"] = label,
            ["other"] = other,
            ["overrides"] = overrides,
        });

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    private static async Task<JsonElement> Queue(GoldenHost host) => Json((await host.Http.Ask(Get("/sindri/context/map/review", Terminal(host)))).Body);

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task The_rebuild_service_writes_a_draft_and_records_context_map_built_and_the_queue_lists_deterministic_proposals()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host(sessions: false);

        var built = await Run(host, "context-rebuild");
        var queue = await host.Http.Ask(Get("/sindri/context/map/review", Terminal(host)));
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextMapBuilt, ct);

        Assert.True(built.IsSuccess, built.IsError ? built.Error.Message : string.Empty);
        Assert.Equal(HttpStatusCode.OK, queue.Status);
        Assert.Contains("\"group\":\"deterministic\"", queue.Body, StringComparison.Ordinal);
        Assert.Contains("surface:command/", queue.Body, StringComparison.Ordinal);
        Assert.Contains("\"degraded\":[\"clusters\"]", recorded.Value!.Envelope.Payload, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_desk_decision_from_the_app_writes_one_row_a_session_may_decide_too_and_undoing_twice_is_047()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host(sessions: true);
        Assert.True((await Run(host, "context-rebuild")).IsSuccess);
        var node = (await Queue(host)).GetProperty("entries")[0].GetProperty("node").GetString()!;

        var accepted = await host.Http.Ask(Post("/sindri/context/map/review", Review("accept", node), App(host)));
        var other = (await Queue(host)).GetProperty("entries")[1].GetProperty("node").GetString()!;
        var session = await host.Http.Ask(Post("/sindri/context/map/review", Review("reject", other), Session()));
        var id = Json(accepted.Body).GetProperty("result").GetProperty("written")[0].GetString()!;
        var undone = await host.Http.Ask(Post("/sindri/context/map/review", Review("undo", override_: id), App(host)));
        var twice = await host.Http.Ask(Post("/sindri/context/map/review", Review("undo", override_: id), App(host)));
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextMapReviewed, ct);
        var history = await host.Http.Ask(Get("/sindri/context/map/review/history", Terminal(host)));

        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        Assert.Equal(1, Json(accepted.Body).GetProperty("result").GetProperty("written").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, session.Status);
        Assert.Equal(HttpStatusCode.OK, undone.Status);
        Assert.Equal((HttpStatusCode.Conflict, "SND-CONTEXT-047"), (twice.Status, twice.Code));
        Assert.Contains("\"verb\":\"undo\"", recorded.Value!.Envelope.Payload, StringComparison.Ordinal);
        Assert.Contains(id, history.Body, StringComparison.Ordinal);
        Assert.Contains("\"undone\":true", history.Body, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Undo_of_a_list_is_all_or_none_and_records_one_event_with_the_row_count()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host(sessions: false);
        Assert.True((await Run(host, "context-rebuild")).IsSuccess);
        var entries = (await Queue(host)).GetProperty("entries");
        var ids = new List<string>();
        foreach (var node in new[] { entries[0].GetProperty("node").GetString()!, entries[1].GetProperty("node").GetString()! })
        {
            var accepted = await host.Http.Ask(Post("/sindri/context/map/review", Review("accept", node), App(host)));
            ids.Add(Json(accepted.Body).GetProperty("result").GetProperty("written")[0].GetString()!);
        }

        ((FakeTimeProvider)host.App.Services.GetRequiredService<TimeProvider>()).Advance(TimeSpan.FromMinutes(1));   // why: the golden host's clock stands still, and an undo is ordered after what it undoes by its time
        var undone = await host.Http.Ask(Post("/sindri/context/map/review", Review("undo", overrides: [.. ids]), App(host)));
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextMapReviewed, ct);
        var again = await host.Http.Ask(Post("/sindri/context/map/review", Review("undo", overrides: [.. ids]), App(host)));
        var both = await host.Http.Ask(Post("/sindri/context/map/review", Review("undo", override_: ids[0], overrides: [ids[1]]), App(host)));
        var empty = await host.Http.Ask(Post("/sindri/context/map/review", Review("undo", overrides: []), App(host)));
        var tooMany = await host.Http.Ask(Post("/sindri/context/map/review", Review("undo", overrides: [.. Enumerable.Repeat("ovr_0123456789abcdef", 1001)]), App(host)));
        var history = Json((await host.Http.Ask(Get("/sindri/context/map/review/history", Terminal(host)))).Body).GetProperty("entries");

        Assert.Equal(HttpStatusCode.OK, undone.Status);
        Assert.Equal(2, Json(undone.Body).GetProperty("result").GetProperty("written").GetArrayLength());
        Assert.Contains("\"rows\":2", recorded.Value!.Envelope.Payload, StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.Conflict, "SND-CONTEXT-047"), (again.Status, again.Code));
        Assert.Contains(ids[0], again.Body, StringComparison.Ordinal);
        Assert.Equal(("SND-CLI-003", "SND-CLI-003", "SND-CLI-003"), (both.Code, empty.Code, tooMany.Code));
        Assert.Equal(4, history.GetArrayLength());
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task The_queue_carries_its_version_counts_and_kinds_and_caps_each_group_at_the_limit()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host(sessions: false);
        Assert.True((await Run(host, "context-rebuild")).IsSuccess);

        var full = Json((await host.Http.Ask(Get("/sindri/context/map/review", Terminal(host)))).Body);
        var capped = Json((await host.Http.Ask(Get("/sindri/context/map/review?limit=1", Terminal(host)))).Body);
        var zero = await host.Http.Ask(Get("/sindri/context/map/review?limit=0", Terminal(host)));
        var huge = await host.Http.Ask(Get("/sindri/context/map/review?limit=1001", Terminal(host)));
        var historyZero = await host.Http.Ask(Get("/sindri/context/map/review/history?limit=0", Terminal(host)));
        var history = Json((await host.Http.Ask(Get("/sindri/context/map/review/history?limit=1", Terminal(host)))).Body);

        var deterministic = full.GetProperty("counts").GetProperty("deterministic").GetInt32();
        Assert.True(deterministic > 1);
        Assert.True(full.GetProperty("version").GetProperty("number").GetInt32() > 0);
        Assert.Equal(7, full.GetProperty("version").GetProperty("commit").GetString()!.Length);
        Assert.Equal(deterministic, full.GetProperty("byKind").EnumerateObject().Sum(k => k.Value.GetInt32()));
        Assert.Equal(deterministic, capped.GetProperty("counts").GetProperty("deterministic").GetInt32());
        Assert.Equal(1, capped.GetProperty("entries").EnumerateArray().Count(e => e.GetProperty("group").GetString() == "deterministic"));
        Assert.True(capped.GetProperty("truncated").GetProperty("deterministic").GetBoolean());
        Assert.False(full.GetProperty("truncated").GetProperty("deterministic").GetBoolean());
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (zero.Status, zero.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (huge.Status, huge.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (historyZero.Status, historyZero.Code));
        Assert.False(history.GetProperty("truncated").GetBoolean());
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Promote_is_046_while_items_are_undecided_then_a_session_promotes_with_no_challenge_and_the_draft_becomes_current()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host(sessions: true);
        Assert.True((await Run(host, "context-rebuild")).IsSuccess);
        var undecided = await host.Http.Ask(Post("/sindri/context/map/promote", "{}", Terminal(host)));
        var bulk = await host.Http.Ask(Post("/sindri/context/map/review", Review("accept-deterministic"), Session()));

        var promoted = await host.Http.Ask(Post("/sindri/context/map/promote", "{}", Session()));
        var status = await host.Http.Ask(Get("/sindri/context/map", Terminal(host)));
        var recorded = await host.App.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ContextMapPromoted, ct);

        Assert.Equal((HttpStatusCode.Conflict, "SND-CONTEXT-046"), (undecided.Status, undecided.Code));
        Assert.Equal(HttpStatusCode.OK, bulk.Status);
        Assert.Equal(HttpStatusCode.OK, promoted.Status);
        Assert.DoesNotContain("challenge", promoted.Body, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"current\"", status.Body, StringComparison.Ordinal);
        Assert.NotNull(recorded.Value);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Promoting_a_draft_that_is_no_longer_the_latest_is_046_promote_refused_and_names_both_drafts_not_047()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host(sessions: false);
        Assert.True((await Run(host, "context-rebuild")).IsSuccess);
        Assert.Equal(HttpStatusCode.OK, (await host.Http.Ask(Post("/sindri/context/map/review", Review("accept-deterministic"), App(host)))).Status);

        var stale = await host.Http.Ask(Post("/sindri/context/map/promote", JsonSerializer.Serialize(new { draft = "v99@0000000" }), Terminal(host)));

        Assert.Equal((HttpStatusCode.Conflict, "SND-CONTEXT-046"), (stale.Status, stale.Code));
        Assert.Contains("v99@0000000", stale.Body, StringComparison.Ordinal);
        Assert.Contains("latest draft", stale.Body, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Applying_a_review_file_acts_at_once_for_a_session_and_a_second_apply_matches_nothing_043()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host(sessions: true);
        Assert.True((await Run(host, "context-rebuild")).IsSuccess);
        var export = await host.Http.Ask(Get("/sindri/context/map/review/export", Terminal(host)));
        var path = Json(export.Body).GetProperty("path").GetString()!;
        var file = JsonNode.Parse(await File.ReadAllTextAsync(path, ct))!;
        foreach (var entry in file["entries"]!.AsArray())
        {
            entry!["decision"] = "accept";
        }

        await File.WriteAllTextAsync(path, file.ToJsonString(), ct);

        var applied = await host.Http.Ask(Post("/sindri/context/map/review", Review("apply-file", path: path), Session()));
        var again = await host.Http.Ask(Post("/sindri/context/map/review", Review("apply-file", path: path), Terminal(host)));   // every entry is decided by now

        Assert.Equal(HttpStatusCode.OK, export.Status);
        Assert.Equal(HttpStatusCode.OK, applied.Status);
        Assert.True(Json(applied.Body).GetProperty("result").GetProperty("written").GetArrayLength() > 0);
        Assert.DoesNotContain("challenge", applied.Body, StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.Conflict, "SND-CONTEXT-043"), (again.Status, again.Code));   // nothing is left to match, and no challenge is asked either
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_review_file_inside_the_repo_is_005_and_one_that_matches_nothing_is_043()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host(sessions: false);
        Assert.True((await Run(host, "context-rebuild")).IsSuccess);
        var inside = Path.Combine(_repo.Path, "review.json");
        await File.WriteAllTextAsync(inside, """{"draft":"v1@x","entries":[]}""", ct);
        var elsewhere = Path.Combine(host.Home, "context", "exports", "nothing.json");
        Directory.CreateDirectory(Path.GetDirectoryName(elsewhere)!);
        await File.WriteAllTextAsync(elsewhere, """{"draft":"v1@x","entries":[{"label":"Gone","group":"model","confidence":0.5,"node":"surface:command/not-there","decision":"accept"}]}""", ct);

        var worktree = await host.Http.Ask(Post("/sindri/context/map/review", Review("apply-file", path: inside), Terminal(host)));
        var nothing = await host.Http.Ask(Post("/sindri/context/map/review", Review("apply-file", path: elsewhere), Terminal(host)));

        Assert.Equal("SND-CONTEXT-005", worktree.Code);
        Assert.Equal((HttpStatusCode.Conflict, "SND-CONTEXT-043"), (nothing.Status, nothing.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task The_catalog_serves_every_review_row_and_promote_as_desk_and_the_build_jobs_as_jobs()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host(sessions: false);

        var commands = await host.Http.Ask(Get("/sindri/commands", Terminal(host)));

        Assert.True(commands.Status == HttpStatusCode.OK, commands.Body);
        var rows = Json(commands.Body).GetProperty("rows").EnumerateArray().ToDictionary(r => r.GetProperty("action").GetString()!, r => r.GetProperty("class").GetString()!, StringComparer.Ordinal);
        Assert.All(["accept", "reject", "rename", "move", "accept-deterministic", "undo", "drop", "repoint"], v => Assert.Equal("desk", rows["ReviewContextMap:" + v]));
        Assert.All(["apply-file", "merge", "add", "delete"], v => Assert.Equal("desk", rows["ReviewContextMap:" + v]));
        Assert.Equal(("desk", "job", "job"), (rows["PromoteContextMap"], rows["ProposeContextMap"], rows["BuildContextMap"]));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_repo_git_cant_read_is_048_and_nothing_is_written()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host(sessions: false);
        Directory.Delete(Path.Combine(_repo.Path, ".git"), recursive: true);

        var built = await Run(host, "context-rebuild");
        var queue = await host.Http.Ask(Get("/sindri/context/map/review", Terminal(host)));

        Assert.Equal("SND-CONTEXT-048", built.Error.Code);
        Assert.Equal("SND-CONTEXT-044", queue.Code);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_busy_heavy_job_lock_is_040_naming_the_holder_and_nothing_is_written()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host(sessions: false, wait: TimeSpan.Zero);
        var script = Path.Combine(FindRepoRoot(), "config", "lib", "locks.sh");
        var info = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardInput = true, UseShellExecute = false };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add($". '{script}'; acquire_any_lock '{LockPath}' 2 0 >/dev/null; acquire_any_lock '{LockPath}' 2 0 >/dev/null; echo held; read -r _");
        using var holder = Process.Start(info)!;
        Assert.Equal("held", (await holder.StandardOutput.ReadLineAsync(ct))?.Trim());
        try
        {
            var built = await Run(host, "context-rebuild");
            var queue = await host.Http.Ask(Get("/sindri/context/map/review", Terminal(host)));

            Assert.Equal("SND-CONTEXT-040", built.Error.Code);
            Assert.Contains("slot 1/2", built.Error.Message, StringComparison.Ordinal);
            Assert.Equal("SND-CONTEXT-044", queue.Code);
        }
        finally
        {
            await holder.StandardInput.WriteLineAsync("done".AsMemory(), ct);
            await holder.WaitForExitAsync(ct);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("the repo root isn't above " + AppContext.BaseDirectory);
    }
}
