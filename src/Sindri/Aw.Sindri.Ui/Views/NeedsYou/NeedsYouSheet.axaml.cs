using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.NeedsYou;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The sheet of a Needs-you row.</summary>
internal sealed partial class NeedsYouSheet : UserControl
{
    public NeedsYouSheet()
    {
        InitializeComponent();
    }
}
#pragma warning restore MA0182
