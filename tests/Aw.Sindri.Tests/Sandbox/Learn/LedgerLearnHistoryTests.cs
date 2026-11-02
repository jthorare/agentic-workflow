using System.Globalization;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sandbox.Learn;

/// <summary>The ledger's reading of a repo's learn history (sandbox spec §3.6, Q13): its learn-mode launches and when the owner ended learn mode.</summary>
[Lane(TestLane.Unit)]
public sealed class LedgerLearnHistoryTests
{
    private static readonly ItemId _web = A.FirstItemId;
    private static readonly ItemId _api = ItemId.From("ITM-00000000000000000000000000000002");

    private readonly SliceHarness _slice = new();
    private LeaseGrant? _grant;
    private int _launches;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<LedgerLearnHistory> Seeded()
    {
        _grant = await _slice.HoldLease();
        await Append(EventTypes.ItemCreated, new ItemCreated(_slice.Ids.New<CommandId>(), _web, "web work", RepoName.From("web"), Link: null, Body: null, Owner: null), $"item.created:{_web.Value}", _web.Value);
        await Append(EventTypes.ItemCreated, new ItemCreated(_slice.Ids.New<CommandId>(), _api, "api work", RepoName.From("api"), Link: null, Body: null, Owner: null), $"item.created:{_api.Value}", _api.Value);
        return new LedgerLearnHistory(_slice.Store, _slice.Events);
    }

    [Fact]
    public async Task Learn_history_counts_learn_runs_per_repo()
    {
        var subject = await Seeded();
        var first = _slice.Clock.GetUtcNow();
        await Launch(_web, NetworkMode.Learn);
        _slice.Clock.Advance(TimeSpan.FromHours(2));
        await Launch(_web, NetworkMode.Learn);
        await Launch(_web, NetworkMode.Enforce);   // not a learn run
        await Launch(_api, NetworkMode.Learn);     // another repo's

        var web = await subject.Read("web", Token);
        var none = await subject.Read("mobile", Token);

        Expect.Equal((first, 2), (web.FirstLearnRun, web.LearnRuns));
        Expect.True(web.EndedAt is null);
        Expect.Equal(0, none.LearnRuns);
        Expect.True(none.FirstLearnRun is null);
    }

    [Fact]
    public async Task Learn_history_sees_an_all_repos_end()
    {
        var subject = await Seeded();
        await Launch(_web, NetworkMode.Learn);
        _slice.Clock.Advance(TimeSpan.FromHours(1));
        var webEnd = _slice.Clock.GetUtcNow();
        await Ended(RepoName.From("web"));
        _slice.Clock.Advance(TimeSpan.FromHours(1));
        var allEnd = _slice.Clock.GetUtcNow();
        await Ended(repo: null);

        var web = await subject.Read("web", Token);
        var api = await subject.Read("api", Token);

        Expect.Equal(allEnd, web.EndedAt);   // the latest of the repo's and the all-repos end
        Expect.Equal(allEnd, api.EndedAt);   // an all-repos end reaches a repo with no end of its own
        Expect.True(webEnd < allEnd);
    }

    [Fact]
    public async Task A_repo_end_does_not_reach_another_repo()
    {
        var subject = await Seeded();
        await Ended(RepoName.From("web"));

        var api = await subject.Read("api", Token);

        Expect.True(api.EndedAt is null);
    }

    private ValueTask Launch(ItemId item, NetworkMode mode)
    {
        var launch = LaunchId.From(string.Create(CultureInfo.InvariantCulture, $"LCH-{++_launches:D32}"));
        var summary = new SandboxSummary(SandboxMechanism.Bubblewrap, "9f2c0a41d8e6", ["/work"], ["model"], mode, InnerState.On, new SandboxLimitsSummary("12GiB", "400%", 2048),
            new SandboxCredentialsSummary(GithubCredentials.None), ModelLoginSource.OwnerCopy);
        return Append(EventTypes.LaunchStarted,
            new LaunchStarted(launch, item, LaunchMode.Open, AgentId.From("claude-code"), SessionId: null, "sindri", new LaunchViewer("warp", "Warp", ViewerOutcomeKind.Opened, Code: null, Detail: null),
                ["tmux"], CatalogSource.Builtin, "sha256:abc", Sandbox: summary),
            $"launch.outcome:{launch.Value}", item.Value);
    }

    private ValueTask Ended(RepoName? repo) =>
        Append(EventTypes.SandboxLearnEnded, new SandboxLearnEnded(_slice.Clock.GetUtcNow(), repo), string.Create(CultureInfo.InvariantCulture, $"sandbox.learn-ended:{repo?.Value ?? "all"}:{_slice.Clock.GetUtcNow().UtcTicks}"), item: null);

    private async ValueTask Append<T>(string type, T payload, string key, string? item)
    {
        var appended = await _slice.Store.Append(_slice.Events.Entry(type, payload, key, item), _grant ?? await GrantFirst(), Token);
        Expect.True(appended.IsSuccess);
    }

    private async Task<LeaseGrant> GrantFirst() => _grant = await _slice.HoldLease();
}
