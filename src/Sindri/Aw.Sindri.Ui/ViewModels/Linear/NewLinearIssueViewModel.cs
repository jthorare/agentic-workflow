using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Linear;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// The Linear "New issue" dialog (Linear hub spec §3): a team, a title and the optional fields, sent once to POST /sindri/linear/issues with a client id that a retry
/// repeats. Closing keeps the draft (fields and client id) until it is created or discarded; the draft lives as long as the shell does.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="url">Where the API is, or null when it is not running.</param>
/// <param name="clock">The clock for the "Sending…" and "Checking…" timers.</param>
internal sealed partial class NewLinearIssueViewModel(ISindriApi api, Func<Uri?> url, TimeProvider clock) : ObservableObject
{
    private const int TitleLimit = 255;

    private readonly UiContext _ui = new();
    private readonly Dictionary<string, LinearLookups> _lookups = new(StringComparer.Ordinal);   // per team, for the opening's life
    private CancellationToken _opened;
    private bool _seating;

    /// <summary>The dialog's copy.</summary>
    public LinearNewText Text { get; } = LinearNewText.Instance;

    /// <summary>True while the dialog is shown.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The teams the profile names.</summary>
    public IReadOnlyList<LinearChoiceViewModel> TeamChoices { get; private set => Set(ref field, value); } = [];

    /// <summary>The chosen team; null before one is picked. Picking one reads its lookups (once per opening).</summary>
    public LinearChoiceViewModel? Team
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            Set(ref field, value);
            if (_seating)
            {
                return;
            }

