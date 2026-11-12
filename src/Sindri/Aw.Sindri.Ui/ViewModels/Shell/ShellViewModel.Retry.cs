using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The shell's Retry for stalled views (<see cref="ShellViewModel"/>, continued; #3056).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The "showing the last rows read" line with Retry, for the banner on Home's Needs you panel while the views are stalled.</summary>
    public FailedRead StalledRead => field ??= new FailedRead(Labels.RowsStaleNote, RetryViews) { IsAttention = true };

    // Re-reads every followed view, as the follower does after an event; nothing to read while not connected.
    private Task RetryViews(CancellationToken cancellationToken) => Health.ApiUrl() is { } url ? Health.RefreshViews(url, cancellationToken) : Task.CompletedTask;
}
