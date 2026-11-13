using Avalonia.Automation.Peers;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>What a flat <see cref="Row"/> tells a screen reader (#3189): see <see cref="RowAutomationPeer"/>.</summary>
internal sealed partial class Row
{
    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new RowAutomationPeer(this);
}
