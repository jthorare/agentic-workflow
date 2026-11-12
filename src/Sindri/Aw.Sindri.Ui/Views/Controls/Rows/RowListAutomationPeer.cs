using Avalonia.Automation;
using Avalonia.Automation.Peers;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// The screen-reader face of a <see cref="RowList"/> (#2978): a list that says how many rows it holds. The panel is virtualized, so the default peer would count only the
/// ~30 rows built; this one counts every row in the list, folded groups left out. A name the page set stays: "Needs you, 8 rows" reads "page name, N rows".
/// </summary>
/// <param name="owner">The list.</param>
internal sealed class RowListAutomationPeer(RowList owner) : ItemsControlAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

    /// <inheritdoc />
    protected override string? GetNameCore()
    {
        var page = AutomationProperties.GetName(owner);
        var rows = RowSpeech.Rows(owner.RowCount);
        return string.IsNullOrEmpty(page) ? rows : page + ", " + rows;
    }
}
