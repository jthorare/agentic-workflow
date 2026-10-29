using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One state's cards on the Projects page ("In progress").</summary>
internal sealed class ProjectGroupViewModel : ObservableObject
{
    internal ProjectGroupViewModel(string heading)
    {
        Heading = heading;
    }

    /// <summary>The state's word.</summary>
    public string Heading { get; }

    /// <summary>How many cards.</summary>
    public int Count => Cards.Count;

    /// <summary>The cards, as the server orders them.</summary>
    public ObservableCollection<ProjectCardViewModel> Cards { get; } = [];

    /// <summary>Shows these cards.</summary>
    /// <param name="cards">The cards, in order.</param>
    internal void Show(IReadOnlyList<ProjectCardViewModel> cards)
    {
        Patch.To(Cards, cards);
        OnChanged(nameof(Count));
    }
}
