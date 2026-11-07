using Aw.Context.Core.Linear;
using Aw.Context.Testing.Linear;

namespace Aw.Context.Tests.Linear;

/// <summary>The pickers' data read from the stateful fake Linear: per team its workflow states, cycles (current, next, last three), members and labels (linear hub spec 2026-11-04 §4.2).</summary>
[Trait("Lane", "integration")]
public sealed class LinearLookupsReaderTests : IAsyncLifetime
{
    private FakeLinear _linear = null!;
    private LinearSourceHarness _harness = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // why: the harness clock reads 2026-10-12, which falls in the third of the five ENG cycles below
        _linear = await FakeLinear.Start(seed => seed
            .Team("ENG").Team("OPS")
            .Member("Ada Fixture")
            .Label("bug").Label("ops-only", "OPS")
            .Cycle("C1", "ENG", At(9, 14), At(9, 28))
            .Cycle("C2", "ENG", At(9, 28), At(10, 12))
            .Cycle("C3", "ENG", At(10, 12), At(10, 26))
            .Cycle("C4", "ENG", At(10, 26), At(11, 9))
            .Cycle("C5", "ENG", At(11, 9), At(11, 23))
            .Cycle("O1", "OPS", At(10, 1), At(10, 31)));
        _harness = new LinearSourceHarness(_linear.Url, _linear.Token, "ENG");
    }

    public async ValueTask DisposeAsync()
    {
        _harness.Dispose();
        await _linear.DisposeAsync();
    }

    private static DateTimeOffset At(int month, int day) => new(2026, month, day, 0, 0, 0, TimeSpan.Zero);

    private async Task<IReadOnlyList<LinearTeamLookups>> Read(params string[] teams)
    {
        var settings = LinearSettings.Create(_linear.Url.ToString(), LinearSourceHarness.Pointer, teams).Value;
        var read = await _harness.Sources.CreateLookupsReader(settings).Read(Ct);
        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : null);
        return read.Value;
    }

    [Fact]
    public async Task A_team_gets_its_states_in_order_with_their_types()
    {
        var team = Assert.Single(await Read("ENG"));

        Assert.Equal("ENG", team.TeamKey);
        Assert.Equal([("Backlog", "backlog"), ("Todo", "unstarted"), ("In Progress", "started"), ("Done", "completed"), ("Canceled", "canceled")], team.States.Select(static s => (s.Name, s.Type)));
        Assert.Equal(_linear.TeamId("ENG"), team.TeamId?.Value);
        Assert.Equal(_linear.StateId("ENG", "Done"), team.States.Single(static s => s.Name == "Done").Id.Value);
    }

    [Fact]
    public async Task A_team_gets_the_current_cycle_the_next_and_the_last_three()
    {
        var team = Assert.Single(await Read("ENG"));

        Assert.Equal(["C1", "C2", "C3", "C4"], team.Cycles.Select(static c => c.Name), StringComparer.Ordinal);
        Assert.Equal([1, 2, 3, 4], team.Cycles.Select(static c => c.Number));
    }

    [Fact]
    public async Task A_team_gets_the_members_and_its_own_and_workspace_labels_but_not_another_teams()
    {
        var eng = Assert.Single(await Read("ENG"));
        var ops = Assert.Single(await Read("OPS"));

        Assert.Equal(["Fixture Viewer", "Ada Fixture"], eng.Members.Select(static m => m.Name), StringComparer.Ordinal);
        Assert.Equal(["bug"], eng.Labels.Select(static l => l.Name), StringComparer.Ordinal);
        Assert.Equal(["bug", "ops-only"], ops.Labels.Select(static l => l.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task With_no_team_named_every_team_the_key_sees_is_read()
    {
        var teams = await Read();

        Assert.Equal(["ENG", "OPS"], teams.Select(static t => t.TeamKey).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_refused_key_is_an_error_not_an_empty_list()
    {
        _linear.Switches.RevokeKey = true;
        var settings = LinearSettings.Create(_linear.Url.ToString(), LinearSourceHarness.Pointer, ["ENG"]).Value;

        var read = await _harness.Sources.CreateLookupsReader(settings).Read(Ct);

        Assert.True(read.IsError);
    }
}
