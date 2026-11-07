using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Linear;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>Sending a save and drawing its answer: applied, conflict, unknown, rate limit and the refusals (<see cref="LinearIssueSheetViewModel"/>, continued).</summary>
internal sealed partial class LinearIssueSheetViewModel
{
    private async Task Send(LinearChangeBody body, CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return;
        }

        var epoch = _epoch;
        Sending(body);
        await using var checking = _clock.CreateTimer(_ => _ui.PostOrInline(() => Checking(epoch)), state: null, _checkAfter, Timeout.InfiniteTimeSpan).ConfigureAwait(true);
        var answer = await Api.LinearChange(url, Key, body.Opened, body.Set, cancellationToken).ConfigureAwait(true);
        if (epoch != _epoch)
        {
            return;   // another issue took the sheet over meanwhile
        }

        IsSaving = false;
        if (!answer.IsSuccess)
        {
            Refused(answer.Error);
        }
        else
        {
            Answered(answer.Value);
        }
    }

    private void Sending(LinearChangeBody body)
    {
        _sent = body;
        (ConflictRows, HasConflict, IsUnknown, IsSaving) = ([], false, false, true);
        (StatusLine, ErrorBanner) = (string.Empty, string.Empty);
        Lock(locked: true);
    }

    private void Answered(LinearWriteOutcome outcome)
    {
        switch (outcome)
        {
            case LinearChangeApplied applied:
                Applied(applied);
                break;
            case LinearChangeConflict conflict:
                Conflicted(conflict);
                break;
            case LinearWriteUnknown:
                IsUnknown = true;
                StatusLine = Labels.LinearUnknown;
                Lock(locked: true);
                break;
            case LinearWriteRateLimited limited:
                ErrorBanner = Labels.LinearRateLimited(StatusText.Clock(limited.RetryAfter));
                Lock(locked: false);
                break;
            default:
                Lock(locked: false);
                break;
        }
    }

    private void Checking(int epoch)
    {
        if (IsSaving && epoch == _epoch)
        {
            StatusLine = Labels.LinearChecking(Key);
        }
    }

    private void Applied(LinearChangeApplied applied)
    {
        Draw(applied.Issue, keepEdits: false);
        Lock(locked: false);
        StatusLine = applied.AlreadyApplied == true ? Labels.LinearAlreadySaved
            : applied.Warning is not null ? Labels.LinearSavedAfterSync
            : Labels.LinearSaved(StatusText.Clock(applied.Issue.UpdatedAt));
        FocusRequested?.Invoke(this, new ValueEventArgs<LinearIssueFocus>(LinearIssueFocus.Title));
    }

    private void Conflicted(LinearChangeConflict conflict)
    {
        var mine = Editable.ToDictionary(f => f.Api, f => f.Current?.DeepClone(), StringComparer.Ordinal);
        Draw(conflict.Issue, keepEdits: true);
        var rows = new List<LinearConflictRowViewModel>();
        foreach (var differing in conflict.Fields)
        {
            if (Editable.FirstOrDefault(f => string.Equals(f.Api, differing.Field, StringComparison.Ordinal)) is not { } field)
            {
                continue;
            }

            var linear = Plain(differing.Linear);
            field.Rebaseline(linear);   // "Save mine anyway" re-runs the check against the values just shown
            rows.Add(new LinearConflictRowViewModel(field.Label, Labels.LinearConflictText(field.Describe(linear), field.Describe(mine.GetValueOrDefault(field.Api))), row => TakeLinears(row, field, linear)));
        }

        ConflictRows = rows;
        ConflictHeadline = Labels.LinearConflictHeadline(Key, StatusText.Clock(conflict.ChangedAt));
        HasConflict = rows.Count > 0;
        Lock(locked: false);
        if (HasConflict)
        {
            FocusRequested?.Invoke(this, new ValueEventArgs<LinearIssueFocus>(LinearIssueFocus.Banner));
        }
    }

    // A value from the 409's body as the plain JSON the fields hold (numbers as ints, strings as strings), so it compares with their choices.
    private static JsonNode? Plain(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => JsonValue.Create(element.GetString()),
        JsonValueKind.Number when element.TryGetInt32(out var number) => JsonValue.Create(number),
        JsonValueKind.True => JsonValue.Create(value: true),
        JsonValueKind.False => JsonValue.Create(value: false),
        JsonValueKind.Array => new JsonArray([.. element.EnumerateArray().Select(Plain)]),
        JsonValueKind.Null or JsonValueKind.Undefined or JsonValueKind.Object or JsonValueKind.Number => null,
        _ => null,
    };

    private void TakeLinears(LinearConflictRowViewModel row, LinearEditFieldViewModel field, JsonNode? linear)
    {
        field.Rebase(linear);
        ConflictRows = [.. ConflictRows.Where(r => !ReferenceEquals(r, row))];
        HasConflict = ConflictRows.Count > 0;
    }

    private void Refused(Error error)
    {
        Lock(locked: false);
        if (string.Equals(error.Code, "SND-AUTH-002", StringComparison.Ordinal))
        {
            CanEdit = false;   // a session may not write: the form reads as text, with no banner
            return;
        }

        CanConnect = string.Equals(error.Code, NoWriteKey, StringComparison.Ordinal);
        ErrorBanner = Labels.LinearWriteError(error, _issue?.Team.Key ?? string.Empty, Key);
    }
}
