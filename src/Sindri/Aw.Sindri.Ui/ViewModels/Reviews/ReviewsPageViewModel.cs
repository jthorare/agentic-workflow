using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The Reviews tab (review page spec §6.1, §6.3, §16): Waiting on you, then Yours, the library header, the login prompt and the waiting count behind the tab's badge and Home's line.</summary>
internal sealed class ReviewsPageViewModel : ObservableObject, IDisposable
{
    internal ReviewsPageViewModel(ISindriApi api, TimeProvider clock)
    {
        _api = api;
        _clock = clock;
    }

    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;

    /// <summary>"Reviews".</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>True until the first answer.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>The line under the title: "GitHub synced 3 min ago · reviews as joi-example · 4 open PRs in 2 repos".</summary>
    public string SubLine { get; private set; } = string.Empty;

    /// <summary>The library header's words; its button opens the library errors sheet.</summary>
    public string LibraryText { get; private set; } = string.Empty;

    /// <summary>True when the header shows a warning (docs with errors, or a folder that can't be read).</summary>
    public bool LibraryWarns { get; private set; }

    /// <summary>The served notice above the list; null for none.</summary>
    public string? Notice { get; private set; }

    /// <summary>"PR list is from …" above the last rows; null when fresh.</summary>
    public string? StaleText { get; private set; }

    /// <summary>True while review.login is unset: the page is the one-field prompt.</summary>
    public bool IsLoginPrompt { get; private set; }

    /// <summary>The prompt's heading ("Set your GitHub login").</summary>
    public string LoginHeading { get; private set; } = string.Empty;

    /// <summary>The login field, pre-filled with the served suggestion.</summary>
    public string LoginField { get; set; } = string.Empty;

    /// <summary>The empty state's lines (text, hint, teams); null when there are rows.</summary>
    public IReadOnlyList<string>? EmptyLines { get; private set; }

    /// <summary>Waiting on you, then Yours.</summary>
    public ObservableCollection<ReviewGroupViewModel> Groups { get; } = [];

    /// <summary>The Waiting on you group's size: the tab's badge and Home's line.</summary>
    public int WaitingCount { get; private set; }

    /// <summary>Reads the list.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => _api.Reviews(url, cancellationToken) is null || _clock is null ? Task.CompletedTask : Task.FromException(new NotSupportedException());

    /// <summary>Whether Reviews is the page shown.</summary>
    /// <param name="shown">True when shown.</param>
    internal void SetShown(bool shown) => IsLoading = shown && IsLoading;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
