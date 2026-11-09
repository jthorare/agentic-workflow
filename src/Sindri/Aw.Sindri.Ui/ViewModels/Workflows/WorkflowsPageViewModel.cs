using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page, read-only (#2258 PR 17 part 1): the workflow list, one workflow's runs and one run's detail. Red stub: every member throws until the green commit.</summary>
internal sealed class WorkflowsPageViewModel : ObservableObject
{
    internal WorkflowsPageViewModel(ISindriApi api, TimeProvider clock)
    {
    }

    public string Title => throw new NotSupportedException();

    public string Purpose => throw new NotSupportedException();

    public string ReadingText => throw new NotSupportedException();

    public string UnreadableText => throw new NotSupportedException();

    public string RetryText => throw new NotSupportedException();

    public string BackText => throw new NotSupportedException();

    public ObservableCollection<WorkflowRowViewModel> Rows => throw new NotSupportedException();

    public string Head => throw new NotSupportedException();

    public bool IsReading => throw new NotSupportedException();

    public bool IsEmpty => throw new NotSupportedException();

    public string EmptyText => throw new NotSupportedException();

    public bool IsUnreadable => throw new NotSupportedException();

    public bool IsFailed => throw new NotSupportedException();

    public string FailedText => throw new NotSupportedException();

    public bool IsList => throw new NotSupportedException();

    public bool IsRuns => throw new NotSupportedException();

    public bool IsRun => throw new NotSupportedException();

    public RunsViewModel Runs => throw new NotSupportedException();

    public RunDetailViewModel Run => throw new NotSupportedException();

    internal Task Show(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException();

    internal void Leave() => throw new NotSupportedException();

    internal Task OpenWorkflow(string slug, CancellationToken cancellationToken) => throw new NotSupportedException();

    internal Task OpenRun(string runId, CancellationToken cancellationToken) => throw new NotSupportedException();

    internal void Back() => throw new NotSupportedException();
}
