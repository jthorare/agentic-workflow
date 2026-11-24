using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// Open file on the node card (#4220): the file on this machine that the node read serves, opened the way the whole map's node panel opens it
/// (the same words, the launcher's file open, a refusal shown in LaunchFailedText). Hidden when the read serves no path on this machine.
/// </summary>
internal sealed partial class NodeCardViewModel
{
    private string? _localPath;

    /// <summary>Gets a value indicating whether the node's source is a file on this machine that the card can open.</summary>
    public bool HasOpenFile => _localPath is { Length: > 0 };

    /// <summary>Gets the open-file button's words.</summary>
    public string OpenFileText { get; } = Labels.ContextNodePanelOpenFile;

    /// <summary>Gets the open-file button's accessible name.</summary>
    public string OpenFileName { get; } = Labels.ContextNodePanelOpenFileName;

    /// <summary>Opens the node's file on this machine with the editor (the launcher's file open).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>When the system answered.</returns>
    internal async Task OpenFile(CancellationToken cancellationToken = default)
    {
        if (_localPath is { Length: > 0 } path)
        {
            _ = await Launch.OpenPath(path, cancellationToken).ConfigureAwait(true);
        }
    }

    // The file the node read serves: a path only when the source is a file in a checkout on this machine (and the caller a person).
    private void TakeFile(NodeView view)
    {
        _localPath = view.Node.Source?.Path;
        OnChanged(nameof(HasOpenFile));
    }

    // A new node, or a closed card, has no file until the node read serves one.
    private void ForgetFile()
    {
        _localPath = null;
        OnChanged(nameof(HasOpenFile));
    }
}
