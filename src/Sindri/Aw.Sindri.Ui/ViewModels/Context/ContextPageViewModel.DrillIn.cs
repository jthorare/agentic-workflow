using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The whole map's drill-in reads (<see cref="ContextPageViewModel"/>, continued; #3674): one page of an open feature's ranked nodes and one tab and page of an open concept's member list, 50 a page, the newest ask winning.</summary>
internal sealed partial class ContextPageViewModel
{
    private int _listRead;   // the newest area page or concept list asked for: an older answer that lands later is dropped

    /// <summary>Opens one page of a feature's ranked nodes (area= and page=, 50 a page).</summary>
    /// <param name="area">The area's key.</param>
    /// <param name="page">Which page, 1-based.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    internal async Task OpenWholeAreaPage(string area, int page, CancellationToken cancellationToken)
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        if (page > 1)
        {
            Whole.ListReading();   // a page turn: skeleton rows; the first read of an area has no list on screen yet
        }

        var asked = ++_listRead;
        var read = await Ports.Api.WholeMap(api, new WholeMapQuery("nodes", area, [], MapVersion: null, Draft: false, Repo: _repo, Concept: OpenedConceptId, Page: page), cancellationToken).ConfigureAwait(true);
        if (asked != _listRead)
        {
            return;   // a newer page was asked for, or the feature was left
        }

        if (!read.IsSuccess && page > 1)
        {
            Whole.ListReadFailed(() => _ = OpenWholeAreaPage(area, page, CancellationToken.None));
        }

        if (read.IsSuccess)
        {
            Whole.ShowArea(read.Value);
            Problem = string.Empty;
        }
        else
        {
            Problem = read.Error.Message;
        }

        OnChanged(nameof(Problem));
        OnChanged(nameof(HasProblem));
        ChangedWhole();
    }

    /// <summary>Opens one tab and page of the open concept's member list (tab=, page=, 50 a page).</summary>
    /// <param name="tab">all, code, feature or record.</param>
    /// <param name="page">Which page, 1-based.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    internal async Task OpenConceptList(string tab, int page, CancellationToken cancellationToken)
    {
        if (Ports.ApiUrl() is not { } api || OpenedConceptId is not { } concept)
        {
            return;
        }

        Whole.ListReading();
        var asked = ++_listRead;
        var read = await Ports.Api.WholeMap(api, new WholeMapQuery("nodes", Area: null, [], MapVersion: null, Draft: false, Repo: _repo, Concept: concept, Page: page, ConceptTab: tab), cancellationToken).ConfigureAwait(true);
        if (asked != _listRead)
        {
            return;   // a newer tab or page was asked for
        }

        if (!read.IsSuccess)
        {
            Whole.ListReadFailed(() => _ = OpenConceptList(tab, page, CancellationToken.None));
        }

        if (read.IsSuccess && string.Equals(OpenedConceptId, concept, StringComparison.Ordinal))
        {
            Whole.Show(read.Value);
            Problem = string.Empty;
        }
        else if (!read.IsSuccess)
        {
            Problem = read.Error.Message;
        }

        OnChanged(nameof(Problem));
        OnChanged(nameof(HasProblem));
        ChangedWhole();
    }
}
