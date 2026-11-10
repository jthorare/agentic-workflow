namespace Aw.Sindri.Ui.ViewModels.Workflows;

internal sealed partial class GraphEditViewModel
{
    // What the edit bar shows: nothing, the block picker, the connect list or the remove confirm.
    private enum Mode
    {
        None,
        Add,
        Connect,
        Remove,
    }
}
