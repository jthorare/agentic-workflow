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

    /// <summary>Hands a web link to the system.</summary>
    /// <param name="uri">The link.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True when the system opened it.</returns>
    public async Task<bool> Open(Uri uri, CancellationToken cancellationToken)
    {
        var opened = await launcher().OpenUri(uri, cancellationToken).ConfigureAwait(true);
        Record(opened, Labels.LaunchFailed(uri.AbsoluteUri));
        return opened;
    }

    /// <summary>Hands a file to the system.</summary>
    /// <param name="path">The file's path.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True when the system opened it.</returns>
    public async Task<bool> OpenPath(string path, CancellationToken cancellationToken)
    {
        var opened = await launcher().OpenFile(path, cancellationToken).ConfigureAwait(true);
        Record(opened, Labels.FileNotOpened(path));
        return opened;
    }

    /// <summary>Records an answer the caller got without launching (a path the app refuses to open).</summary>
    /// <param name="opened">True when the file opened.</param>
    /// <param name="failedText">The line to show when it did not.</param>
    public void Record(bool opened, string failedText)
    {
        (Failed, FailedText) = opened ? (false, string.Empty) : (true, failedText);
        OnChanged(nameof(Failed));
        OnChanged(nameof(FailedText));
    }

    /// <summary>Makes an owner that exposes LaunchFailed and LaunchFailedText raise their change notifications when this helper's change.</summary>
    /// <param name="onChanged">The owner's change-notification method.</param>
    public void Forward(Action<string> onChanged) =>
        PropertyChanged += (_, e) => onChanged(string.Equals(e.PropertyName, nameof(Failed), StringComparison.Ordinal) ? "LaunchFailed" : "LaunchFailedText");

    /// <summary>Clears the line (another subject was opened).</summary>
    public void Reset() => Record(opened: true, string.Empty);
}