            ClearTeamError();
            ClearLookups();
            Reading = value is null ? Task.CompletedTask : ReadLookups(value);
        }
    }

    /// <summary>The title (required, 1 to 255 characters).</summary>
    public string Title
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(IsTitleBlank));
            Typed(value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                ClearTitleError();
            }
        }
    } = string.Empty;

    /// <summary>True while the title is blank.</summary>
    public bool IsTitleBlank => string.IsNullOrWhiteSpace(Title);

    /// <summary>The description, or empty.</summary>
    public string Description
    {
        get;
        set
        {
            Set(ref field, value);
            Typed(value);
        }
    } = string.Empty;

    /// <summary>The team's states, led by the "Team default" entry; empty before the team has synced.</summary>
    public IReadOnlyList<LinearChoiceViewModel> States { get; private set => Set(ref field, value); } = [];

    /// <summary>The chosen state; the first entry sends none.</summary>
    public LinearChoiceViewModel? State { get; set => Set(ref field, value); }

    /// <summary>The team's active members, led by "Unassigned".</summary>
    public IReadOnlyList<LinearChoiceViewModel> Assignees { get; private set => Set(ref field, value); } = [];

    /// <summary>The chosen assignee.</summary>
    public LinearChoiceViewModel? Assignee { get; set => Set(ref field, value); }

    /// <summary>No priority, Urgent, High, Medium, Low (0 to 4).</summary>
    public IReadOnlyList<LinearChoiceViewModel> Priorities { get; } =
        [.. Enumerable.Range(0, 5).Select(p => new LinearChoiceViewModel(JsonValue.Create(p), Labels.LinearPriority(p)))];

    /// <summary>The chosen priority.</summary>
    public LinearChoiceViewModel? Priority { get => field ?? Priorities[0]; set => Set(ref field, value); }

    /// <summary>The team's labels, each with a tick.</summary>
    public IReadOnlyList<LinearChoiceViewModel> LabelChoices { get; private set => Set(ref field, value); } = [];

    /// <summary>The team's cycles, led by "No cycle".</summary>
    public IReadOnlyList<LinearChoiceViewModel> Cycles { get; private set => Set(ref field, value); } = [];

    /// <summary>The chosen cycle.</summary>
    public LinearChoiceViewModel? Cycle { get; set => Set(ref field, value); }

    /// <summary>"ENG's states haven't synced yet…" or "Couldn't load ENG's states" while the pickers have nothing; else empty.</summary>
    public string NotSyncedNote
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasNote));
        }
    } = string.Empty;

    /// <summary>True when <see cref="NotSyncedNote"/> shows.</summary>
    public bool HasNote => NotSyncedNote.Length > 0;

    /// <summary>True when the team's lookups are shown, so the pickers that need them can be used.</summary>
    public bool HasLookups => States.Count > 0;

    /// <summary>"Resume draft" and "Discard draft" show over a reopened dialog that holds a draft.</summary>
    public bool ShowsDraft { get; private set => Set(ref field, value); }

    /// <summary>True from a create that closed the dialog until it opens again: focus then goes to the new row, not back to what opened the dialog.</summary>
    public bool JustCreated { get; private set; }

    /// <summary>Counts the opens and submits that ask the view to put focus on the team.</summary>
    public int TeamFocusRequests { get; private set => Set(ref field, value); }

    /// <summary>Counts the blank submits that ask the view to put focus on the title.</summary>
    public int TitleFocusRequests { get; private set => Set(ref field, value); }

    /// <summary>The draft's client id; null until text is typed or a create is sent.</summary>
    internal string? ClientId { get; private set; }

    /// <summary>The latest lookups read (a team pick starts one).</summary>
    internal Task Reading { get; private set; } = Task.CompletedTask;

    /// <summary>The issue was created: its key.</summary>
    internal event EventHandler<ValueEventArgs<string>>? Created;

    /// <summary>Opens the dialog and reads the profile's teams; a single team is picked, and a team already picked has its lookups read again.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the teams are shown.</returns>
    internal async Task Open(CancellationToken cancellationToken)
    {
        _opened = cancellationToken;
        _lookups.Clear();
        ShowsDraft = ClientId is not null;
        JustCreated = false;
        ClearTitleError();
        ClearTeamError();
        IsOpen = true;
        if (url() is { } address)
        {
            var connection = await api.LinearConnection(address, cancellationToken).ConfigureAwait(true);
            if (connection.IsError)
            {
                if (TeamChoices.Count == 0)
                {
                    ErrorBanner = Labels.LinearWriteError(connection.Error, string.Empty, string.Empty);
                }
            }
            else
            {
                Offer(connection.Value.Teams);
            }
        }

        if (Team is { } picked)
        {
            Reading = ReadLookups(picked);   // the draft's team: its lookups again, the picks kept by id
        }

        await Reading.ConfigureAwait(true);
        if (Team is null)
        {
            TeamFocusRequests++;
        }
    }

    /// <summary>Closes the dialog; the draft stays.</summary>
    internal void Close() => IsOpen = false;

    /// <summary>Throws the draft away: the fields, the ticks and the client id. Nothing happens while a create is in flight; an unanswered one is let go, with no Retry sent.</summary>
    internal void Discard()
    {
        if (_inFlight)
        {
            return;
        }

        (IsUnknown, IsLocked, _sent) = (false, false, null);
        StatusLine = string.Empty;
        ClearDraft();
        ErrorBanner = string.Empty;
        ClearTitleError();
    }

    // The fields back to a fresh dialog's (the team stays, so the next issue for it starts where this one did).
    private void ClearDraft()
    {
        (Title, Description) = (string.Empty, string.Empty);
        ClientId = null;
        ShowsDraft = false;
        State = States.Count > 0 ? States[0] : null;
        Assignee = Assignees.Count > 0 ? Assignees[0] : null;
        Priority = Priorities[0];
        Cycle = Cycles.Count > 0 ? Cycles[0] : null;
        foreach (var label in LabelChoices)
        {
            label.Reset(ticked: false);
        }
    }

    // The client id is made with the first typed text and kept with the draft.
    private void Typed(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            ClientId ??= Guid.NewGuid().ToString();
        }

        if (ErrorBanner.Length > 0 && !IsLocked)
        {
            ErrorBanner = string.Empty;
        }
    }

    private void Offer(IReadOnlyList<LinearTeam> teams)
    {
        var offered = teams.Select(t => (t.Key, Name: string.Equals(t.DisplayName, t.Key, StringComparison.Ordinal) ? t.Key : $"{t.Key} · {t.DisplayName}")).ToList();
        if (!offered.Select(o => o.Key).SequenceEqual(TeamChoices.Select(c => Id(c) ?? string.Empty), StringComparer.Ordinal))
        {
            TeamChoices = [.. offered.Select(o => new LinearChoiceViewModel(JsonValue.Create(o.Key), o.Name))];
            if (Team is { } picked && Id(picked) is { } key && TeamChoices.FirstOrDefault(c => string.Equals(Id(c), key, StringComparison.Ordinal)) is { } same)
            {
                // The draft's team is still offered: the entry that stands for it is the new one, with the draft's picks left alone.
                SeatTeam(same);
            }
            else
            {
                Team = null;
            }
        }

        if (Team is null && TeamChoices.Count == 1)
        {
            Team = TeamChoices[0];
        }
    }

    // The entry standing for the same team in a rebuilt list takes the old one's place without the pick-a-team side effects.
    private void SeatTeam(LinearChoiceViewModel same)
    {
        _seating = true;
        try
        {
            Team = same;
        }
        finally
        {
            _seating = false;
        }
    }

    private static string? Id(LinearChoiceViewModel? choice) => choice?.Value is JsonValue v && v.TryGetValue<string>(out var id) ? id : null;
}
