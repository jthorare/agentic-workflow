using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The pages in no concept (<see cref="ContextPageViewModel"/>, continued, pages spec §3.4): the concepts read counts them, the head links them,
/// and the link reads them and lists them in a panel over the concept cards. The page still reads no map until a concept or this link is opened.
/// </summary>
internal sealed partial class ContextPageViewModel
{
    private int _pagesInNoConcept;
    private bool _noConceptOpen;

    /// <summary>Gets the pages in no concept, once the link is opened.</summary>
    public PagesSectionViewModel NoConceptPages { get; } = new();

    /// <summary>Gets a value indicating whether the head shows the link: the concept cards show and some page is in no concept.</summary>
    public bool ShowNoConceptLink => ShowConcepts && _pagesInNoConcept > 0;

    /// <summary>Gets the link's words: "Pages in no concept (12)".</summary>
    public string NoConceptLinkText => Labels.ContextPagesNoConceptLink(_pagesInNoConcept);

    /// <summary>Gets a value indicating whether the panel of the pages in no concept shows: it was opened and the concept cards are on the stage.</summary>
    public bool ShowNoConcept => _noConceptOpen && ShowConcepts;

    /// <summary>Opens the panel and reads the pages in no concept (the link, and Try again).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the pages are listed.</returns>
    internal async Task OpenNoConcept(CancellationToken cancellationToken = default)
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        _noConceptOpen = true;
        NoConceptPages.Loading(noConcept: true);
        ChangedNoConcept();
        try
        {
            var read = await Ports.Api.WholeMap(api, new WholeMapQuery("nodes", Area: null, [], MapVersion: null, Draft: false, Repo: _repo, PagesInNoConcept: true), cancellationToken).ConfigureAwait(true);
            if (read.IsSuccess)
            {
                NoConceptPages.Show(read.Value, noConcept: true);
            }
        }
        finally
        {
            if (NoConceptPages.IsLoading)
            {
                NoConceptPages.Failed();   // a refusal, or a read that threw, is the failure line with Try again
            }
        }
    }

    /// <summary>Closes the panel (its close button, and Esc).</summary>
    /// <returns>True when it was open.</returns>
    internal bool CloseNoConcept()
    {
        if (!_noConceptOpen)
        {
            return false;
        }

        _noConceptOpen = false;
        NoConceptPages.Clear();
        ChangedNoConcept();
        return true;
    }

    // The count from the concepts read; a server that doesn't count them hides the link.
    private void CountNoConcept(ContextConceptsView? view) => _pagesInNoConcept = view?.PagesInNoConcept ?? 0;

    private void ChangedNoConcept()
    {
        OnChanged(nameof(ShowNoConceptLink));
        OnChanged(nameof(NoConceptLinkText));
        OnChanged(nameof(ShowNoConcept));
    }
}
