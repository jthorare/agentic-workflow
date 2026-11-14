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

    /// <summary>The state's word without its dot ("Working", "Waiting on you").</summary>
    public string StateWord { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the dot takes the Working colour.</summary>
    public bool IsWorking { get; private set => Set(ref field, value); }

    /// <summary>True when the dot takes the Waiting on you colour.</summary>
    public bool IsWaiting { get; private set => Set(ref field, value); }

    /// <summary>The state glyph.</summary>
    public string Glyph { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>What an asking session waits on ("approval · Bash"), else empty.</summary>
    public string AskingText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"estimated" when the state was worked out from the transcript.</summary>
    public string EstimatedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The tooltip on "estimated": what is estimated and why; empty when the row is not estimated.</summary>
    public string EstimatedTip => IsEstimated ? Labels.SessionEstimatedTip : string.Empty;

    /// <summary>The worktree as served, or empty.</summary>
    public string WorktreeText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>How long it has been in its state.</summary>
    public string SinceText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The host's label, with the tmux session after it when no lane names the row.</summary>
    public string HostText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where it runs, in one line: host, lane or launch.</summary>
    public string WhereText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Repository column: the repo, then the last folder of its worktree; the empty-cell dash when it has neither.</summary>
    public string RepoText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True under the page width where only Status, Agent and For stay.</summary>
    public bool IsCompact { get; private set => Set(ref field, value); }

    /// <summary>True when every column shows.</summary>
    public bool IsWide => !IsCompact;

    /// <summary>True when the row has something for the Repository column.</summary>
    internal bool HasRepoValue => !string.Equals(RepoText, Labels.EmptyCell, StringComparison.Ordinal);

    /// <summary>True when the row says where it runs.</summary>
    internal bool HasWhereValue => WhereText.Length > 0;

    /// <summary>True when the row asks something or runs a Step.</summary>
    internal bool HasDoingValue => HasAsking || HasStep;

    /// <summary>True when the Repository cell shows (the page drops a column every row leaves empty).</summary>
    public bool ShowsRepo { get; private set => Set(ref field, value); } = true;

    /// <summary>True when the Where it runs cell shows.</summary>
    public bool ShowsWhere { get; private set => Set(ref field, value); } = true;

    /// <summary>True when the What it is doing cell shows.</summary>
    public bool ShowsDoing { get; private set => Set(ref field, value); } = true;

    /// <summary>Takes which optional columns the page shows.</summary>
    /// <param name="repo">The Repository column.</param>
    /// <param name="where">The Where it runs column.</param>
    /// <param name="doing">The What it is doing column.</param>
    internal void SetColumns(bool repo, bool where, bool doing) => (ShowsRepo, ShowsWhere, ShowsDoing) = (repo, where, doing);

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

    /// <summary>"on ITM-…/step/1 · build · attempt 2" for a session an engine launch started; empty otherwise.</summary>
    public string StepText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The step's accessible name: its visible words first, then its state.</summary>
    public string StepName { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the row names the Step it works on.</summary>
    public bool HasStep => StepText.Length > 0;

    /// <summary>True when the doing cell would be empty (the session neither asks nor runs a Step): it says so in words.</summary>
    public bool HasNoStep => !HasAsking && !HasStep;

    /// <summary>What the doing cell says then.</summary>
    public string NoStepText { get; } = Labels.SessionNoStep;

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
        (Glyph, ChipText, StateWord) = (chip.Glyph, chip.Text, chip.Word);
        (IsWorking, IsWaiting) = (string.Equals(chip.Tone, "working", StringComparison.Ordinal), string.Equals(chip.Tone, "waiting", StringComparison.Ordinal));
        TitleText = title;
        (AskingText, EstimatedText) = (AskingOf(Row), EstimatedOf(Row));
        (WorktreeText, SinceText) = (Row.Worktree ?? string.Empty, Labels.Age(_clock.GetUtcNow() - Row.Since));
        (HostText, LaneText) = (HostOf(Row), LaneOf(Row));
        StepText = Row.Launch is { } launch && launch.Instance is { Length: > 0 } ? Labels.SessionStep(launch.State, launch.Attempt) : string.Empty;
        StepName = Row.Launch is { } named && named.Instance is { Length: > 0 } ? Labels.SessionStepName(StepText, named.State) : string.Empty;
        WhereText = string.Join(" · ", new[] { HostText, LaneText }.Where(p => p.Length > 0));
        RepoText = string.Join(" · ", new[] { Row.Repo ?? string.Empty, LastFolder(WorktreeText) }.Where(p => p.Length > 0)) is { Length: > 0 } repoText ? repoText : Labels.EmptyCell;
        Name = Labels.SessionName(Row.ProviderLabel, title, chip.Word.ToLowerInvariant() + (EstimatedText.Length > 0 ? " (" + EstimatedText + ")" : string.Empty), string.Join(", ", new[] { AskingText, StepText }.Where(p => p.Length > 0)), HostText);
        Signature = string.Join('|', Row.State, AskingText, EstimatedText, title, WorktreeText, HostText, LaneText, StepText, Row.Focus.Method, Row.Repo);
        Focus.Show(Row.Ref);
        foreach (var name in (string[])[nameof(IsAsking), nameof(HasAsking), nameof(IsEstimated), nameof(EstimatedTip), nameof(HasWorktree), nameof(HasLane), nameof(IsLaunched), nameof(HasStep), nameof(HasNoStep)])
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

    // #3082: a title as the human gave it; else the repo and the worktree's folder (the branch's name); else the provider. The id shows only in the sheet.
    internal static string TitleOf(SessionRowView row)
    {
        if (row.Title is { Length: > 0 } title)
        {
            return title;
        }

        var folder = row.Worktree is { Length: > 0 } worktree ? LastFolder(worktree) : string.Empty;
        var where = string.Join(' ', new[] { row.Repo ?? string.Empty, folder }.Where(p => p.Length > 0));
        return where.Length > 0 ? where : row.ProviderLabel;
    }
}
