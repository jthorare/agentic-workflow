using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One workflow's runs list. Red stub.</summary>
internal sealed class RunsViewModel : ObservableObject
{
    internal RunsViewModel(ISindriApi api, TimeProvider clock)
    {
    }

    public string Slug => throw new NotSupportedException();

    public string Title => throw new NotSupportedException();

    public string DetailText => throw new NotSupportedException();

    public string Head => throw new NotSupportedException();

    public ObservableCollection<RunRowViewModel> Rows => throw new NotSupportedException();

    public bool IsReading => throw new NotSupportedException();

    public bool IsEmpty => throw new NotSupportedException();

    public string EmptyText => throw new NotSupportedException();

    public bool IsFailed => throw new NotSupportedException();

    public string FailedText => throw new NotSupportedException();

    internal Task Read(Uri url, string slug, string name, CancellationToken cancellationToken) => throw new NotSupportedException();
}
