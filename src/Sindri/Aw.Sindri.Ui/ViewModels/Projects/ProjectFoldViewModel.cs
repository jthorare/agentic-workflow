#pragma warning disable CA1822, MA0041, MA0182, S3237, IDE0060, CA1801, S1172 // red stubs: replaced by the implementation commit
using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>A fold under the groups ("Finished", "Hidden"): closed until opened, and kept open across reads.</summary>
internal sealed class ProjectFoldViewModel : ObservableObject
{
    internal ProjectFoldViewModel(string title, string why)
    {
        throw new NotSupportedException();
    }

    /// <summary>The fold's title.</summary>
    public string Title => throw new NotSupportedException();

    /// <summary>Why these cards are here; empty for none.</summary>
    public string Why => throw new NotSupportedException();

    /// <summary>How many cards.</summary>
    public int Count => throw new NotSupportedException();

    /// <summary>True when there are cards: a fold with none is not drawn.</summary>
    public bool IsVisible => throw new NotSupportedException();

    /// <summary>True while the fold is open.</summary>
    public bool IsOpen => throw new NotSupportedException();

    /// <summary>The cards, as the server orders them.</summary>
    public ObservableCollection<ProjectCardViewModel> Cards => throw new NotSupportedException();

    /// <summary>Opens or closes the fold.</summary>
    internal void Toggle() => throw new NotSupportedException();

    /// <summary>Shows these cards.</summary>
    /// <param name="cards">The cards, in order.</param>
    internal void Show(IReadOnlyList<ProjectCardViewModel> cards) => throw new NotSupportedException();
}
