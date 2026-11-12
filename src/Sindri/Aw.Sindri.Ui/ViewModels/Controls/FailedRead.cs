namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>
/// A read that failed or went stale, as the shared FailedReadLine draws it (#3056, audit #3019): plain words, a Retry button
/// and, only when a fix exists, a fix button. A raw code never shows as text; <see cref="Code"/> goes in the tooltip and the help text.
/// </summary>
/// <param name="Words">What happened, in plain language ("Couldn't read the queue just now.").</param>
/// <param name="Retry">Reads again; the Retry button calls it.</param>
internal sealed record FailedRead(string Words, Func<CancellationToken, Task> Retry)
{
    /// <summary>The API's or the app's code, for the tooltip and the help text only; empty when there is none.</summary>
    internal string Code { get; init; } = string.Empty;

    /// <summary>The Retry button's label.</summary>
    internal string RetryText { get; init; } = Labels.Retry;

    /// <summary>The fix button's label; empty when no fix exists.</summary>
    internal string FixLabel { get; init; } = string.Empty;

    /// <summary>What the fix button does; null when no fix exists.</summary>
    internal Func<CancellationToken, Task>? Fix { get; init; }

    /// <summary>True when a fix exists, so the fix button shows.</summary>
    internal bool HasFix => Fix is not null && FixLabel.Length > 0;

    /// <summary>True when there is a code to put in the tooltip.</summary>
    internal bool HasCode => Code.Length > 0;

    /// <summary>The tooltip and help text: the code, or null for none.</summary>
    internal string? Help => HasCode ? Code : null;
}
