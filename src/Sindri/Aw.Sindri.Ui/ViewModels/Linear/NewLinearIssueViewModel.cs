using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// The Linear "New issue" dialog (Linear hub spec §3): a team, a title and the optional fields, sent once to POST /sindri/linear/issues with a client id that a retry
/// repeats. Closing keeps the draft (fields and client id) until it is created or discarded. Not built yet.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="url">Where the API is, or null when it is not running.</param>
/// <param name="clock">The clock for the "Sending…" and "Checking…" timers.</param>
internal sealed class NewLinearIssueViewModel(ISindriApi api, Func<Uri?> url, TimeProvider clock) : ObservableObject
{
    /// <summary>The dialog's copy.</summary>
    public LinearNewText Text { get; } = LinearNewText.Instance;

    /// <summary>True while the dialog is shown.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The teams the profile names.</summary>
    public IReadOnlyList<LinearChoiceViewModel> TeamChoices { get; private set => Set(ref field, value); } = [];

    /// <summary>The chosen team; null before one is picked.</summary>
    public LinearChoiceViewModel? Team { get; set => Set(ref field, value); }

    /// <summary>The title (required, 1 to 255 characters).</summary>
    public string Title { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The description, or empty.</summary>
    public string Description { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The team's states, led by the "Team default" entry.</summary>
    public IReadOnlyList<LinearChoiceViewModel> States { get; private set => Set(ref field, value); } = [];

    /// <summary>The chosen state.</summary>
    public LinearChoiceViewModel? State { get; set => Set(ref field, value); }

    /// <summary>The team's active members, led by "Unassigned".</summary>
    public IReadOnlyList<LinearChoiceViewModel> Assignees { get; private set => Set(ref field, value); } = [];

    /// <summary>The chosen assignee.</summary>
    public LinearChoiceViewModel? Assignee { get; set => Set(ref field, value); }

    /// <summary>No priority, Urgent, High, Medium, Low.</summary>
    public IReadOnlyList<LinearChoiceViewModel> Priorities { get; private set => Set(ref field, value); } = [];

    /// <summary>The chosen priority.</summary>
    public LinearChoiceViewModel? Priority { get; set => Set(ref field, value); }

    /// <summary>The team's labels, each with a tick.</summary>
    public IReadOnlyList<LinearChoiceViewModel> LabelChoices { get; private set => Set(ref field, value); } = [];

    /// <summary>The team's cycles, led by "No cycle".</summary>
    public IReadOnlyList<LinearChoiceViewModel> Cycles { get; private set => Set(ref field, value); } = [];

    /// <summary>The chosen cycle.</summary>
    public LinearChoiceViewModel? Cycle { get; set => Set(ref field, value); }

    /// <summary>"ENG's states haven't synced yet…" while the team's lookups are not synced; else empty.</summary>
    public string NotSyncedNote { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Title is required" after a blank submit, until a title is typed.</summary>
    public string TitleError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Team is required" after a submit with no team, until one is picked.</summary>
    public string TeamError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the reopened dialog holds a draft ("Resume draft" and "Discard draft" show).</summary>
    public bool ShowsDraft { get; private set => Set(ref field, value); }

    /// <summary>True while the form is locked: a create is in flight, or its answer is unknown.</summary>
    public bool IsLocked { get; private set => Set(ref field, value); }

    /// <summary>True when the last create got no answer: the form stays locked and Retry shows.</summary>
    public bool IsUnknown { get; private set => Set(ref field, value); }

    /// <summary>True when a session may not write: Create is disabled and nothing else says so.</summary>
    public bool IsOff { get; private set => Set(ref field, value); }

    /// <summary>The Create button's text: "Create issue", or "Sending…" once a create has run 300 ms.</summary>
    public string CreateText { get; private set => Set(ref field, value); } = Labels.LinearNewCreate;

    /// <summary>The polite line: "Checking…", "Linear didn't confirm the issue. Retry?", "Created ENG-58".</summary>
    public string StatusLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The banner over the form: a rate limit or a refusal.</summary>
    public string ErrorBanner { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The live region's gate, set before its text.</summary>
    public bool IsAnnouncing { get; private set => Set(ref field, value); }

    /// <summary>The live region's text; never empty.</summary>
    public string Announcement { get; private set => Set(ref field, value); } = Labels.LinearNewTitleRequired;

    /// <summary>Counts the submits that ask the view to put focus on the team.</summary>
    public int TeamFocusRequests { get; private set => Set(ref field, value); }

    /// <summary>Counts the submits that ask the view to put focus on the title.</summary>
    public int TitleFocusRequests { get; private set => Set(ref field, value); }

    /// <summary>The draft's client id; null until text is typed or a create is sent.</summary>
    internal string? ClientId { get; private set; }

    /// <summary>The last lookups read for the picked team.</summary>
    internal Task Reading { get; private set; } = Task.CompletedTask;

    /// <summary>The issue was created: its key.</summary>
    internal event EventHandler<ValueEventArgs<string>>? Created;

    /// <summary>Opens the dialog and reads the profile's teams; a single team is picked.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the teams are shown.</returns>
    internal Task Open(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(clock);
        throw new NotSupportedException();
    }

    /// <summary>Closes the dialog; the draft stays.</summary>
    internal void Close() => throw new NotSupportedException();

    /// <summary>Throws the draft away: the fields, the ticks and the client id.</summary>
    internal void Discard() => throw new NotSupportedException();

    /// <summary>Sends the form.</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Submit(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Sends the same body again with the same client id (after an unknown answer).</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Retry(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Raises <see cref="Created"/> (kept so the event has a raiser while the dialog is a stub).</summary>
    /// <param name="key">The key.</param>
    internal void Raise(string key) => Created?.Invoke(this, new ValueEventArgs<string>(key));
}
