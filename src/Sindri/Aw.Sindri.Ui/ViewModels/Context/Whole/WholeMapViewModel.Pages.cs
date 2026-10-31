using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A concept's pages and actions (<see cref="WholeMapViewModel"/>, continued, pages spec §3.4): the Why drill lists them beside its decisions and records.</summary>
internal sealed partial class WholeMapViewModel
{
    private string? _conceptKey;

    /// <summary>Gets the Pages and actions section.</summary>
    public PagesSectionViewModel Pages { get; } = new();

    /// <summary>Raised when the section's Try again is pressed: the page reads the concept's map again.</summary>
    internal event EventHandler? PagesRetryRequested;

    /// <summary>Try again on the section.</summary>
    internal void RetryPages() => PagesRetryRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Gets a value indicating whether the section shows: a concept's Why band is drilled into.</summary>
    public bool ShowPages => HasConcept && _drilled == WholeBand.Intent;

    // The section follows the whole map's reads of a concept: loading, failed, or the answer's pages.
    private void PagesFollow(WholeMapView? answer, bool failed = false)
    {
        if (!HasConcept)
        {
            Pages.Clear();
        }
        else if (answer is not null)
        {
            Pages.Show(answer, _conceptKey);
        }
        else if (failed)
        {
            Pages.Failed();
        }
        else
        {
            Pages.Loading();
        }
    }
}
