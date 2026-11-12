using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The Reviews tab (<see cref="ReviewsPageViewModel"/>, continued): the failed read as the shared line.</summary>
internal sealed partial class ReviewsPageViewModel
{
    /// <summary>The last read's failure line; empty when the last read worked.</summary>
    public string Error => ReadFailure?.Words ?? string.Empty;

    /// <summary>The last read's failure as the shared failed-read line draws it (words, Retry, the code behind Details); null when the last read worked.</summary>
    public FailedRead? ReadFailure { get; private set { Set(ref field, value); OnChanged(nameof(Error)); } }

    // The Retry button of the failed line: the same read, at the address the page last used.
    private Task RetryRead(CancellationToken cancellationToken) => _url is { } url ? Read(url, cancellationToken) : Task.CompletedTask;
}
