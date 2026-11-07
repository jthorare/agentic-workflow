using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>What the form shows for an issue: its fields, the pickers' choices from the team's lookups (<see cref="LinearIssueSheetViewModel"/>, continued).</summary>
internal sealed partial class LinearIssueSheetViewModel
{
    // Draws what the form shows for an issue; a refresh or a 409 keeps the owner's edits (keepEdits), an applied save replaces them.
    private void DrawEdit(LinearIssueOne issue, bool keepEdits)
    {
        _issue = issue;
        var gate = issue.Write;
        var code = issue.Removed || gate is null or { Allowed: true } ? null : gate.Code;
        CanEdit = !issue.Removed && gate is { Allowed: true };
        CanConnect = string.Equals(code, NoWriteKey, StringComparison.Ordinal);
        WriteBanner = string.Equals(code, NoWriteKey, StringComparison.Ordinal) ? Labels.LinearWriteAccess
            : string.Equals(code, TeamNotPicked, StringComparison.Ordinal) ? Labels.LinearTeamNotPicked(issue.Team.Key)
            : string.Empty;
        TitleField.Load(JsonValue.Create(issue.Title), keepEdits);
        StateField.Load(JsonValue.Create(issue.State.Id), keepEdits);
        PriorityField.Load(JsonValue.Create(issue.Priority), keepEdits);
        AssigneeField.Load(issue.Assignee is { } a ? JsonValue.Create(a.Id) : null, keepEdits);
        CycleField.Load(issue.Cycle is { } c ? JsonValue.Create(c.Id) : null, keepEdits);
        LabelsField.Load(new JsonArray([.. issue.Labels.Select(l => (JsonNode?)JsonValue.Create(l.Id))]), keepEdits);
        DescriptionField.Load(JsonValue.Create(issue.Description), keepEdits);
        ProjectField.ShowFixed(issue.Project?.Name ?? Labels.LinearFieldNone);
        ApplyOptions();
        OnChanged(nameof(HasChanges));
        OnChanged(nameof(DirtyLine));
        OnChanged(nameof(CanSave));
    }

    // The pickers' entries from the team's lookups, with the issue's own current values always present so a field never shows a blank.
    private void ApplyOptions()
    {
        if (_issue is not { } issue)
        {
            return;
        }

        var team = issue.Team.Key;
        var synced = _lookups is { SyncedAt: not null };
        var lookups = synced ? _lookups : null;

        PriorityField.SetOptions([.. Enumerable.Range(0, 5).Select(p => new LinearChoiceViewModel(JsonValue.Create(p), Labels.LinearPriority(p)))]);

        StateField.SetOptions(With(lookups?.States.OrderBy(s => s.Position).Select(s => new LinearChoiceViewModel(JsonValue.Create(s.Id), s.Name)), first: [], current: (issue.State.Id, issue.State.Name)));
        AssigneeField.SetOptions(With(lookups?.Members.Where(m => m.Active).Select(m => new LinearChoiceViewModel(JsonValue.Create(m.Id), m.Name)), first: [new LinearChoiceViewModel(value: null, name: Labels.LinearUnassigned)], current: issue.Assignee is { } a ? (a.Id, a.Name) : null));
        CycleField.SetOptions(With(lookups?.Cycles.Select(c => new LinearChoiceViewModel(JsonValue.Create(c.Id), c.Name)), first: [new LinearChoiceViewModel(value: null, name: Labels.LinearNoCycle)], current: issue.Cycle is { } c0 ? (c0.Id, c0.Name) : null));
        LabelsField.SetOptions(WithLabels(lookups?.Labels, issue.Labels));

        var (state, members, cycles, labels) = (Reason(team, "states"), Reason(team, "members"), Reason(team, "cycles"), Reason(team, "labels"));
        StateField.SetAvailable(available: synced, unavailableText: state);
        AssigneeField.SetAvailable(available: synced, unavailableText: members);
        CycleField.SetAvailable(available: synced, unavailableText: cycles);
        LabelsField.SetAvailable(available: synced, unavailableText: labels);
        PriorityField.SetAvailable(available: true, unavailableText: string.Empty);
    }

    private string Reason(string team, string what) =>
        _lookups is { SyncedAt: not null } ? string.Empty : _lookupsFailed ? Labels.LinearLookupFailed(team, what) : Labels.LinearLookupNotSynced(team, what);

    private static List<LinearChoiceViewModel> With(IEnumerable<LinearChoiceViewModel>? offered, List<LinearChoiceViewModel> first, (string Id, string Name)? current)
    {
        var all = first.Concat(offered ?? []).ToList();
        if (current is { } known && !all.Exists(o => o.Value is JsonValue v && v.TryGetValue<string>(out var id) && string.Equals(id, known.Id, StringComparison.Ordinal)))
        {
            all.Add(new LinearChoiceViewModel(JsonValue.Create(known.Id), known.Name));
        }

        return all;
    }

    private static List<LinearChoiceViewModel> WithLabels(IReadOnlyList<LinearLabel>? offered, IReadOnlyList<LinearLabel> current)
    {
        var all = (offered ?? []).Concat(current.Where(c => offered is null || !offered.Any(o => string.Equals(o.Id, c.Id, StringComparison.Ordinal)))).ToList();
        return [.. all.Select(l => new LinearChoiceViewModel(JsonValue.Create(l.Id), l.Name))];
    }

    // The team's choices are read once per opened issue (not again on a refresh); a failed read is tried again on the next read.
    private async Task LoadLookups(LinearIssueOne issue, CancellationToken cancellationToken)
    {
        if (!CanEdit || _lookups is not null || _url is not { } url)
        {
            return;
        }

        var epoch = _epoch;
        var answer = await Api.LinearLookups(url, issue.Team.Key, cancellationToken).ConfigureAwait(true);
        if (epoch != _epoch)
        {
            return;
        }

        (_lookups, _lookupsFailed) = (answer.IsSuccess ? answer.Value : null, !answer.IsSuccess);
        ApplyOptions();
    }

    // The sheet moved to another issue: nothing of the last one's form is kept.
    private void ClearEdit()
    {
        _epoch++;
        (_issue, _lookups, _sent, _leaveTo, _lookupsFailed) = (null, null, null, null, false);
        (CanEdit, CanConnect, IsSaving, IsUnknown, HasConflict, IsConfirmingDiscard) = (false, false, false, false, false, false);
        (ConflictRows, WriteBanner, StatusLine, ErrorBanner) = ([], string.Empty, string.Empty, string.Empty);
        foreach (var field in Editable)
        {
            field.Load(field.IsText ? JsonValue.Create(string.Empty) : null, keepEdit: false);
            field.SetLocked(locked: false);
        }
    }
}
