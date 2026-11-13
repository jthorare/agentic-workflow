using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// The screen-reader face of a flat <see cref="Row"/> (#3189): a list item named by the spoken sentence of its item (state, title, facts, who, age), with no glyph or text nodes under it,
/// that says whether it is selected and which list it belongs to. Its children are the action strip's buttons, and only while the row is hot.
/// </summary>
/// <param name="owner">The row.</param>
internal sealed class RowAutomationPeer(Row owner) : ControlAutomationPeer(owner), ISelectionItemProvider
{
    /// <inheritdoc />
    public bool IsSelected => owner.Classes.Contains("sel");

    /// <inheritdoc />
    public ISelectionProvider? SelectionContainer => owner.FindAncestorOfType<RowList>() is { } list ? CreatePeerForElement(list) as ISelectionProvider : null;

    /// <inheritdoc />
    public void Select() => _ = owner.Focus(NavigationMethod.Unspecified);   // the cursor goes to the row; an open peek follows it

    /// <inheritdoc />
    public void AddToSelection() => Select();   // one row at a time

    /// <inheritdoc />
    public void RemoveFromSelection()
    {
        // The row being peeked can't be unselected by a screen reader; the page closes the peek.
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    /// <inheritdoc />
    protected override bool IsControlElementCore() => true;

    /// <inheritdoc />
    protected override bool IsContentElementCore() => true;

    /// <inheritdoc />
    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() =>
        owner.Strip is { } strip ? [.. strip.GetVisualDescendants().OfType<Button>().Select(CreatePeerForElement)] : [];
}
