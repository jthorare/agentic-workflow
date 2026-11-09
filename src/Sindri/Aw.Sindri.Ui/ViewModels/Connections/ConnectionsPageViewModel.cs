#pragma warning disable CA1822, MA0041, MA0182 // red commit: stubs, replaced by the green commit
using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>Stub (red commit).</summary>
internal sealed class ConnectionsPageViewModel(ISindriApi api, ILauncher launcher, TimeProvider clock) : ObservableObject
{
    internal ISindriApi Api { get; } = api;

    internal ILauncher Launcher { get; } = launcher;

    internal TimeProvider Clock { get; } = clock;

    public ObservableCollection<ConnectionRowViewModel> Rows { get; } = [];

    public AddConnectionViewModel Add => throw new NotSupportedException();

    public string Head => throw new NotSupportedException();

    public bool IsEmpty => throw new NotSupportedException();

    public bool IsReading => throw new NotSupportedException();

    public string EmptyText => throw new NotSupportedException();

    internal void SetShown(bool shown) => throw new NotSupportedException();

    internal Task Refresh(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException();
}
