using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>A team's lookups in the dialog's pickers (<see cref="NewLinearIssueViewModel"/>, continued): read once per team per opening, and a team that has not synced offers nothing.</summary>
internal sealed partial class NewLinearIssueViewModel
{
    // A team pick is the start of a different issue's choices: the team's own pickers go empty until its lookups are in.
    private void ClearLookups()
    {
        (States, Assignees, LabelChoices, Cycles) = ([], [], [], []);
        (State, Assignee, Cycle) = (null, null, null);
        NotSyncedNote = string.Empty;
        OnChanged(nameof(HasLookups));
    }

    private async Task ReadLookups(LinearChoiceViewModel team)
    {
        if (Id(team) is not { } key)
        {
            return;
        }

        if (!_lookups.TryGetValue(key, out var lookups))
        {
            if (url() is not { } address)
            {
                return;
            }

            var read = await api.LinearLookups(address, key, _opened).ConfigureAwait(true);
            if (!ReferenceEquals(Team, team))
            {
                return;   // another team was picked meanwhile
            }

            if (read.IsError)
            {
                NotSyncedNote = Labels.LinearLookupFailed(key, "states");   // not remembered: the next pick reads again
                return;
            }

            lookups = _lookups[key] = read.Value;
        }
        else if (!ReferenceEquals(Team, team))
        {
            return;
        }

        Fill(key, lookups);
    }

    // Rebuilds the pickers from the lookups, keeping what the draft had picked (by id) when it is still offered.
    private void Fill(string team, LinearLookups lookups)
    {
        var (state, assignee, cycle) = (Id(State), Id(Assignee), Id(Cycle));
        var ticked = LabelChoices.Where(l => l.IsTicked).Select(Id).OfType<string>().ToHashSet(StringComparer.Ordinal);
        if (lookups.SyncedAt is null)
        {
            ClearLookups();
            NotSyncedNote = Labels.LinearNewNotSynced(team);
            return;
        }

        States = [new LinearChoiceViewModel(value: null, Labels.LinearNewDefaultState), .. lookups.States.OrderBy(s => s.Position).Select(s => new LinearChoiceViewModel(JsonValue.Create(s.Id), s.Name))];
        Assignees = [new LinearChoiceViewModel(value: null, Labels.LinearUnassigned), .. lookups.Members.Where(m => m.Active).Select(m => new LinearChoiceViewModel(JsonValue.Create(m.Id), m.Name))];
        Cycles = [new LinearChoiceViewModel(value: null, Labels.LinearNoCycle), .. lookups.Cycles.Select(c => new LinearChoiceViewModel(JsonValue.Create(c.Id), c.Name))];
        LabelChoices = [.. lookups.Labels.Select(l => new LinearChoiceViewModel(JsonValue.Create(l.Id), l.Name))];
        foreach (var label in LabelChoices.Where(l => Id(l) is { } id && ticked.Contains(id)))
        {
            label.Reset(ticked: true);
        }

        State = Pick(States, state);
        Assignee = Pick(Assignees, assignee);
        Cycle = Pick(Cycles, cycle);
        NotSyncedNote = string.Empty;
        OnChanged(nameof(HasLookups));
    }

    private static LinearChoiceViewModel Pick(IReadOnlyList<LinearChoiceViewModel> choices, string? id) =>
        id is null ? choices[0] : choices.FirstOrDefault(c => string.Equals(Id(c), id, StringComparison.Ordinal)) ?? choices[0];
}
