using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>One open pane. Disposing it detaches and never stops the session.</summary>
public interface IOpenTerminalPane : IAsyncDisposable
{
    /// <summary>Where the pane lives.</summary>
    public TerminalHost Host { get; }

    /// <summary>
    /// The frames, at most 30 a second and none while the screen is unchanged. A key sent through <see cref="SendKey"/> or <see cref="SendText"/>
    /// makes one capture at once instead of waiting for the next tick, and that tick is skipped so the cap holds (perf D14).
    /// </summary>
    /// <param name="cancellationToken">Ends the sequence.</param>
    /// <returns>The frames; the sequence ends after a frame with Dead set, or with SND-AGENT-016 raised when tmux goes away.</returns>
    public IAsyncEnumerable<TerminalFrame> Frames(CancellationToken cancellationToken);

    /// <summary>Sends one named key ('Enter', 'C-c'); a name outside the list is dropped.</summary>
    /// <param name="key">The key name (<see cref="TerminalKeys"/>).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or an invalid error for a name outside the list.</returns>
    public ValueTask<Result<Unit>> SendKey(string key, CancellationToken cancellationToken);

    /// <summary>Sends literal text (at most 4 KiB).</summary>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or an invalid error when the text is over 4 KiB.</returns>
    public ValueTask<Result<Unit>> SendText(string text, CancellationToken cancellationToken);

    /// <summary>Sends one bracketed paste (at most 256 KiB).</summary>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or an invalid error when the text is over 256 KiB.</returns>
    public ValueTask<Result<Unit>> Paste(string text, CancellationToken cancellationToken);

    /// <summary>Sets how Sindri draws; on a foreign socket it never resizes the window.</summary>
    /// <param name="size">The size, 20 to 400 columns and 5 to 200 rows.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or an invalid error for a size out of range.</returns>
    public ValueTask<Result<Unit>> Resize(HostSize size, CancellationToken cancellationToken);
}
