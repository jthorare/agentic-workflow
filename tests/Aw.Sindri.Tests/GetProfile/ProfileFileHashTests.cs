using System.Net;
using System.Text.Json;
using Aw.Context.Testing.Fixtures;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;
using Aw.Kernel.Services;
using Aw.Sindri.Features.CheckProfile;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.GetProfile;

/// <summary>GET /sindri/profile's 'fileHash' (#1826): the hash of the profile file as it is on disk when asked, the one a write's 'ifHash' is compared with, so it moves with a hand edit at once while 'live' waits for a check.</summary>
public sealed class ProfileFileHashTests : IDisposable
{
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string ProfilePath(GoldenHost host) => Path.Combine(host.Home, "profile", "profile.yaml");

    private async Task<GoldenHost> Checked()
    {
        var host = await ContextHosts.Approved(_repo, sessions: true, Token);
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease
        using (host.App.Services.GetRequiredService<ICallerContextAccessor>().EnterService("profile-watch"))
        {
            await using var scope = host.App.Services.CreateAsyncScope();
            Assert.True((await scope.ServiceProvider.GetRequiredService<IService<CheckProfileRequest, CheckProfileResponse>>().Handle(new CheckProfileRequest(grant), Token)).IsSuccess);
        }

        return host;
    }

    private static async Task<JsonElement> Profile(GoldenHost host)
    {
        await ViewScenarios.CatchUp(host, Token);
        var answer = await host.Http.Ask(Get("/sindri/profile", Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, answer.Status);
        return JsonDocument.Parse(answer.Body).RootElement.Clone();
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_hand_edit_shows_in_fileHash_at_once_while_live_keeps_the_hash_of_the_last_check()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Checked();
        var before = await Profile(host);
        var checkedHash = (await host.LiveHash(Token)).Value;
        Assert.Equal((checkedHash, checkedHash), (before.GetProperty("live").GetString(), before.GetProperty("fileHash").GetString()));

        await File.AppendAllTextAsync(ProfilePath(host), "services:\n  heartbeat: { every: 5s }\n", Token);
        var edited = (await host.LiveHash(Token)).Value;
        var after = await Profile(host);

        Assert.NotEqual(checkedHash, edited, StringComparer.Ordinal);
        Assert.Equal((checkedHash, edited), (after.GetProperty("live").GetString(), after.GetProperty("fileHash").GetString()));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_profile_file_gives_a_null_fileHash()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Checked();
        File.Delete(ProfilePath(host));

        var answer = await Profile(host);

        Assert.Equal(JsonValueKind.Null, answer.GetProperty("fileHash").ValueKind);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_fileHash_equals_the_hash_a_set_pages_dry_run_reports_on_the_same_file()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Checked();
        await File.AppendAllTextAsync(ProfilePath(host), "services:\n  heartbeat: { every: 5s }\n", Token);

        var started = await host.Http.Ask(Post("/sindri/context/pages", """{"set":[{"name":"Week","id":"week"}],"dryRun":true}""", Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, started.Status);
        var id = JsonDocument.Parse(started.Body).RootElement.GetProperty("jobId").GetString();
        JsonElement view = default;
        for (var i = 0; i < 400; i++)
        {
            await ViewScenarios.CatchUp(host, Token);
            view = JsonDocument.Parse((await host.Http.Ask(Get($"/sindri/jobs/{id}", Human(host.Token.Value)))).Body).RootElement.Clone();
            if (view.GetProperty("state").GetString() != "running")
            {
                break;
            }

            await Task.Delay(50, Token);
        }

        Assert.Equal("succeeded", view.GetProperty("state").GetString());
        var dryRun = view.GetProperty("result").GetProperty("hash").GetString();
        Assert.Equal(dryRun, (await Profile(host)).GetProperty("fileHash").GetString());
    }
}
