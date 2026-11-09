#pragma warning disable CA1822, MA0041, MA0182 // red commit: stubs, replaced by the green commit
using Aw.Sindri.Ui.Api.Connections;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>Stub (red commit).</summary>
internal sealed class ConnectionRowViewModel : ObservableObject
{
    internal ConnectionRowViewModel(ConnectionView view, ConnectionActions actions)
    {
        (View, Actions) = (view, actions);
    }

    internal ConnectionView View { get; }

    internal ConnectionActions Actions { get; }

    public string Name => throw new NotSupportedException();

    public string ChipText => throw new NotSupportedException();

    public string ReasonText => throw new NotSupportedException();

    public string RefreshedText => throw new NotSupportedException();

    public string NoteText => throw new NotSupportedException();

    public bool CanSignIn => throw new NotSupportedException();

    public bool IsConfirmingRemove => throw new NotSupportedException();

    internal event EventHandler? Removed;

    internal void AskRemove() => throw new NotSupportedException();

    internal void Keep() => throw new NotSupportedException();

    internal Task ConfirmRemove(CancellationToken cancellationToken) => throw new NotSupportedException();

    internal Task Test(CancellationToken cancellationToken) => throw new NotSupportedException();

    internal Task SignIn(CancellationToken cancellationToken) => throw new NotSupportedException();

    internal void RaiseRemoved() => Removed?.Invoke(this, EventArgs.Empty);
}
