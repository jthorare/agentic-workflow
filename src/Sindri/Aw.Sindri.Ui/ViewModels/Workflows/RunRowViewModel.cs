using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One run on a workflow's runs list. Red stub.</summary>
internal sealed class RunRowViewModel : ObservableObject
{
    internal RunRowViewModel(WorkflowRunRowView row, TimeProvider clock)
    {
    }

    public string Id => throw new NotSupportedException();

    public string ChipText => throw new NotSupportedException();

    public string TriggerText => throw new NotSupportedException();

    public string RevisionText => throw new NotSupportedException();

    public string WhenText => throw new NotSupportedException();

    public string ErrorText => throw new NotSupportedException();

    public bool HasError => throw new NotSupportedException();

    public string Name => throw new NotSupportedException();
}
