using Aw.Kernel.Data;
using Aw.Meta.Core;
using Aw.Meta.Core.Delegations;
using Aw.Meta.Infrastructure.Sqlite;

namespace Aw.Meta.Tests.Cli;

/// <summary>delegate --drop end to end over a real meta.db: the exit codes, the printed lines and the usage errors (#4018).</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class DelegateDropCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private async Task Ready()
    {
        _ = await _cli.Run("team-init", "--team", "t1", "--repo", "/r", "--lanes", "meta,api");
        _ = await _cli.Run("role-set", "--session", "s-meta", "--name", "t1-meta");
    }

    // The delegation of #77 to lane api, written into the team's meta.db the way delegate stores it.
    private async Task Delegated()
    {
        var text = DelegationBook.Add(stored: null, new Delegation(77, "https://github.com/o/r/issues/77", "Dark theme", "api", "t1-api", _cli.Clock.GetUtcNow()));
        var connections = new SqliteConnectionFactory(MetaDatabase.OptionsFor(_cli.File("meta/teams/t1")));
        await using var connection = (await connections.Open(TestContext.Current.CancellationToken)).Value;
        await using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO kv (key, value) VALUES ($key, $value)";
        _ = insert.Parameters.AddWithValue("$key", KvKeys.Delegations);
        _ = insert.Parameters.AddWithValue("$value", text);
        _ = await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Drop_of_a_delegated_issue_exits_0_and_names_the_lane_and_a_second_drop_exits_1()
    {
        await Ready();
        await Delegated();

        var dropped = await _cli.Run("delegate", "--drop", "77");
        var again = await _cli.Run("delegate", "--drop", "77");

        Assert.Equal(0, dropped.Exit);
        Assert.Contains("dropped the delegation of #77 to lane api", dropped.Out, StringComparison.Ordinal);
        Assert.Equal(1, again.Exit);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Drop_of_an_issue_with_no_delegation_exits_1_and_says_a_delegate_still_delivering_may_record_it()
    {
        await Ready();

        var none = await _cli.Run("delegate", "--drop", "5");

        Assert.Equal(1, none.Exit);
        Assert.Contains("no delegation of #5 recorded: nothing dropped", none.Out, StringComparison.Ordinal);
        Assert.Contains("run aw-meta delegate --drop 5 again", none.Out, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("--drop x", "--drop must be an issue number")]
    [InlineData("--drop 0", "--drop is an issue number, 1 or more")]
    [InlineData("--drop 5 --lane api", "--drop takes no other option, got --lane")]
    [InlineData("--drop 5 --issue 5", "--drop takes no other option, got --issue")]
    [InlineData("--drop 5 --label bug", "--drop takes no other option, got --label")]
    public async Task A_bad_drop_is_a_usage_error_exit_2_and_drops_nothing(string args, string problem)
    {
        await Ready();
        await Delegated();

        var refused = await _cli.Run(["delegate", .. args.Split(' ')]);
        var kept = await _cli.Run("delegated");

        Assert.Equal(2, refused.Exit);
        Assert.StartsWith("AW-META-001: " + problem, refused.Err, StringComparison.Ordinal);
        Assert.Contains("#77", kept.Out, StringComparison.Ordinal);
    }

    public void Dispose() => _cli.Dispose();
}
