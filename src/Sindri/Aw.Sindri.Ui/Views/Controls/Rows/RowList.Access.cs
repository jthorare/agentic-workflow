using Avalonia.Automation;
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

    /// <summary>The rows built now that are the selected one: the list's selection for a screen reader.</summary>
    internal IEnumerable<Row> SelectedRows() => ItemsPanelRoot?.Children.OfType<Row>().Where(r => r.Classes.Contains("sel")) ?? [];

    // Tells a built row its place among all the rows ("3 of 40"): groups and their headers are not counted, folded groups left out.
    private void SetPlace(Row row, int index)
    {
        var placed = 0;
        var at = 0;
        foreach (var state in _run.States)
        {
            var next = at + 1 + state.Visible;
            if (index < next)
            {
                AutomationProperties.SetPositionInSet(row, placed + (index - at));
                break;
            }

            placed += state.Visible;
            at = next;
        }

        AutomationProperties.SetSizeOfSet(row, RowCount);
    }

    private void RefreshPlaces()
    {
        foreach (var row in ItemsPanelRoot?.Children.OfType<Row>() ?? [])
        {
            if (row.DataContext is not null && IndexFromContainer(row) is var index and >= 0)
            {
                SetPlace(row, index);
            }
        }
    }

    // After an edit: if the row count changed, the list's name did, and the peer says so (once per change).
    private void AnnounceRowCount()
    {
        RefreshPlaces();
        var now = RowCount;
        var before = _announcedRows;
        _announcedRows = now;
        if (now != before)
        {
            _peer?.RowCountChanged(before, now);
        }
    }
}
