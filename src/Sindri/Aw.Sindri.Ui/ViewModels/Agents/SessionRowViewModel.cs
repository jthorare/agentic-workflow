using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// One live session on the Agents page (collector spec CU-1, CU-6). It is kept by its ref across reads and updated in place, so a control with
/// focus keeps it. It maps served fields to words and decides nothing: Focus is the catalog's pending control, never a button that sends.
/// </summary>
internal sealed class SessionRowViewModel : ObservableObject, ISessionRef
{
    private readonly TimeProvider _clock;

    internal SessionRowViewModel(SessionRowView row, CommandRowView? focusRow, IClipboard clipboard, TimeProvider clock)
    {
        Row = row;
        _clock = clock;
        Focus = new FocusLine(row.Ref, focusRow, clipboard, clock);
        Link = new DeepLink(DeepLinkKind.Session, row.Ref);
        Derive();
    }

    /// <summary>The session as last served.</summary>
    internal SessionRowView Row { get; private set; }

    /// <summary>The session's ref, provider/id.</summary>
    public string Ref => Row.Ref;

    /// <summary>The deep link that opens its sheet.</summary>
    public DeepLink Link { get; }

    /// <summary>The Focus control: disabled, with its phase and the CLI line to copy.</summary>
    public FocusLine Focus { get; }

    /// <summary>The provider's label as served.</summary>
    public string Provider => Row.ProviderLabel;

