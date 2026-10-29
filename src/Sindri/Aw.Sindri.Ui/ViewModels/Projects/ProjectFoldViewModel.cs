using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>A fold under the groups ("Finished", "Hidden"): closed until opened, and kept open across reads.</summary>
internal sealed class ProjectFoldViewModel : ObservableObject
{
    internal ProjectFoldViewModel(string title, string why)
    {
        (Title, Why) = (title, why);
    }

    /// <summary>The fold's title.</summary>
    public string Title { get; }

    /// <summary>Why these cards are here; empty for none.</summary>
    public string Why { get; }

    /// <summary>How many cards.</summary>
    public int Count => Cards.Count;

    /// <summary>True when there are cards: a fold with none is not drawn.</summary>
    public bool IsVisible => Count > 0;

    /// <summary>True while the fold is open.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The cards, as the server orders them.</summary>
    public ObservableCollection<ProjectCardViewModel> Cards { get; } = [];

    /// <summary>Opens or closes the fold.</summary>
    internal void Toggle() => IsOpen = !IsOpen;

    /// <summary>Shows these cards.</summary>
    /// <param name="cards">The cards, in order.</param>
    internal void Show(IReadOnlyList<ProjectCardViewModel> cards)
    {
        Patch.To(Cards, cards);
        OnChanged(nameof(Count));
        OnChanged(nameof(IsVisible));
    }
}
