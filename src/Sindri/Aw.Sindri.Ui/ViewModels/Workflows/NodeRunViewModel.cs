using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One execution of one node. Red stub.</summary>
internal sealed class NodeRunViewModel : ObservableObject
{
    internal NodeRunViewModel(NodeRunView run, int count)
    {
    }

    public string TitleText => throw new NotSupportedException();

    public string ChipText => throw new NotSupportedException();

    public string InputText => throw new NotSupportedException();

    public bool HasInput => throw new NotSupportedException();

    public string OutputText => throw new NotSupportedException();

    public bool HasOutput => throw new NotSupportedException();

    public bool IsTruncated => throw new NotSupportedException();

    public string TruncatedText => throw new NotSupportedException();

    public string ErrorText => throw new NotSupportedException();

    public bool HasError => throw new NotSupportedException();

    public string Name => throw new NotSupportedException();
}
