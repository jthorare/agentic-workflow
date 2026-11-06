using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Facts;

/// <summary>A findings fact's viewer: drawn from its view model; the Full trace button is a toggle bound to its finding, so nothing is rebuilt and focus stays on it.</summary>
internal sealed partial class FindingsFactView : UserControl
{
    public FindingsFactView()
    {
        InitializeComponent();
    }
}