    /// <summary>The title as served; else the repo and the first 8 characters of the id; else the 8 characters.</summary>
    public string TitleText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The chip: glyph and word.</summary>
    public string ChipText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The state glyph.</summary>
    public string Glyph { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>What an asking session waits on ("approval · Bash"), else empty.</summary>
    public string AskingText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"estimated" when the state was worked out from the transcript.</summary>
    public string EstimatedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The worktree as served, or empty.</summary>
    public string WorktreeText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>How long it has been in its state.</summary>
    public string SinceText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The host's label, with the tmux session after it when no lane names the row.</summary>
    public string HostText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where it runs, in one line: host, lane or launch.</summary>
    public string WhereText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Repository column: the repo, then the last folder of its worktree.</summary>
    public string RepoText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True under the page width where only Status, Agent and For stay.</summary>
    public bool IsCompact { get; private set => Set(ref field, value); }

    /// <summary>True when every column shows.</summary>
    public bool IsWide => !IsCompact;

    /// <summary>Sets the row's layout for the page's width.</summary>
    /// <param name="compact">True under the width where only three columns stay.</param>
    internal void SetCompact(bool compact)
    {
        IsCompact = compact;
        OnChanged(nameof(IsWide));
    }

    /// <summary>The lane ("meta · sindri/ideas") or, for a launched session, its launch; else empty.</summary>
    public string LaneText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The row's accessible name.</summary>
    public string Name { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the row is where the keys act.</summary>
    public bool IsSelected { get; set => Set(ref field, value); }

    /// <summary>True when the state is asking.</summary>
    public bool IsAsking => string.Equals(Row.State, "asking", StringComparison.Ordinal);

    /// <summary>True when the row has an asking detail.</summary>
    public bool HasAsking => AskingText.Length > 0;

    /// <summary>True when the row is estimated.</summary>
    public bool IsEstimated => EstimatedText.Length > 0;

    /// <summary>True when the row has a worktree.</summary>
    public bool HasWorktree => WorktreeText.Length > 0;

    /// <summary>True when the row has a lane or launch line.</summary>
    public bool HasLane => LaneText.Length > 0;

    /// <summary>True when Sindri launched the session (it sits in the second group).</summary>
    public bool IsLaunched => Row.LaunchId is { Length: > 0 };

    /// <summary>Raised when the row asks to open the frame of the Step it works on.</summary>
    internal event EventHandler<SessionFrameRequestedEventArgs>? FrameRequested;

    /// <summary>"on ITM-…/step/1 · build · attempt 2" for a session an engine launch started; empty otherwise.</summary>
    public string StepText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The step button's accessible name: its visible words first, then what it opens.</summary>
    public string StepName { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the row names the Step it works on.</summary>
    public bool HasStep => StepText.Length > 0;

    /// <summary>True when the doing cell would be empty (the session neither asks nor runs a Step): it says so in words.</summary>
    public bool HasNoStep => !HasAsking && !HasStep;

    /// <summary>What the doing cell says then.</summary>
    public string NoStepText { get; } = Labels.SessionNoStep;

    /// <summary>Opens the frame of the Step the session works on; nothing without one.</summary>
    internal void OpenFrame()
    {
        if (Row.Launch is { Instance: { Length: > 0 } instance } launch)
        {
            FrameRequested?.Invoke(this, new SessionFrameRequestedEventArgs(instance, launch.State));
        }
    }

    /// <summary>Everything the row draws, so a read that changes nothing leaves the row alone.</summary>
    internal string Signature { get; private set; } = string.Empty;

    /// <summary>Takes a newer serving of the same session and redraws only what changed.</summary>
    /// <param name="row">The row as served now.</param>
    internal void Update(SessionRowView row)
    {
        Row = row;
        Derive();
    }

    /// <summary>Re-words the age (time passes without a read).</summary>
    internal void Age() => SinceText = Labels.Age(_clock.GetUtcNow() - Row.Since);

    private void Derive()
    {
        var chip = SessionChips.For(Row.State);
        var title = TitleOf(Row);
        (Glyph, ChipText) = (chip.Glyph, chip.Text);
        TitleText = title;
        (AskingText, EstimatedText) = (AskingOf(Row), EstimatedOf(Row));
        (WorktreeText, SinceText) = (Row.Worktree ?? string.Empty, Labels.Age(_clock.GetUtcNow() - Row.Since));
        (HostText, LaneText) = (HostOf(Row), LaneOf(Row));
        StepText = Row.Launch is { } launch && launch.Instance is { Length: > 0 } ? Labels.SessionStep(launch.State, launch.Attempt) : string.Empty;
        StepName = Row.Launch is { } named && named.Instance is { Length: > 0 } ? Labels.SessionStepName(StepText, named.State) : string.Empty;
        WhereText = string.Join(" · ", new[] { HostText, LaneText }.Where(p => p.Length > 0));
        RepoText = string.Join(" · ", new[] { Row.Repo ?? string.Empty, LastFolder(WorktreeText) }.Where(p => p.Length > 0));
        Name = Labels.SessionName(Row.ProviderLabel, title, chip.Word.ToLowerInvariant() + (EstimatedText.Length > 0 ? " (" + EstimatedText + ")" : string.Empty), string.Join(", ", new[] { AskingText, StepText }.Where(p => p.Length > 0)), HostText);
        Signature = string.Join('|', Row.State, AskingText, EstimatedText, title, WorktreeText, HostText, LaneText, StepText, Row.Focus.Method, Row.Repo);
        Focus.Show(Row.Ref);
        foreach (var name in (string[])[nameof(IsAsking), nameof(HasAsking), nameof(IsEstimated), nameof(HasWorktree), nameof(HasLane), nameof(IsLaunched), nameof(HasStep), nameof(HasNoStep)])
        {
            OnChanged(name);
        }
    }

    private static string AskingOf(SessionRowView row) => row.Asking is { } asking ? Labels.AskingDetail(asking.Reason, asking.Tool) : string.Empty;

    private static string EstimatedOf(SessionRowView row) => row.Estimated && string.Equals(row.StateSource, "transcript", StringComparison.Ordinal) ? "estimated" : string.Empty;

    private static string LastFolder(string path) => Path.GetFileName(path.TrimEnd('/', '\\'));

    private static string HostOf(SessionRowView row) => row.Lane is null && row.Host.Session is { Length: > 0 } session ? row.Host.Label + " · " + session : row.Host.Label;

    private static string LaneOf(SessionRowView row) =>
        row.Lane is { } lane ? Labels.LaneLine(lane.Team, lane.Lane) : row.LaunchId is { Length: > 0 } ? Labels.LaunchLine : string.Empty;

    // A title as the human gave it; else where it is (repo and the id's first characters); else the id's first characters.
    private static string TitleOf(SessionRowView row)
    {
        if (row.Title is { Length: > 0 } title)
        {
            return title;
        }

        var id8 = ShortId.Of(row.Id);
        return row.Repo is { Length: > 0 } repo ? repo + "/" + id8 : id8;
    }
}
