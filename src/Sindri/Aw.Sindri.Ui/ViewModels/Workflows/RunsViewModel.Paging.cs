using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// Show older runs (<see cref="RunsViewModel"/>, continued; #3305): Sindri serves the newest 50 runs and says when older ones exist; each press reads the page past every
/// run shown so far and appends it under the rows, so the rows already shown stay and the list keeps its scroll. Pages are by offset, so a run that came in since pushes the
/// next page down by one: a run already shown is skipped, never listed twice. A re-read of the newest page keeps the older rows, and every run the new page pushed out.
/// </summary>
internal sealed partial class RunsViewModel
{
    private IReadOnlyList<WorkflowRunRowView> _newest = [];
    private List<WorkflowRunRowView> _older = [];
    private bool _olderMore;

    /// <summary>True when Sindri said older runs exist past the ones shown.</summary>
    public bool HasMore { get; private set { Set(ref field, value); OnChanged(nameof(CanShowMore)); } }

    /// <summary>True while the next page's read is on the way; a second press does nothing.</summary>
    public bool IsLoadingMore { get; private set { Set(ref field, value); OnChanged(nameof(CanShowMore)); OnChanged(nameof(ShowMoreText)); } }

    /// <summary>True when the last Show older runs read failed; the rows shown stay.</summary>
    public bool IsMoreFailed { get; private set { Set(ref field, value); OnChanged(nameof(ShowMoreText)); } }

    /// <summary>The sentence for a failed Show older runs read; empty otherwise.</summary>
    public string MoreFailedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The button's words: "Show older runs", "Loading older runs…" while the read is on the way, "Try again" after a failed one.</summary>
    public string ShowMoreText => IsLoadingMore ? Labels.WfRunsLoadingOlder : IsMoreFailed ? Labels.WfTryAgain : Labels.WfRunsShowOlder;

    /// <summary>True when the button can be pressed: older runs exist and no read is on the way.</summary>
    public bool CanShowMore => HasMore && !IsLoadingMore;

    /// <summary>Reads the next page of older runs and appends it under the rows shown.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the page is shown or the failure is.</returns>
    internal async Task ShowMore(Uri url, CancellationToken cancellationToken)
    {
        if (!CanShowMore)
        {
            return;
        }

        var slug = Slug;
        (IsMoreFailed, MoreFailedText) = (false, string.Empty);   // the old failure line hides while the retry is on its way
        IsLoadingMore = true;
        Result<WorkflowRunList> page;
        try
        {
            page = await Api.WorkflowRunsPage(url, slug, _newest.Count + _older.Count, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsLoadingMore = false;   // a cancelled read frees the button too
        }

        if (!string.Equals(slug, Slug, StringComparison.Ordinal))
        {
            return;   // another workflow was opened since: this page is not its
        }

        if (page.IsError)
        {
            (IsMoreFailed, MoreFailedText) = (true, Labels.WfRunsOlderFailed(page.Error));
            return;
        }

        var shown = Known();
        foreach (var run in page.Value.Runs)
        {
            if (shown.Add(run.Id))
            {
                _older.Add(run);
                Rows.Add(new RunRowViewModel(run, Clock));
            }
        }

        _olderMore = page.Value.More;
        HasMore = page.Value.More;
        Recount();
    }

    // The newest page just landed (it rebuilt the rows): the older rows go back under it, with every run the new page pushed out of the old one, and the button follows
    // the last older page while there is one, else the newest page.
    private void Paged(WorkflowRunList newest)
    {
        if (_older.Count > 0)
        {
            var onTop = newest.Runs.Select(static r => r.Id).ToHashSet(StringComparer.Ordinal);
            _older = [.. _newest.Concat(_older).Where(r => onTop.Add(r.Id))];
            foreach (var run in _older)
            {
                Rows.Add(new RunRowViewModel(run, Clock));
            }
        }

        _newest = newest.Runs;
        HasMore = _older.Count > 0 ? _olderMore : newest.More;
        Recount();
    }

    // Another workflow, or one that is off: no older rows and no button.
    private void ForgetOlder()
    {
        (_newest, _older, _olderMore) = ([], [], false);
        (HasMore, IsMoreFailed, MoreFailedText) = (false, false, string.Empty);
    }

    private HashSet<string> Known() => new(_newest.Concat(_older).Select(static r => r.Id), StringComparer.Ordinal);

    private void Recount()
    {
        if (_older.Count == 0)
        {
            return;   // the newest page's own head stands
        }

        var all = _newest.Concat(_older).ToList();
        Head = Labels.WfRunsHead(all.Count(static r => string.Equals(r.Status, "running", StringComparison.Ordinal)), all.Count);
    }
}
