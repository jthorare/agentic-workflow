using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// The Pages and actions section (pages spec §3.4, §3.5): a concept's pages, or the pages in no concept, each with its actions, in the order the answer gives them
/// (owner-named first, then suggested by number of actions). At most <see cref="Cap"/> rows until Show all.
/// </summary>
internal sealed class PagesSectionViewModel : ObservableObject
{
    /// <summary>The most pages listed before Show all.</summary>
    internal const int Cap = 20;

    private List<PageRowViewModel> _all = [];
    private bool _showingAll;
    private bool _noConcept;

    /// <summary>Gets the section's head.</summary>
    public string Head => _noConcept ? Labels.ContextPagesNoConceptHead : Labels.ContextPagesHead;

    /// <summary>Gets the pages shown, in the section's order.</summary>
    public IReadOnlyList<PageRowViewModel> Rows => _showingAll ? _all : [.. _all.Take(Cap)];

    /// <summary>Gets a value indicating whether a read runs.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Gets a value indicating whether the read failed.</summary>
    public bool IsFailed { get; private set; }

    /// <summary>Gets the line while the read runs.</summary>
    public string LoadingText { get; } = Labels.ContextPagesLoading;

    /// <summary>Gets the failure line.</summary>
    public string FailedText => _noConcept ? Labels.ContextPagesNoConceptFailed : Labels.ContextPagesFailed;

    /// <summary>Gets the Try again button's words.</summary>
    public string RetryText { get; } = Labels.ContextPagesRetry;

    /// <summary>Gets a value indicating whether the empty line shows: an answer came and listed no page.</summary>
    public bool IsEmpty => !IsLoading && !IsFailed && _all.Count == 0;

    /// <summary>Gets the empty line.</summary>
    public string EmptyText => _noConcept ? Labels.ContextPagesNoConceptEmpty : Labels.ContextPagesEmpty;

    /// <summary>Gets a value indicating whether Show all shows: more pages than the cap, not all shown yet.</summary>
    public bool HasMore => !_showingAll && _all.Count > Cap;

    /// <summary>Gets the Show all button's words.</summary>
    public string ShowAllText => Labels.ContextPagesShowAll(_all.Count);

    /// <summary>Gets the line the section's status reads out (a polite live region): the loading, failure or empty line, else empty.</summary>
    public string StatusText => IsLoading ? LoadingText : IsFailed ? FailedText : IsEmpty ? EmptyText : string.Empty;

    /// <summary>A read starts: what was listed stays until the answer.</summary>
    /// <param name="noConcept">True for the pages in no concept.</param>
    internal void Loading(bool noConcept = false)
    {
        (_noConcept, IsLoading, IsFailed) = (noConcept, true, false);
        Changed();
    }

    /// <summary>The read failed: the failure line with Try again.</summary>
    internal void Failed()
    {
        (IsLoading, IsFailed) = (false, true);
        Changed();
    }

    /// <summary>Lists the pages of an answer; an answer without a pages list lists none.</summary>
    /// <param name="answer">A concept open, or the pages in no concept.</param>
    /// <param name="concept">The opened concept's key, left out of each action's "also in"; null for the pages in no concept.</param>
    /// <param name="noConcept">True for the pages in no concept.</param>
    internal void Show(WholeMapView answer, string? concept = null, bool noConcept = false)
    {
        ArgumentNullException.ThrowIfNull(answer);
        _all = PageRows.Of(answer, concept);
        (_noConcept, _showingAll, IsLoading, IsFailed) = (noConcept, false, false, false);
        Changed();
    }

    /// <summary>Lists every page past the cap.</summary>
    internal void ShowAll()
    {
        if (HasMore)
        {
            _showingAll = true;
            Changed();
        }
    }

    /// <summary>Empties the section (the concept closed).</summary>
    internal void Clear()
    {
        (_all, _showingAll, IsLoading, IsFailed) = ([], false, false, false);
        Changed();
    }

    private void Changed()
    {
        foreach (var name in (string[])[nameof(Head), nameof(Rows), nameof(IsLoading), nameof(IsFailed), nameof(FailedText), nameof(IsEmpty), nameof(EmptyText), nameof(HasMore), nameof(ShowAllText), nameof(StatusText)])
        {
            OnChanged(name);
        }
    }
}
