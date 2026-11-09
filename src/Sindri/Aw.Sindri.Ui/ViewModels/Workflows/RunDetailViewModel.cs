using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One run with every node's status, input, output and error. Red stub.</summary>
internal sealed class RunDetailViewModel : ObservableObject
{
    internal RunDetailViewModel(ISindriApi api, TimeProvider clock)
    {
    }

    public string Id => throw new NotSupportedException();

    public string TitleText => throw new NotSupportedException();

    public string ChipText => throw new NotSupportedException();

    public string TriggerText => throw new NotSupportedException();

    public string RevisionText => throw new NotSupportedException();

    public string WhenText => throw new NotSupportedException();

    public string ErrorText => throw new NotSupportedException();

    public bool HasError => throw new NotSupportedException();

    public string OutputsText => throw new NotSupportedException();

    public bool HasOutputs => throw new NotSupportedException();

    public IReadOnlyList<NodeGroupViewModel> Groups => throw new NotSupportedException();

    public bool IsReading => throw new NotSupportedException();

    public bool IsFailed => throw new NotSupportedException();

    public string FailedText => throw new NotSupportedException();

    internal Task Read(Uri url, string runId, CancellationToken cancellationToken) => throw new NotSupportedException();
}
