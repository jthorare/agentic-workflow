using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>Reads the states, cycles, members and labels of the profile's teams in one operation (spec 2026-11-04 §4.2): the teams, each with its four lists.</summary>
/// <param name="client">The Linear client.</param>
/// <param name="deadline">How long each call gets.</param>
/// <param name="clock">The clock the current cycle is judged by.</param>
internal sealed class LinearLookupsReader(LinearClient client, TimeSpan deadline, TimeProvider clock)
{
    private const int MaxTeams = 50;

    /// <summary>Reads every team the settings name (every team the key sees when they name none).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One entry per team, or the source's error.</returns>
    public async ValueTask<Result<IReadOnlyList<LinearTeamLookups>>> Read(CancellationToken cancellationToken)
    {
        var teams = client.Settings.Teams;
        var list = new LinearList<LinearLookupsData, LinearLookupsTeam>("Lookups", teams.Count > 0 ? LinearFilters.TeamKeys(teams) : null, MaxTeams, static d => d.Teams);
        var started = clock.GetTimestamp();
        var nodes = await client.Pages(list, () => deadline - clock.GetElapsedTime(started), cancellationToken).ConfigureAwait(false);
        if (nodes.IsError)
        {
            return nodes.Error;
        }

        var now = clock.GetUtcNow();
        return nodes.Value.Select(t => Team(t, now)).OfType<LinearTeamLookups>().ToList();
    }

    // A team whose id isn't usable is left out; so is any row that has none (Linear sends uuids, so neither happens against Linear itself)
    private static LinearTeamLookups? Team(LinearLookupsTeam team, DateTimeOffset now) => LinearExternalIds.Of(team.Id) is not { } id
        ? null
        : new LinearTeamLookups(id, team.Key, States(team), LinearCycles.Pick(Cycles(team), now), Members(team), Labels(team));

    private static List<LinearLookupState> States(LinearLookupsTeam team) =>
        [.. (team.States?.Nodes ?? []).Select(static s => Row(s.Id, i => new LinearLookupState(i, s.Name, s.Type, s.Position))).OfType<LinearLookupState>().OrderBy(static s => s.Position)];

    private static IEnumerable<LinearLookupCycle> Cycles(LinearLookupsTeam team) =>
        (team.Cycles?.Nodes ?? []).Select(static c => Row(c.Id, i => new LinearLookupCycle(i, c.Number, c.Name, c.StartsAt, c.EndsAt))).OfType<LinearLookupCycle>();

    private static List<LinearLookupMember> Members(LinearLookupsTeam team) =>
        [.. (team.Members?.Nodes ?? []).Where(static m => m.Active).Select(static m => Row(m.Id, i => new LinearLookupMember(i, m.Name, m.DisplayName))).OfType<LinearLookupMember>()];

    private static List<LinearLookupLabel> Labels(LinearLookupsTeam team) =>
        [.. (team.Labels?.Nodes ?? []).Where(static l => !string.IsNullOrEmpty(l.Name)).Select(static l => Row(l.Id, i => new LinearLookupLabel(i, l.Name ?? string.Empty))).OfType<LinearLookupLabel>()];

    private static T? Row<T>(string id, Func<ExternalId, T> make)
        where T : class => LinearExternalIds.Of(id) is { } typed ? make(typed) : null;
}
