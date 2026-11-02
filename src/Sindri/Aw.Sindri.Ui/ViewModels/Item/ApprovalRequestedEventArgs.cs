namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>An Allow wants the owner to open the profile approval.</summary>
/// <param name="target">The host or path being allowed.</param>
internal sealed class ApprovalRequestedEventArgs(string target) : EventArgs
{
    /// <summary>The host or path being allowed.</summary>
    public string Target { get; } = target;
}
