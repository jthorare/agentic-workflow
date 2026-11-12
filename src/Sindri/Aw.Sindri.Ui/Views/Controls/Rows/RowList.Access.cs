using Avalonia.Automation.Peers;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>What <see cref="RowList"/> tells a screen reader (#2978): it is a list, and it holds this many rows.</summary>
internal sealed partial class RowList
{
    /// <summary>How many rows the list holds now, in every group, folded groups left out. The screen reader hears it as the list's size.</summary>
    internal int RowCount => _run.States.Sum(state => state.Visible);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new RowListAutomationPeer(this);
}
