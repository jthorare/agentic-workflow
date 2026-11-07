using Aw.Kernel;
using Aw.Sindri.Core.Linear;

namespace Aw.Sindri.Tests.LinearReads;

/// <summary>The in-memory <see cref="ILinearSideFacts"/>: one team's lookups and a list of changes, or an error; records what each read asked for.</summary>
internal sealed class FakeLinearSideFacts : ILinearSideFacts
{
    private readonly Dictionary<string, LinearLookupsResponse> _teams = new(StringComparer.Ordinal);
    private readonly List<LinearSinceItem> _items = [];
    private Error? _failure;
    private bool _noViewer;

    internal List<string> AskedTeams { get; } = [];

    internal List<DateTimeOffset> AskedAfter { get; } = [];

    internal FakeLinearSideFacts WithTeam(LinearLookupsResponse lookups)
    {
        _teams[lookups.Team.Key] = lookups;
        return this;
    }

    internal FakeLinearSideFacts WithItems(params LinearSinceItem[] items)
    {
        _items.AddRange(items);
        return this;
    }

    internal FakeLinearSideFacts WithoutViewer()
    {
        _noViewer = true;
        return this;
    }

    internal FakeLinearSideFacts Failing(Error error)
    {
        _failure = error;
        return this;
    }

    public ValueTask<Result<LinearLookupsResponse>> Lookups(string team, CancellationToken cancellationToken)
    {
        AskedTeams.Add(team);
        Result<LinearLookupsResponse> answer = _failure is { } failure
            ? failure
            : _teams.TryGetValue(team, out var found) ? found : new LinearLookupsResponse(new LinearTeamRef(Uuid: null, team, Name: null), SyncedAt: null, [], [], [], []);
        return ValueTask.FromResult(answer);
    }

    public ValueTask<Result<IReadOnlyList<LinearSinceItem>?>> Since(DateTimeOffset after, CancellationToken cancellationToken)
    {
        AskedAfter.Add(after);
        var answer = _failure is { } failure
            ? failure
            : Results.Success<IReadOnlyList<LinearSinceItem>?>(_noViewer ? null : [.. _items.Where(i => i.At > after).OrderBy(static i => i.At)]);
        return ValueTask.FromResult(answer);
    }
}
