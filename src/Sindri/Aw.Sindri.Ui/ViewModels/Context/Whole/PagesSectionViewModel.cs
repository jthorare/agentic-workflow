using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The Pages and actions section (pages spec §3.4): a concept's pages, or the pages in no concept, each with its actions, in the order the answer gives them.</summary>
internal sealed class PagesSectionViewModel : ObservableObject
{
    /// <summary>Gets the pages, in the section's order.</summary>
    public IReadOnlyList<PageRowViewModel> Rows { get; private set; } = [];

    /// <summary>Lists the pages of an answer that carries them.</summary>
    /// <param name="answer">A concept open, or the pages in no concept.</param>
    internal void Show(WholeMapView answer) => throw new NotSupportedException();
}
