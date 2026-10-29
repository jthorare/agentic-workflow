#pragma warning disable CA1822, MA0041, MA0182, S3237, IDE0060, CA1801, S1172 // red stubs: replaced by the implementation commit
using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One state's cards on the Projects page ("In progress").</summary>
internal sealed class ProjectGroupViewModel : ObservableObject
{
    internal ProjectGroupViewModel(string heading)
    {
        throw new NotSupportedException();
    }

    /// <summary>The state's word.</summary>
    public string Heading => throw new NotSupportedException();

    /// <summary>How many cards.</summary>
    public int Count => throw new NotSupportedException();

    /// <summary>The cards, as the server orders them.</summary>
    public ObservableCollection<ProjectCardViewModel> Cards => throw new NotSupportedException();

    /// <summary>Shows these cards.</summary>
    /// <param name="cards">The cards, in order.</param>
    internal void Show(IReadOnlyList<ProjectCardViewModel> cards) => throw new NotSupportedException();
}
