using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Linear;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>Sending the form and drawing the answer (<see cref="NewLinearIssueViewModel"/>, continued): created, unknown, rate limit and the refusals.</summary>
internal sealed partial class NewLinearIssueViewModel
{
    private static readonly TimeSpan _sendingAfter = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan _checkAfter = TimeSpan.FromSeconds(15);

    private LinearCreateBody? _sent;
    private bool _inFlight;

    /// <summary>True while the form is locked: a create is in flight, or its answer is unknown.</summary>
    public bool IsLocked
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanEdit));
            OnChanged(nameof(CanCreate));
            OnChanged(nameof(CanPick));
        }
    }

    /// <summary>True when the fields take input.</summary>
    public bool CanEdit => !IsLocked;

    /// <summary>True when the lookups' pickers take input: the form is open and the team's lookups are in.</summary>
    public bool CanPick => !IsLocked && HasLookups;

    /// <summary>True when the Create button takes a press: not locked, and not turned off by a session.</summary>
    public bool CanCreate => !IsLocked && !IsOff;

    /// <summary>True when the last create got no answer: the form stays locked and Retry shows instead of Create.</summary>
    public bool IsUnknown
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(ShowsCreate));
        }
    }

    /// <summary>True while Create (and not Retry) is the button.</summary>
    public bool ShowsCreate => !IsUnknown;

    /// <summary>True when a session may not write: Create is disabled and nothing else says so, the text is kept.</summary>
    public bool IsOff
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanCreate));
        }
    }

    /// <summary>The Create button's text: "Create issue", or "Sending…" once a create has run 300 ms.</summary>
    public string CreateText { get; private set => Set(ref field, value); } = Labels.LinearNewCreate;

    /// <summary>The polite line: "Checking…" or "Linear didn't confirm the issue. Retry?"; empty otherwise.</summary>
    public string StatusLine
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasStatusLine));
        }
    } = string.Empty;

    /// <summary>True when <see cref="StatusLine"/> shows.</summary>
    public bool HasStatusLine => StatusLine.Length > 0;

    /// <summary>The banner over the form: a rate limit or a refusal; typing clears it.</summary>
    public string ErrorBanner
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasErrorBanner));
        }
    } = string.Empty;

    /// <summary>True when <see cref="ErrorBanner"/> shows.</summary>
    public bool HasErrorBanner => ErrorBanner.Length > 0;

    /// <summary>Sends the form: the team and the title are asked for first; nothing is sent while a create is in flight or unanswered.</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Submit(CancellationToken cancellationToken)
    {
        if (IsLocked || IsOff)
        {
            return Task.CompletedTask;
        }

        if (Id(Team) is not { } team)
        {
            RequireTeam();
            return Task.CompletedTask;
        }

        var title = Title.Trim();
        if (title.Length is 0 or > TitleLimit)
        {
            RequireTitle(title.Length == 0 ? Labels.LinearNewTitleRequired : Labels.LinearNewTitleTooLong);
            return Task.CompletedTask;
        }

        var clientId = ClientId ??= Guid.NewGuid().ToString();
        return Send(Body(clientId, team, title), cancellationToken);
    }

    /// <summary>Sends the same body again with the same client id (after an unknown answer).</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Retry(CancellationToken cancellationToken) =>
        IsUnknown && !_inFlight && _sent is { } body ? Send(body, cancellationToken) : Task.CompletedTask;

    // Only the fields that are set go in: the clear entries and "No priority" send nothing.
    private LinearCreateBody Body(string clientId, string team, string title)
    {
        var labels = LabelChoices.Where(l => l.IsTicked).Select(Id).OfType<string>().ToList();
        return new LinearCreateBody(
            clientId,
            team,
            title,
            Description: string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            StateId: Id(State),
            AssigneeId: Id(Assignee),
            Priority: Priority?.Value is JsonValue p && p.TryGetValue<int>(out var priority) && priority > 0 ? priority : null,
            CycleId: Id(Cycle),
            LabelIds: labels.Count > 0 ? labels : null);
    }

    private async Task Send(LinearCreateBody body, CancellationToken cancellationToken)
    {
        if (url() is not { } address)
        {
            Refused(SindriUiErrors.NotRunning.With("no API to send to"), body.Team);
            return;
        }

        _sent = body;
        (_inFlight, IsUnknown, IsLocked) = (true, false, true);
        (StatusLine, ErrorBanner) = (string.Empty, string.Empty);
        await using var sending = clock.CreateTimer(_ => _ui.PostOrInline(ShowSending), state: null, _sendingAfter, Timeout.InfiniteTimeSpan).ConfigureAwait(true);
        await using var checking = clock.CreateTimer(_ => _ui.PostOrInline(() => Checking(body.Team)), state: null, _checkAfter, Timeout.InfiniteTimeSpan).ConfigureAwait(true);
        var answer = await api.LinearCreate(address, body, cancellationToken).ConfigureAwait(true);
        (_inFlight, CreateText) = (false, Labels.LinearNewCreate);
        if (answer.IsSuccess)
        {
            Answered(answer.Value, body.Team);
        }
        else
        {
            Refused(answer.Error, body.Team);
        }
    }

    private void ShowSending()
    {
        if (_inFlight)
        {
            CreateText = Labels.LinearSending;
        }
    }

    private void Checking(string team)
    {
        if (_inFlight)
        {
            StatusLine = Labels.LinearChecking(team);
            Say(StatusLine);
        }
    }

    private void Answered(LinearWriteOutcome outcome, string team)
    {
        StatusLine = string.Empty;
        switch (outcome)
        {
            case LinearCreated created:
                Done(created.Issue.Key);
                break;
            case LinearWriteUnknown:
                Unknown();
                break;
            case LinearWriteRateLimited limited:
                ErrorBanner = Labels.LinearRateLimited(StatusText.Clock(limited.RetryAfter));
                IsLocked = false;
                break;
            default:
                Refused(SindriUiErrors.BadAnswer.With($"{team}: the create was answered with something this build can't read"), team);
                break;
        }
    }

    private void Refused(Error error, string team)
    {
        StatusLine = string.Empty;
        if (string.Equals(error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal))
        {
            Unknown();   // the app's own call got no answer, so the POST may have landed
            return;
        }

        IsLocked = false;
        if (string.Equals(error.Code, "SND-AUTH-002", StringComparison.Ordinal))
        {
            IsOff = true;   // a session may not write: no banner, the text is kept
            return;
        }

        ErrorBanner = Labels.LinearWriteError(error, team, string.Empty);
    }

    private void Unknown()
    {
        (IsUnknown, IsLocked) = (true, true);
        StatusLine = Labels.LinearNewUnknown;
        Say(StatusLine);
    }

    private void Done(string key)
    {
        (IsUnknown, IsLocked, _sent) = (false, false, null);
        ClearDraft();
        JustCreated = true;
        IsOpen = false;
        Say(Labels.LinearNewCreated(key));
        Created?.Invoke(this, new ValueEventArgs<string>(key));
    }
}
