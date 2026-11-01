namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The Accept all suggested mode (<see cref="AddSeveralPagesViewModel"/>, continued).</summary>
internal sealed partial class AddSeveralPagesViewModel
{
    private const int PageCap = 200;

    /// <summary>Reads the section's suggested pages, how many pages the profile holds and what waits, and draws one ticked row per page.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the rows are drawn.</returns>
    internal Task LoadSuggested(CancellationToken cancellationToken)
    {
        _ = (PageCap, _acceptConcept, cancellationToken);
        throw new NotSupportedException();
    }
}
