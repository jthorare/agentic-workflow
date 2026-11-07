using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Xunit;

namespace Aw.Context.Testing.Records;

/// <summary>
/// The contract every <see cref="ILinearLookupStore"/> honours: a team's lists read back whole, a second replace swaps the lists (rows Linear dropped are gone), and teams don't mix.
/// A team is read back by its key; the contract's teams are 'ENG' and 'OPS'.
/// </summary>
/// <typeparam name="TStore">The implementation.</typeparam>
public abstract class LinearLookupStoreContract<TStore>
    where TStore : ILinearLookupStore
{
    private static readonly string _owner = typeof(TStore).Name;
    private static readonly ExternalId _eng = ExternalId.From("00000000-0000-0000-0000-00000000e001");
    private static readonly ExternalId _ops = ExternalId.From("00000000-0000-0000-0000-00000000e002");

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract TStore Create();

    private static async Task<LinearTeamLookups?> Read(TStore store, ExternalId teamId) =>
        ContractCheck.Ok(_owner, await store.Team(teamId == _eng ? "ENG" : "OPS", CancellationToken.None));

    private static LinearTeamLookups Team(ExternalId id, string key, params string[] names) => new(
        id,
        key,
        [.. names.Select((n, i) => new LinearLookupState(Id("a", i), "State " + n, "started", i))],
        [.. names.Select((n, i) => new LinearLookupCycle(Id("b", i), i + 1, "Cycle " + n, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(14 * i), new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero).AddDays(14 * i)))],
        [.. names.Select((n, i) => new LinearLookupMember(Id("c", i), "Member " + n, DisplayName: null, Active: i % 2 == 0))],
        [.. names.Select((n, i) => new LinearLookupLabel(Id("d", i), "Label " + n, "#eb575" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)))],
        "Team " + key,
        new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));

    private static ExternalId Id(string kind, int n) => ExternalId.From($"00000000-0000-0000-0000-{kind}000000000{n:x3}");

    /// <summary>A team never stored has no rows.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_team_never_stored_reads_as_null() =>
        ContractCheck.That(_owner, await Read(Create(), _eng) is null, "an unknown team reads as null");

    /// <summary>The lists of a team read back whole.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_replaced_team_reads_back_whole()
    {
        var store = Create();
        var team = Team(_eng, "ENG", "one", "two");
        _ = ContractCheck.Ok(_owner, await store.ReplaceTeam(team, CancellationToken.None));
        var read = await Read(store, _eng);
        ContractCheck.That(_owner, read is not null, "the team reads back");
        ContractCheck.That(_owner, read!.States.SequenceEqual(team.States) && read.Cycles.SequenceEqual(team.Cycles) && read.Members.SequenceEqual(team.Members) && read.Labels.SequenceEqual(team.Labels), "every list reads back whole");
    }

    /// <summary>A second replace swaps the lists: rows Linear no longer lists are gone.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_second_replace_drops_the_rows_linear_no_longer_lists()
    {
        var store = Create();
        _ = ContractCheck.Ok(_owner, await store.ReplaceTeam(Team(_eng, "ENG", "one", "two", "three"), CancellationToken.None));
        var later = Team(_eng, "ENG", "one");
        _ = ContractCheck.Ok(_owner, await store.ReplaceTeam(later, CancellationToken.None));
        var read = await Read(store, _eng);
        ContractCheck.That(_owner, read is not null && read.States.Count == 1 && read.Cycles.Count == 1 && read.Members.Count == 1 && read.Labels.Count == 1, "only the later rows remain");
    }

    /// <summary>Replacing one team leaves another's rows as they were.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Replacing_one_team_leaves_the_other_alone()
    {
        var store = Create();
        var ops = Team(_ops, "OPS", "ops");
        _ = ContractCheck.Ok(_owner, await store.ReplaceTeam(ops, CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.ReplaceTeam(Team(_eng, "ENG", "one", "two"), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.ReplaceTeam(Team(_eng, "ENG"), CancellationToken.None));
        var read = await Read(store, _ops);
        ContractCheck.That(_owner, read is not null && read.States.SequenceEqual(ops.States) && read.Labels.SequenceEqual(ops.Labels), "the other team keeps its rows");
        ContractCheck.That(_owner, await Read(store, _eng) is { States.Count: 0, Labels.Count: 0 }, "the replaced team now has empty lists");
    }

    /// <summary>A stored team reads back by its key, in the order the pickers show it, and an unknown key reads as null.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_team_reads_back_by_its_key_in_picker_order()
    {
        var store = Create();
        var team = Team(_eng, "ENG", "one", "two");
        var reversed = team with { States = [.. team.States.Reverse()], Cycles = [.. team.Cycles.Reverse()], Members = [.. team.Members.Reverse()], Labels = [.. team.Labels.Reverse()] };
        _ = ContractCheck.Ok(_owner, await store.ReplaceTeam(reversed, CancellationToken.None));

        var read = ContractCheck.Ok(_owner, await store.Team("ENG", CancellationToken.None));

        ContractCheck.That(_owner, read is not null && read.TeamId == _eng && string.Equals(read.TeamKey, "ENG", StringComparison.Ordinal), "the team reads back by its key");
        ContractCheck.That(_owner, read!.States.SequenceEqual(team.States) && read.Cycles.SequenceEqual(team.Cycles) && read.Members.SequenceEqual(team.Members) && read.Labels.SequenceEqual(team.Labels), "the lists read in picker order");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Team("OPS", CancellationToken.None)) is null, "an unknown key reads as null");
    }

    /// <summary>Two teams are read apart by their keys.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Two_teams_read_apart_by_their_keys()
    {
        var store = Create();
        _ = ContractCheck.Ok(_owner, await store.ReplaceTeam(Team(_eng, "ENG", "one", "two"), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.ReplaceTeam(Team(_ops, "OPS", "ops"), CancellationToken.None));

        var ops = ContractCheck.Ok(_owner, await store.Team("OPS", CancellationToken.None));
        var eng = ContractCheck.Ok(_owner, await store.Team("ENG", CancellationToken.None));

        ContractCheck.That(_owner, ops is { States.Count: 1 } && eng is { States.Count: 2 }, "each key reads its own team's rows");
    }

    /// <summary>The workspace's URL key reads as null until it is stored, then reads back, and a second write replaces it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_workspace_url_key_reads_null_then_what_was_stored()
    {
        var store = Create();
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.WorkspaceUrlKey(CancellationToken.None)) is null, "no key before the first write");
        _ = ContractCheck.Ok(_owner, await store.ReplaceWorkspaceUrlKey("acme", CancellationToken.None));
        ContractCheck.That(_owner, string.Equals(ContractCheck.Ok(_owner, await store.WorkspaceUrlKey(CancellationToken.None)), "acme", StringComparison.Ordinal), "the key reads back");
        _ = ContractCheck.Ok(_owner, await store.ReplaceWorkspaceUrlKey("beta", CancellationToken.None));
        ContractCheck.That(_owner, string.Equals(ContractCheck.Ok(_owner, await store.WorkspaceUrlKey(CancellationToken.None)), "beta", StringComparison.Ordinal), "a second write replaces it");
    }
}
