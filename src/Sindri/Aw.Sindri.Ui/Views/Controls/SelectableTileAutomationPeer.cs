using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The automation peer of <see cref="SelectableTile"/> (#3234): a button that also answers the selection-item pattern, so a screen reader says "selected" on the chosen one.
/// A tile of class "row" reads as a list item, a plain tile as a button. Selecting it presses it, as a click does.
/// </summary>
/// <param name="owner">The tile.</param>
internal sealed class SelectableTileAutomationPeer(SelectableTile owner) : ButtonAutomationPeer(owner), ISelectionItemProvider
{
    private readonly SelectableTile _owner = owner;

    /// <inheritdoc />
    public bool IsSelected => _owner.IsSelected;

    /// <inheritdoc />
    public ISelectionProvider? SelectionContainer => null;

    /// <inheritdoc />
    public void Select() => Invoke();

    /// <inheritdoc />
    public void AddToSelection() => throw new InvalidOperationException("A tile is one choice; pressing it selects it.");

    /// <inheritdoc />
    public void RemoveFromSelection() => throw new InvalidOperationException("A tile is one choice; the page deselects it by choosing another.");

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        _owner.Classes.Contains("row") ? AutomationControlType.ListItem : AutomationControlType.Button;
}
