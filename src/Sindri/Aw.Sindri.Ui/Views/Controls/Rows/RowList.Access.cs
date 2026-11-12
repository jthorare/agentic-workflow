using Avalonia.Automation.Peers;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>What <see cref="RowList"/> tells a screen reader (#2978): it is a list, and it holds this many rows.</summary>
internal sealed partial class RowList
{
    private RowListAutomationPeer? _peer;
    private int _announcedRows;

    /// <summary>Raised when `.` asks for the cursor row's actions; the page opens them (Item is null when no row has the cursor).</summary>
    public event EventHandler<RowRequestEventArgs>? ActionsRequested;

    internal void RaiseActions(RowRequestEventArgs e) => ActionsRequested?.Invoke(this, e);

    /// <summary>How many rows the list holds now, in every group, folded groups left out. The screen reader hears it as the list's size.</summary>
    internal int RowCount => _run.States.Sum(state => state.Visible);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        _announcedRows = RowCount;
        return _peer = new RowListAutomationPeer(this);
    }

    // After an edit: if the row count changed, the list's name did, and the peer says so (once per change).
    private void AnnounceRowCount()
    {
        var now = RowCount;
        var before = _announcedRows;
        _announcedRows = now;
        if (now != before)
        {
            _peer?.RowCountChanged(before, now);
        }
    }
}
