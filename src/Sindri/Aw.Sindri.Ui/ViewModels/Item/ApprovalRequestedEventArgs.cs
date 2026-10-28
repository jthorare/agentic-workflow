namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>An Allow wants the owner to open the profile approval.</summary>
/// <param name="approval">The approval's id.</param>
/// <param name="target">The host or path being allowed.</param>
internal sealed class ApprovalRequestedEventArgs(string approval, string target) : EventArgs
{
    /// <summary>The approval's id.</summary>
    public string Approval { get; } = approval;

    /// <summary>The host or path being allowed.</summary>
    public string Target { get; } = target;
}
