using System.Globalization;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sandbox.Denials;

/// <summary>Which item and repo a launch belongs to, read from the ledger (sandbox spec §3.6): 'launch.started' names the item, 'item.created' the item's repo.</summary>
[Lane(TestLane.Unit)]
public sealed class LedgerLaunchOwnersTests
{
    private static readonly ItemId _web = A.FirstItemId;
    private static readonly ItemId _bare = ItemId.From("ITM-00000000000000000000000000000002");

    private readonly SliceHarness _slice = new();
    private LeaseGrant? _grant;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_launch_has_its_item_and_that_items_repo()
    {
        _grant = await _slice.HoldLease();
        await Created(_web, RepoName.From("web"));
        await Created(_bare, repo: null);
        var onWeb = await Launch(1, _web);
        var onBare = await Launch(2, _bare);

        var owners = await new LedgerLaunchOwners(_slice.Store, _slice.Events).Read(Token);

        Expect.Equal(2, owners.Count);
        Expect.Equal((_web, "web"), (owners[onWeb].Item, owners[onWeb].Repo?.Value));
        Expect.Equal(_bare, owners[onBare].Item);
        Expect.True(owners[onBare].Repo is null);
    }

    [Fact]
    public async Task A_launch_the_ledger_never_saw_has_no_owner()
    {
        _grant = await _slice.HoldLease();

        var owners = await new LedgerLaunchOwners(_slice.Store, _slice.Events).Read(Token);

        Assert.Empty(owners);
    }

    private ValueTask Created(ItemId item, RepoName? repo) =>
        Append(EventTypes.ItemCreated, new ItemCreated(_slice.Ids.New<CommandId>(), item, "work", repo, Link: null, Body: null, Owner: null), $"item.created:{item.Value}", item.Value);

    private async ValueTask<LaunchId> Launch(int n, ItemId item)
    {
        var launch = LaunchId.From(string.Create(CultureInfo.InvariantCulture, $"LCH-{n:D32}"));
        await Append(EventTypes.LaunchStarted,
            new LaunchStarted(launch, item, LaunchMode.Open, AgentId.From("claude-code"), SessionId: null, "sindri", new LaunchViewer("warp", "Warp", ViewerOutcomeKind.Opened, Code: null, Detail: null),
                ["tmux"], CatalogSource.Builtin, "sha256:abc"),
            $"launch.outcome:{launch.Value}", item.Value);
        return launch;
    }

    private async ValueTask Append<T>(string type, T payload, string key, string? item)
    {
        var appended = await _slice.Store.Append(_slice.Events.Entry(type, payload, key, item), _grant!, Token);
        Expect.True(appended.IsSuccess);
    }
}
