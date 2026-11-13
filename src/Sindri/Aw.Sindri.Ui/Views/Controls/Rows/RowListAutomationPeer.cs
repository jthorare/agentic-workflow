using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// The screen-reader face of a <see cref="RowList"/> (#2978): a list that says how many rows it holds. The panel is virtualized, so the default peer would count only the
/// ~30 rows built; this one counts every row in the list, folded groups left out. A name the page set stays: "Needs you, 8 rows" reads "page name, N rows".
/// </summary>
/// <param name="owner">The list.</param>
internal sealed class RowListAutomationPeer(RowList owner) : ItemsControlAutomationPeer(owner), ISelectionProvider
{
    /// <inheritdoc />
    public bool CanSelectMultiple => false;

    /// <inheritdoc />
    public bool IsSelectionRequired => false;

    /// <inheritdoc />
    public IReadOnlyList<AutomationPeer> GetSelection() => [.. owner.SelectedRows().Select(CreatePeerForElement)];

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

    /// <inheritdoc />
    protected override string? GetNameCore() => NameFor(owner.RowCount);

    /// <summary>Tells the screen reader the list's name changed because its row count did.</summary>
    /// <param name="oldRows">The count before.</param>
    /// <param name="newRows">The count now.</param>
    internal void RowCountChanged(int oldRows, int newRows) =>
        RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, NameFor(oldRows), NameFor(newRows));

    private string NameFor(int rows)
    {
        var page = AutomationProperties.GetName(owner);
        var count = RowSpeech.Rows(rows);
        return string.IsNullOrEmpty(page) ? count : page + ", " + count;
    }
}
