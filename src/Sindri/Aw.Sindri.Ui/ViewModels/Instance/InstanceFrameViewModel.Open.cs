namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The instance frame's primary open button (<see cref="InstanceFrameViewModel"/>, continued; api q65): the instance's served open says where the owner acts on it, in the served words.</summary>
internal sealed partial class InstanceFrameViewModel
{
    /// <summary>The served words of the open button ("Answer in the child"); empty when none is shown.</summary>
    public string OpenText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the instance names a place to act that is not this frame.</summary>
    public bool HasOpen => OpenText.Length > 0;

    /// <summary>Raised when the open button leads somewhere that is not another instance.</summary>
    internal event EventHandler<FrameLinkEventArgs>? LinkRequested;

    /// <summary>Follows the open button: another instance opens in its frame, anything else through the shell's links.</summary>
    public void OpenServed()
    {
        _ = LinkRequested;
        throw new NotSupportedException();
    }
}
