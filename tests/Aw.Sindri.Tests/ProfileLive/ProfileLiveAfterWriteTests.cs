using System.Globalization;
using System.Net;
using System.Text.Json;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.CheckProfile;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ProfileLive;

/// <summary>
/// The profile view's live hash right after Sindri's own profile writes (#1826): a write job inside serve records a profile check before it settles, so a read that carries the job's sequence sees the hash the write produced,
/// with no wait for the 30 s watch. A hand edit of the file is still the watch's to notice.
/// </summary>
public sealed class ProfileLiveAfterWriteTests : IDisposable
{
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<GoldenHost> Fresh()
    {
        var host = await ContextHosts.Approved(_repo, sessions: true, Token, services => services.Replace(ServiceDescriptor.Singleton<IRepoInspector>(SetupRepoScenarios.Inspector())));
        var approved = (await host.LiveHash(Token)).Value;

        // why: the golden host runs no projector and its clock is fake, so the watch's start check never lands here; record it the way ProfileWatch does (the lease grant, as service:profile-watch) and fold it
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;
        using (host.App.Services.GetRequiredService<ICallerContextAccessor>().EnterService("profile-watch"))
        {
            await using var scope = host.App.Services.CreateAsyncScope();
            var check = await scope.ServiceProvider.GetRequiredService<IService<CheckProfileRequest, CheckProfileResponse>>().Handle(new CheckProfileRequest(grant), Token);
            Assert.True(check.IsSuccess);
        }

        Assert.Equal(approved, await ViewLive(host), StringComparer.Ordinal);   // the view begins fresh
        return host;
    }

    // Folds the ledger into the views (the golden host runs no projector), then reads the profile view's live hash.
    private static async Task<string?> ViewLive(GoldenHost host, long? after = null)
    {
        await ViewScenarios.CatchUp(host, Token);
        var headers = after is { } sequence ? [.. Human(host.Token.Value), ("X-Sindri-After", sequence.ToString(CultureInfo.InvariantCulture))] : Human(host.Token.Value);
        var answer = await host.Http.Ask(Get("/sindri/profile", headers));
        Assert.Equal(HttpStatusCode.OK, answer.Status);
        using var body = JsonDocument.Parse(answer.Body);
        return body.RootElement.TryGetProperty("live", out var live) && live.ValueKind == JsonValueKind.String ? live.GetString() : null;
    }

    // The profile view's changedPaths after the ledger reached <paramref name="after"/>.
    private static async Task<string[]> ViewChangedPaths(GoldenHost host, long after)
    {
        await ViewScenarios.CatchUp(host, Token);
        var answer = await host.Http.Ask(Get("/sindri/profile", [.. Human(host.Token.Value), ("X-Sindri-After", after.ToString(CultureInfo.InvariantCulture))]));
        Assert.Equal(HttpStatusCode.OK, answer.Status);
        using var body = JsonDocument.Parse(answer.Body);
        return body.RootElement.TryGetProperty("changedPaths", out var paths) && paths.ValueKind == JsonValueKind.Array
            ? [.. paths.EnumerateArray().Select(static p => p.GetString()!)]
            : [];
    }

    // Starts a job the way the app does and waits for it; returns its result and the ledger head once it settled.
    private static async Task<(JsonElement Result, long Settled)> RunJob(GoldenHost host, string route, string json)
    {
        var started = await host.Http.Ask(Post(route, json, Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, started.Status);
        var id = JsonDocument.Parse(started.Body).RootElement.GetProperty("jobId").GetString();
        for (var i = 0; i < 400; i++)
        {
            await ViewScenarios.CatchUp(host, Token);
            var view = JsonDocument.Parse((await host.Http.Ask(Get($"/sindri/jobs/{id}", Human(host.Token.Value)))).Body).RootElement.Clone();
            if (view.GetProperty("state").GetString() != "running")
            {
                Assert.Equal("succeeded", view.GetProperty("state").GetString());
                var head = (await host.App.Services.GetRequiredService<ILedgerStore>().Head(Token)).Value;
                return (view.GetProperty("result").Clone(), head);
            }

            await Task.Delay(50, Token);
        }

        throw new TimeoutException("the job did not end");
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_set_pages_job_leaves_the_views_live_hash_at_the_hash_it_wrote()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Fresh();
        var before = await ViewLive(host);

        var (result, settled) = await RunJob(host, "/sindri/context/pages", $$"""{"set":[{"name":"Week","id":"week"}],"ifHash":"{{before}}"}""");

        var hash = result.GetProperty("hash").GetString();
        Assert.NotEqual(before, hash, StringComparer.Ordinal);
        Assert.Equal(hash, await ViewLive(host, settled));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_pin_concepts_job_leaves_the_views_live_hash_at_the_hash_it_wrote()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Fresh();
        var before = await ViewLive(host);

        var (result, settled) = await RunJob(host, "/sindri/context/pins", $$"""{"set":[{"name":"Scheduling"}],"ifHash":"{{before}}"}""");

        var hash = result.GetProperty("hash").GetString();
        Assert.NotEqual(before, hash, StringComparer.Ordinal);
        Assert.Equal(hash, await ViewLive(host, settled));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task An_add_repo_job_leaves_the_views_live_hash_at_the_hash_of_the_file_it_wrote()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Fresh();
        var before = await ViewLive(host);

        var (_, settled) = await RunJob(host, "/sindri/setup/repos", """{"path":"/work/demo"}""");

        var written = (await host.LiveHash(Token)).Value;
        Assert.NotEqual(before, written, StringComparer.Ordinal);
        Assert.Equal(written, await ViewLive(host, settled));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_hand_edit_of_the_profile_file_does_not_move_the_views_live_hash_before_the_next_watch_check()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Fresh();
        var before = await ViewLive(host);

        var path = Path.Combine(host.Home, "profile", "profile.yaml");
        await File.AppendAllTextAsync(path, "services:\n  heartbeat: { every: 5s }\n", Token);

        Assert.NotEqual(before, (await host.LiveHash(Token)).Value, StringComparer.Ordinal);   // the file changed
        Assert.Equal(before, await ViewLive(host));   // the view waits for the watch
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_sindri_write_after_a_hand_edit_lists_both_changes_so_the_hand_edit_is_never_approved_unseen()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Fresh();
        var path = Path.Combine(host.Home, "profile", "profile.yaml");
        await File.AppendAllTextAsync(path, "services:\n  heartbeat: { every: 5s }\n", Token);
        var before = (await host.LiveHash(Token)).Value;   // what the app sends as ifHash: the file as it is now

        var (result, settled) = await RunJob(host, "/sindri/context/pages", $$"""{"set":[{"name":"Week","id":"week"}],"ifHash":"{{before}}"}""");

        var hash = result.GetProperty("hash").GetString();
        Assert.Equal(hash, await ViewLive(host, settled), StringComparer.Ordinal);
        var paths = await ViewChangedPaths(host, settled);
        Assert.Contains(paths, static p => p.StartsWith("services", StringComparison.Ordinal));
        Assert.Contains(paths, static p => p.Contains("pages", StringComparison.Ordinal));
    }
}
