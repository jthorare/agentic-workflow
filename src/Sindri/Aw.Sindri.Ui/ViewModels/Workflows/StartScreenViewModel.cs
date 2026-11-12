#pragma warning disable MA0041, MA0182 // MA0041: stub bodies; MA0182: wired into the page views in the next round

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The start screen of New workflow (spec 4.1): templates A, B and C, then Blank. "Describe it" is not offered before P6.</summary>
internal sealed class StartScreenViewModel : ObservableObject
{
    /// <summary>The entries: A, B, C, Blank.</summary>
    public IReadOnlyList<StartEntry> Entries => throw new NotSupportedException();

    /// <summary>Gets the index of the selected entry (A at first).</summary>
    public int SelectedIndex => throw new NotSupportedException();

    /// <summary>Gets the selected entry.</summary>
    public StartEntry Selected => throw new NotSupportedException();

    /// <summary>Moves the selection; it stays on the screen's edge.</summary>
    /// <param name="move">The direction.</param>
    public void Move(StartMove move) => throw new NotSupportedException();

    /// <summary>Selects an entry by index; out of range does nothing.</summary>
    /// <param name="index">The index.</param>
    public void Select(int index) => throw new NotSupportedException();

    /// <summary>Chooses the selected entry (Enter).</summary>
    /// <returns>The entry chosen.</returns>
    public StartEntry Choose() => throw new NotSupportedException();
}
