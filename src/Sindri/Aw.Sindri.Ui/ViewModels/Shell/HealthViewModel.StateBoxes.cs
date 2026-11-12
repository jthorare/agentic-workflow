namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The System page's connection-state boxes (<see cref="HealthViewModel"/>, continued; system spec §3, slice 5): Not running, Different versions, Unreadable, Connecting, and the amber Stale line.</summary>
internal sealed partial class HealthViewModel
{
    /// <summary>A box stands in for the cards: Sindri is not running, speaks another API version, or answered with something this app can't read.</summary>
    public bool HasStateBox { get; private set => Set(ref field, value); }

    /// <summary>The box's sentence; empty without a box.</summary>
    public string StateBoxTitle { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The box's lines: the two API versions on a mismatch, the served code and reason when unreadable; none otherwise.</summary>
    public IReadOnlyList<HealthCell> StateBoxLines { get; private set => Set(ref field, value); } = [];

    /// <summary>The command the box copies (sindri serve when not running); empty for none.</summary>
    public string StateBoxFix { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the box copies a command.</summary>
    public bool HasStateBoxFix => StateBoxFix.Length > 0;

    /// <summary>First load: the headline says Connecting and skeleton rows stand in for the cards.</summary>
    public bool IsConnectingPage { get; private set => Set(ref field, value); } = true;

    /// <summary>Sindri has stopped answering: the last answer shows under an amber line.</summary>
    public bool ShowStaleLine { get; private set => Set(ref field, value); }

    /// <summary>The amber line's words; empty unless stale.</summary>
    public string StaleLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The amber line's Retry button: reads the status again (<see cref="Refresh"/>).</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string StaleRetryText => Labels.Retry;
#pragma warning restore CA1822, MA0041

    /// <summary>The Overview's cards show: connected, with an answer to put in them.</summary>
    public bool ShowOverviewCards { get; private set => Set(ref field, value); }

    // The boxes follow the connection state; the cards give way to a box or to the skeleton rows.
    private void ShowStateBox()
    {
        var box = BoxFor(State);
        (StateBoxTitle, StateBoxFix, StateBoxLines) = (box.Title, box.Fix, box.Lines);
        HasStateBox = StateBoxTitle.Length > 0;
        OnChanged(nameof(HasStateBoxFix));
        IsConnectingPage = State == ShellState.Connecting;
        ShowStaleLine = State == ShellState.Stale;
        StaleLine = ShowStaleLine ? Labels.SystemStaleLine((int)StaleAfter.TotalSeconds) : string.Empty;
        ShowOverviewCards = !HasStateBox && !IsConnectingPage;
    }

    private sealed record Box(string Title, string Fix, IReadOnlyList<HealthCell> Lines);

    private Box BoxFor(ShellState state) => state switch
    {
        ShellState.NotRunning => new Box(Labels.SystemNotRunningBox, Labels.SystemServeFix, []),
        ShellState.VersionMismatch => new Box(Labels.VersionMismatch, string.Empty, Detail.Versions),
        ShellState.Unreadable => new Box(Labels.SystemUnreadableBox, string.Empty, Reason.Length > 0 ? [new HealthCell(Labels.Unreadable, Reason)] : []),
        ShellState.Connecting or ShellState.Stale or ShellState.Normal => new Box(string.Empty, string.Empty, []),
        _ => new Box(string.Empty, string.Empty, []),
    };
}
