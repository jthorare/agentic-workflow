#pragma warning disable CA1822, MA0041, MA0182 // red commit: stubs, replaced by the green commit
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>Stub (red commit).</summary>
internal sealed class ConnectionSheetViewModel(ISindriApi api, ILauncher launcher, TimeProvider clock) : ObservableObject
{
    internal ISindriApi Api { get; } = api;

    internal ILauncher Launcher { get; } = launcher;

    internal TimeProvider Clock { get; } = clock;

    public FramePhase Phase => throw new NotSupportedException();

    public string Heading => throw new NotSupportedException();

    public string ProblemText => throw new NotSupportedException();

    public string SecretLine => throw new NotSupportedException();

    public ConnectionRowViewModel? Row => throw new NotSupportedException();

    internal event EventHandler? Removed;

    internal void Show(string name) => throw new NotSupportedException();

    internal Task Open(Uri url, string name, CancellationToken cancellationToken) => throw new NotSupportedException();

    internal void RaiseRemoved() => Removed?.Invoke(this, EventArgs.Empty);
}
