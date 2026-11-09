using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One workflow on the list. Red stub.</summary>
internal sealed class WorkflowRowViewModel : ObservableObject
{
    internal WorkflowRowViewModel(WorkflowRowView row, TimeProvider clock)
    {
    }

    public string Slug => throw new NotSupportedException();

    public string NameText => throw new NotSupportedException();

    public string StateText => throw new NotSupportedException();

    public string RevisionText => throw new NotSupportedException();

    public string ChangedText => throw new NotSupportedException();

    public string Name => throw new NotSupportedException();

    public bool IsActive => throw new NotSupportedException();
}
