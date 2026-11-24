using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// The one way a view model hands a link or a file to the system: it keeps the launcher's answer, and when the system said no
/// <see cref="Failed"/> is true and <see cref="FailedText"/> holds "Couldn't open the link" with the link to copy. The next launch that works clears it.
/// </summary>
internal sealed class LinkLaunch(Func<ILauncher> launcher) : ObservableObject
{
    /// <summary>A helper over a launcher that does not change.</summary>
    /// <param name="fixedLauncher">The launcher.</param>
    internal LinkLaunch(ILauncher fixedLauncher)
        : this(() => fixedLauncher)
    {
    }

    /// <summary>True when the last launch was refused by the system.</summary>
    public bool Failed { get; private set; }

    /// <summary>The line to show (and copy from) after a refused launch; empty otherwise.</summary>
    public string FailedText { get; private set; } = string.Empty;

    /// <summary>True when the refused launch can be tried again (a link or a file the system said no to; not a failed copy) (#3060).</summary>
    public bool CanRetry => Failed && Again is not null;

    /// <summary>Repeats the last launch the system refused (the "Try again" button).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True when the system opened it this time.</returns>
    public Task<bool> Retry(CancellationToken cancellationToken) => Again is { } again ? again(cancellationToken) : Task.FromResult(false);

    // The last launch, to repeat; null when there is none to repeat.
    private Func<CancellationToken, Task<bool>>? Again { get; set; }

    /// <summary>Hands a web link to the system.</summary>
    /// <param name="uri">The link.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True when the system opened it.</returns>
    public async Task<bool> Open(Uri uri, CancellationToken cancellationToken)
    {
        var opened = await launcher().OpenUri(uri, cancellationToken).ConfigureAwait(true);
        Note(opened, Labels.LaunchFailed(uri.AbsoluteUri), ct => Open(uri, ct));
        return opened;
    }

    /// <summary>Hands a file to the system.</summary>
    /// <param name="path">The file's path.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True when the system opened it.</returns>
    public async Task<bool> OpenPath(string path, CancellationToken cancellationToken)
    {
        var system = launcher();
        var opened = await system.OpenFile(path, cancellationToken).ConfigureAwait(true);
        Note(opened, system.FileExists(path) ? Labels.NoAppOpens(Path.GetExtension(path)) : Labels.FileNotOnMachine, ct => OpenPath(path, ct));
        return opened;
    }

    /// <summary>Records an answer the caller got without launching (a path the app refuses to open).</summary>
    /// <param name="opened">True when the file opened.</param>
    /// <param name="failedText">The line to show when it did not.</param>
    public void Record(bool opened, string failedText) => Note(opened, failedText, again: null);   // nothing was launched, so there is nothing to try again

    // The answer, the line, and the launch "Try again" repeats (null when there is none).
    private void Note(bool opened, string failedText, Func<CancellationToken, Task<bool>>? again)
    {
        if (opened)
        {
            (Failed, FailedText, Again) = (false, string.Empty, null);
        }
        else
        {
            (Failed, FailedText, Again) = (true, failedText, again);
        }

        OnChanged(nameof(Failed));
        OnChanged(nameof(FailedText));
        OnChanged(nameof(CanRetry));
    }

    /// <summary>Makes an owner that exposes LaunchFailed and LaunchFailedText raise their change notifications when this helper's change.</summary>
    /// <param name="onChanged">The owner's change-notification method.</param>
    public void Forward(Action<string> onChanged) =>
        PropertyChanged += (_, e) => onChanged(e.PropertyName switch
        {
            nameof(Failed) => "LaunchFailed",
            nameof(CanRetry) => "CanRetryLaunch",
            _ => "LaunchFailedText",
        });

    /// <summary>Clears the line (another subject was opened).</summary>
    public void Reset() => Record(opened: true, string.Empty);
}
