using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The instance frame's primary open button (<see cref="InstanceFrameViewModel"/>, continued; api q65): the instance's served open says where the owner acts on it, in the served words.</summary>
internal sealed partial class InstanceFrameViewModel
{
    private DeepLink? _openLink;

    /// <summary>The served words of the open button ("Answer in the child"); empty when none is shown.</summary>
    public string OpenText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the instance names a place to act that is not this frame.</summary>
    public bool HasOpen => _openLink is not null;

    /// <summary>Raised when the open button leads somewhere that is not another instance.</summary>
    internal event EventHandler<FrameLinkEventArgs>? LinkRequested;

    /// <summary>Follows the open button: another instance opens in its frame, anything else through the shell's links.</summary>
    public void OpenServed()
    {
        switch (_openLink)
        {
            case { Kind: DeepLinkKind.Workflow, Subject: { } address }:
                OpenRequested?.Invoke(this, new FrameOpenEventArgs(address, id: null, fromPointer: true));
                break;
            case { } link:
                LinkRequested?.Invoke(this, new FrameLinkEventArgs(link));
                break;
            default:
                break;
        }
    }

    private void ShowOpenCleared()
    {
        _openLink = null;
        OpenText = string.Empty;
        OnChanged(nameof(HasOpen));
    }

    // The route must parse and lead elsewhere: a route to this very frame would be a button that does nothing.
    private void ShowOpen(InstanceDetail detail)
    {
        var served = detail.Open;
        var link = served?.Route is { Length: > 0 } route && DeepLink.TryParse(route, out var parsed)
            && !(parsed.Kind == DeepLinkKind.Workflow && string.Equals(parsed.Subject, detail.Address, StringComparison.Ordinal))
            ? parsed
            : null;
        _openLink = link;
        OpenText = link is null ? string.Empty : served?.Label is { Length: > 0 } label ? label : Labels.OpenInstance;
        OnChanged(nameof(HasOpen));
    }
}
