using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.Linear;

/// <summary>What an index tick over the stateful fake Linear keeps for the Linear hub's pickers beyond the lists themselves: a team's name and sync time, the members who left, a label's colour, and the workspace's URL key.</summary>
[Trait("Lane", "integration")]
public sealed class LinearWorkspaceTests : IAsyncLifetime
{
    private LinearHubWorld _world = null!;

    public async ValueTask InitializeAsync() =>
        _world = await LinearHubWorld.Start(seed => seed
            .Team("ENG", "Engineering")
            .Member("Ada Fixture").Member("Former Member", active: false)
            .Label("bug", "ENG", "#eb5757"));

    public ValueTask DisposeAsync() => _world.DisposeAsync();

    private ContextScope Session() => _world.Scope with { Caller = new ContextCaller(ContextCallerKind.Session, ContextRepoName.From("split-panes"), ContextClient.Mcp) };

    private async Task<Result<T>> Call<T>(Func<IContextIssues, ContextScope, ValueTask<Result<T>>> call, ContextScope? scope = null)
    {
        await using var provider = _world.Provider.CreateAsyncScope();
        return await call(provider.ServiceProvider.GetRequiredService<IContextEngine>(), scope ?? _world.Scope);
    }

    [Fact]
    public async Task A_teams_name_and_the_time_of_its_sync_read_back()
    {
        _ = await _world.Tick();

        var lookups = (await Call((i, s) => i.IssueLookups("ENG", s, TestContext.Current.CancellationToken))).Value;

        Assert.Equal("Engineering", lookups?.Name);
        Assert.Equal(LinearHubWorld.Now, lookups?.SyncedAt);
    }

    [Fact]
    public async Task A_picked_team_no_tick_has_synced_reads_as_empty_lists_and_an_unpicked_team_as_null()
    {
        var picked = (await Call((i, s) => i.IssueLookups("ENG", s, TestContext.Current.CancellationToken))).Value;
        var unpicked = (await Call((i, s) => i.IssueLookups("OPS", s, TestContext.Current.CancellationToken))).Value;

        Assert.NotNull(picked);
        Assert.Equal("ENG", picked.TeamKey);
        Assert.Null(picked.TeamId);
        Assert.Null(picked.Name);
        Assert.Null(picked.SyncedAt);
        Assert.Empty(picked.States);
        Assert.Empty(picked.Cycles);
        Assert.Empty(picked.Members);
        Assert.Empty(picked.Labels);
        Assert.Null(unpicked);
    }

    [Fact]
    public async Task A_team_row_from_before_the_migration_reads_without_a_name_or_a_sync_time()
    {
        _ = await _world.Tick();
        _world.Sql("UPDATE linear_team SET name = '', synced_at = NULL WHERE key = 'ENG'");

        var lookups = (await Call((i, s) => i.IssueLookups("ENG", s, TestContext.Current.CancellationToken))).Value;

        Assert.NotNull(lookups);
        Assert.Null(lookups.Name);
        Assert.Null(lookups.SyncedAt);
    }

    [Fact]
    public async Task A_picked_team_asked_for_in_lower_case_is_answered_under_the_profiles_spelling()
    {
        var lookups = (await Call((i, s) => i.IssueLookups("eng", s, TestContext.Current.CancellationToken))).Value;

        Assert.Equal("ENG", lookups?.TeamKey);
        Assert.Null(lookups?.TeamId);
    }

    [Fact]
    public async Task A_member_who_left_is_kept_with_active_false()
    {
        _ = await _world.Tick();

        var members = (await Call((i, s) => i.IssueLookups("ENG", s, TestContext.Current.CancellationToken))).Value?.Members ?? [];

        Assert.False(members.Single(static m => string.Equals(m.Name, "Former Member", StringComparison.Ordinal)).Active);
        Assert.True(members.Single(static m => string.Equals(m.Name, "Ada Fixture", StringComparison.Ordinal)).Active);
    }

    [Fact]
    public async Task A_labels_colour_reads_back()
    {
        _ = await _world.Tick();

        var labels = (await Call((i, s) => i.IssueLookups("ENG", s, TestContext.Current.CancellationToken))).Value?.Labels ?? [];

        Assert.Equal("#eb5757", Assert.Single(labels).Color);
    }

    [Fact]
    public async Task The_workspace_url_key_is_null_before_the_first_tick_and_the_workspaces_after_it()
    {
        var before = await Call((i, s) => i.WorkspaceUrlKey(s, TestContext.Current.CancellationToken));
        _ = await _world.Tick();
        var after = await Call((i, s) => i.WorkspaceUrlKey(s, TestContext.Current.CancellationToken));

        Assert.True(before.IsSuccess);
        Assert.Null(before.Value);
        Assert.Equal("fixture", after.Value);
    }

    [Fact]
    public async Task The_workspace_url_key_is_closed_to_a_session()
    {
        _ = await _world.Tick();

        var refused = await Call((i, s) => i.WorkspaceUrlKey(s, TestContext.Current.CancellationToken), Session());

        Assert.True(refused.IsError);
        Assert.Equal(ContextErrors.SessionsDisabled.Code, refused.Error.Code);
    }
}
