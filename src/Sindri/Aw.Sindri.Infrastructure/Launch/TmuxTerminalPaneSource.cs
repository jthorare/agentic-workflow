using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The tmux <see cref="ITerminalPaneSource"/> (agents spec §15.7, way C): polls 'capture-pane -e -p' on one pane id, up to 30 times a second and only
/// while the stream is open, and types with 'send-keys' and 'paste-buffer -p'. Every call passes <see cref="TmuxAllowlist"/> first and drops TMUX from
/// its environment. It never attaches, never enters copy-mode and never resizes a foreign window. It logs no frame or key.
/// </summary>
internal sealed partial class TmuxTerminalPaneSource(TmuxHostOptions options, ITerminalPaneLocator locator) : ITerminalPaneSource
{
    private const int TimedOut = 124;   // the exit code timeout(1) uses
    private const string PasteBuffer = "sindri-paste";
    private static readonly TimeSpan _tick = TimeSpan.FromMilliseconds(33);   // 30 a second
    private static readonly TimeSpan _callBudget = TimeSpan.FromSeconds(5);

    /// <inheritdoc/>
    public async ValueTask<Result<IOpenTerminalPane>> Open(SessionRef session, HostSize? size, CancellationToken cancellationToken)
    {
        var found = await locator.Find(session, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

#pragma warning disable CA2000 // disposed in the finally below on every failure path; on success it is handed to the caller
        PaneStream? stream = new(options, found.Value);
#pragma warning restore CA2000
        try
        {
            if (await stream.Facts(cancellationToken).ConfigureAwait(false) is null)
            {
                return SindriErrors.AgentPaneGone.With("tmux or the session is gone");
            }

            var opened = stream;
            stream = null;   // the caller owns it now
            return opened;
        }
        finally
        {
            if (stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private sealed partial class PaneStream(TmuxHostOptions options, TerminalTarget target) : IOpenTerminalPane
    {
        private readonly SemaphoreSlim _kick = new(0);
        private readonly Lock _view = new();   // guards _back, _hist and _alt: the pump reads them, the receive loop sets them
        private int _back;   // rows above the live screen the view shows; 0 is live
        private int _hist;   // the history size of the last capture, 0 on the alternate screen
        private bool _alt;
        private long _seq;   // per stream, not per enumeration: a reader that comes back gets the next number, so frames always count up

        public TerminalHost Host { get; } = new(target.SocketIsPath ? "path" : target.Socket, target.Session, target.Pane);

        public async IAsyncEnumerable<TerminalFrame> Frames([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            string? last = null;   // a new reader gets the current screen at once
            while (!cancellationToken.IsCancellationRequested)
            {
                var facts = await Facts(cancellationToken).ConfigureAwait(false);
                if (facts is null)
                {
                    yield break;   // tmux or the session went away: the socket closes with 4016
                }

                var (back, hist) = View(facts);
                string[] captureArgs = back == 0
                    ? ["capture-pane", "-e", "-p", "-t", target.Pane]
                    : [
                        "capture-pane", "-e", "-p", "-t", target.Pane,
                        "-S", (-back).ToString(CultureInfo.InvariantCulture),
                        "-E", (facts.Height - 1 - back).ToString(CultureInfo.InvariantCulture),
                    ];
                var captured = await Call(captureArgs, stdin: null, cancellationToken).ConfigureAwait(false);
                if (captured.ExitCode != 0)
                {
                    yield break;
                }

                var rows = Rows(captured.Output, facts.Height);
                var signature = string.Join('\n', rows) + facts.Signature + "|" + back.ToString(CultureInfo.InvariantCulture) + "," + hist.ToString(CultureInfo.InvariantCulture);
                if (!string.Equals(signature, last, StringComparison.Ordinal))
                {
                    last = signature;
                    yield return new TerminalFrame(Interlocked.Increment(ref _seq), rows, facts.CursorX, facts.CursorY, facts.Width, facts.Height, facts.Alt, facts.InMode, facts.Dead) { Back = back, Hist = hist };
                    if (facts.Dead)
                    {
                        yield break;
                    }
                }

                // The next capture is a tick away, or at once after a key (perf D14); the tick after a kicked capture is still a full tick, so the cap holds.
                _ = await _kick.WaitAsync(_tick, cancellationToken).ConfigureAwait(false);
            }
        }

        public async ValueTask<Result<Unit>> SendKey(string key, CancellationToken cancellationToken)
        {
            var named = TerminalKeys.Check(key);
            if (named.IsError)
            {
                return named;
            }

            return Live(await Typed(["send-keys", "-t", target.Pane, key], stdin: null, cancellationToken).ConfigureAwait(false));
        }

        public async ValueTask<Result<Unit>> SendText(string text, CancellationToken cancellationToken)
        {
            if (Encoding.UTF8.GetByteCount(text) > 4096)
            {
                return SindriErrors.InvalidInput.With("Typed text is over 4 KiB");
            }

            return text.Length == 0
                ? Unit.Value
                : Live(await Typed(["send-keys", "-t", target.Pane, "-l", "--", EscapeTrailingSemicolon(text)], stdin: null, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>
        /// tmux reads an argument that ends in ';' as a command separator, even after '-l --', and drops that ';' (so "ls;" types "ls"). An argument that ends
        /// in '\;' loses one backslash and keeps the ';', so one more backslash before the last ';' types the text exactly (checked on tmux 3.4 for 0 to 6 backslashes).
        /// </summary>
        private static string EscapeTrailingSemicolon(string text) => text.EndsWith(';') ? string.Concat(text.AsSpan(0, text.Length - 1), "\\;") : text;

        public async ValueTask<Result<Unit>> Paste(string text, CancellationToken cancellationToken)
        {
            if (Encoding.UTF8.GetByteCount(text) > 256 * 1024)
            {
                return SindriErrors.InvalidInput.With("A paste is over 256 KiB");
            }

            var loaded = await Typed(["load-buffer", "-b", PasteBuffer, "-"], text, cancellationToken).ConfigureAwait(false);
            return loaded.IsError
                ? loaded
                : Live(await Typed(["paste-buffer", "-p", "-d", "-b", PasteBuffer, "-t", target.Pane], stdin: null, cancellationToken).ConfigureAwait(false));
        }

        public async ValueTask<Result<Unit>> Resize(HostSize newSize, CancellationToken cancellationToken)
        {
            if (newSize.Cols is < 20 or > 400 || newSize.Rows is < 5 or > 200)
            {
                return SindriErrors.InvalidInput.With("A size is 20 to 400 columns and 5 to 200 rows");
            }

            if (target.Foreign)   // only how Sindri draws changes; a foreign window is never resized
            {
                return Unit.Value;
            }

            return await Typed(
                ["resize-window", "-t", target.Session, "-x", newSize.Cols.ToString(CultureInfo.InvariantCulture), "-y", newSize.Rows.ToString(CultureInfo.InvariantCulture)],
                stdin: null,
                cancellationToken).ConfigureAwait(false);
        }

        public ValueTask<Result<Unit>> Scroll(int back, CancellationToken cancellationToken)
        {
            lock (_view)
            {
                _back = _alt ? 0 : Math.Clamp(back, 0, _hist);   // clamped to the history of the last capture; the next capture clamps again
            }

            _ = _kick.Release();   // capture right away, like a key
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }

        /// <summary>A key, text or paste that went through returns the view to the live screen.</summary>
        private Result<Unit> Live(Result<Unit> sent)
        {
            if (sent.IsSuccess)
            {
                lock (_view)
                {
                    _back = 0;
                }
            }

            return sent;
        }

        /// <summary>
        /// The offset and history size this capture shows. The alternate screen has no scrollback, so entering it drops the offset (leaving it lands live). While
        /// scrolled, rows that arrived since the last capture move the offset with them, so the view stays on the same lines; then it is clamped to the history.
        /// </summary>
        private (int Back, int Hist) View(PaneFacts facts)
        {
            lock (_view)
            {
                _alt = facts.Alt;
                var hist = facts.Alt ? 0 : facts.Hist;
                if (facts.Alt)
                {
                    _back = 0;
                }
                else if (_back > 0 && hist > _hist)
                {
                    _back += hist - _hist;
                }

                _back = Math.Clamp(_back, 0, hist);
                _hist = hist;
                return (_back, hist);
            }
        }

        public ValueTask DisposeAsync()
        {
            _kick.Dispose();
            return ValueTask.CompletedTask;   // detaching is dropping the polling: nothing was attached, nothing is stopped
        }
    }

    private sealed record PaneFacts(int CursorX, int CursorY, int Width, int Height, bool InMode, bool Alt, bool Dead, int Hist)
    {
        public string Signature => string.Create(CultureInfo.InvariantCulture, $"|{CursorX},{CursorY},{Width},{Height},{InMode},{Alt},{Dead}");
    }

    private readonly record struct TmuxOutput(int ExitCode, string Output);
}
