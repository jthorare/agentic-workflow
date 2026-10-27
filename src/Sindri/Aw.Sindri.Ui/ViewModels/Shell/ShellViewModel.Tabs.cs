namespace Aw.Sindri.Ui.ViewModels.Shell;

internal sealed partial class ShellViewModel
{
    /// <summary>True while a top-bar tab is being selected (a pointer, a screen reader or the driver), so the window can leave focus on the tab instead of the page region.</summary>
    internal bool IsSelectingTab { get; private set; }
}
