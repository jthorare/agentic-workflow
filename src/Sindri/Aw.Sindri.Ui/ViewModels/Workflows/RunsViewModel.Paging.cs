namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>Show older runs (<see cref="RunsViewModel"/>, continued; #3305): the runs past the newest page, appended under it while Sindri says more exist.</summary>
internal sealed partial class RunsViewModel
{
    /// <summary>True when Sindri said older runs exist past the ones shown.</summary>
    public bool HasMore { get; private set => Set(ref field, value); }

    /// <summary>True while the next page's read is on the way; a second press does nothing.</summary>
    public bool IsLoadingMore { get; private set => Set(ref field, value); }

    /// <summary>True when the last Show older runs read failed; the rows shown stay.</summary>
    public bool IsMoreFailed { get; private set => Set(ref field, value); }

    /// <summary>The sentence for a failed Show older runs read; empty otherwise.</summary>
    public string MoreFailedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The button's words: "Show older runs", "Loading older runs…" while the read is on the way, "Try again" after a failed one.</summary>
    public string ShowMoreText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the button can be pressed: older runs exist and no read is on the way.</summary>
    public bool CanShowMore => HasMore && !IsLoadingMore;

    /// <summary>Reads the next page of older runs and appends it under the rows shown.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the page is shown or the failure is.</returns>
    internal Task ShowMore(Uri url, CancellationToken cancellationToken)
    {
        _ = (url, cancellationToken);
        throw new NotSupportedException();
    }
}
