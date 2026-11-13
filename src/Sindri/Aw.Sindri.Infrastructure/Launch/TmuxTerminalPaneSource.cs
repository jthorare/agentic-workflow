using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The tmux <see cref="ITerminalPaneSource"/> (agents spec §15.7, way C): polls 'capture-pane -e -p' on one pane id, up to 30 times a second and only
/// while the stream is open, and types with 'send-keys' and 'paste-buffer -p'. Every call passes <see cref="TmuxAllowlist"/> first and drops TMUX from
/// its environment. It never attaches, never enters copy-mode and never resizes a foreign window. It logs no frame or key.
/// </summary>
internal sealed class TmuxTerminalPaneSource(TmuxHostOptions options, ITerminalPaneLocator locator) : ITerminalPaneSource
{
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

    private sealed class PaneStream(TmuxHostOptions options, TerminalTarget target) : IOpenTerminalPane
    {
        private readonly SemaphoreSlim _kick = new(0);

        public TerminalHost Host { get; } = new(target.SocketIsPath ? "path" : target.Socket, target.Session, target.Pane);

        public async IAsyncEnumerable<TerminalFrame> Frames([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            long seq = 0;
            string? last = null;
            while (!cancellationToken.IsCancellationRequested)
            {
                var facts = await Facts(cancellationToken).ConfigureAwait(false);
                if (facts is null)
                {
                    yield break;   // tmux or the session went away: the socket closes with 4016
                }

                var captured = await Call(["capture-pane", "-e", "-p", "-t", target.Pane], stdin: null, cancellationToken).ConfigureAwait(false);
                if (captured.ExitCode != 0)
                {
                    yield break;
                }

                var rows = Rows(captured.Output, facts.Height);
                var signature = string.Join('\n', rows) + facts.Signature;
                if (!string.Equals(signature, last, StringComparison.Ordinal))
                {
                    last = signature;
                    seq++;
                    yield return new TerminalFrame(seq, rows, facts.CursorX, facts.CursorY, facts.Width, facts.Height, facts.Alt, facts.InMode, facts.Dead);
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

            return await Typed(["send-keys", "-t", target.Pane, key], stdin: null, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<Result<Unit>> SendText(string text, CancellationToken cancellationToken)
        {
            if (Encoding.UTF8.GetByteCount(text) > 4096)
            {
                return SindriErrors.InvalidInput.With("Typed text is over 4 KiB");
            }

            return text.Length == 0
                ? Unit.Value
                : await Typed(["send-keys", "-t", target.Pane, "-l", "--", text], stdin: null, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<Result<Unit>> Paste(string text, CancellationToken cancellationToken)
        {
            if (Encoding.UTF8.GetByteCount(text) > 256 * 1024)
            {
                return SindriErrors.InvalidInput.With("A paste is over 256 KiB");
            }

            var loaded = await Typed(["load-buffer", "-b", PasteBuffer, "-"], text, cancellationToken).ConfigureAwait(false);
            return loaded.IsError
                ? loaded
                : await Typed(["paste-buffer", "-p", "-d", "-b", PasteBuffer, "-t", target.Pane], stdin: null, cancellationToken).ConfigureAwait(false);
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

        public ValueTask DisposeAsync()
        {
            _kick.Dispose();
            return ValueTask.CompletedTask;   // detaching is dropping the polling: nothing was attached, nothing is stopped
        }

        /// <summary>The cursor, size and mode facts of the pane, or null when tmux or the pane is gone.</summary>
        internal async ValueTask<PaneFacts?> Facts(CancellationToken cancellationToken)
        {
            const string format = "#{cursor_x} #{cursor_y} #{pane_width} #{pane_height} #{pane_in_mode} #{alternate_on} #{pane_dead}";
            var run = await Call(["display-message", "-p", "-t", target.Pane, format], stdin: null, cancellationToken).ConfigureAwait(false);
            if (run.ExitCode != 0)
            {
                return null;
            }

            var parts = run.Output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 7 || !parts.All(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            {
                return null;
            }

            var n = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
            return new PaneFacts(n[0], n[1], n[2], n[3], n[4] == 1, n[5] == 1, n[6] == 1);
        }

        private static List<string> Rows(string output, int height)
        {
            var rows = output.Split('\n').ToList();
            if (rows.Count > 0 && rows[^1].Length == 0)
            {
                rows.RemoveAt(rows.Count - 1);
            }

            while (rows.Count < height)
            {
                rows.Add(string.Empty);
            }

            return rows.Count > height && height > 0 ? rows[..height] : rows;
        }

        private async ValueTask<Result<Unit>> Typed(IReadOnlyList<string> args, string? stdin, CancellationToken cancellationToken)
        {
            var run = await Call(args, stdin, cancellationToken).ConfigureAwait(false);
            if (run.ExitCode != 0)
            {
                return SindriErrors.AgentPaneGone.With("tmux refused the call");
            }

            _ = _kick.Release();   // capture right away instead of waiting for the next tick
            return Unit.Value;
        }

        private async Task<TmuxOutput> Call(IReadOnlyList<string> args, string? stdin, CancellationToken cancellationToken)
        {
            var allowed = TmuxAllowlist.Check(target.Foreign ? TmuxList.Foreign : TmuxList.Launch, args);
            if (allowed.IsError)
            {
                return new TmuxOutput(2, string.Empty);
            }

            var socketArgs = target.SocketIsPath ? new[] { "-S", target.Socket } : ["-L", target.Socket];
            var extra = options.TmuxTmpDir is { } tmp ? new Dictionary<string, string?>(StringComparer.Ordinal) { ["TMUX_TMPDIR"] = tmp } : null;
            var start = ChildEnvironment.Start(options.Executable, [.. socketArgs, .. args], extra);
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.RedirectStandardInput = stdin is not null;
            _ = start.Environment.Remove("TMUX");   // a pane's own server is never ours
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_callBudget);
            Process process;
            try
            {
                process = Process.Start(start) ?? throw new Win32Exception("tmux did not start");
            }
            catch (Win32Exception)
            {
                return new TmuxOutput(127, string.Empty);
            }

            using (process)
            {
                return await Collect(process, stdin, deadline.Token).ConfigureAwait(false);
            }
        }

        /// <summary>Writes stdin, reads stdout and waits for the exit; a cancel kills the client, so no tmux client is left running.</summary>
        private static async Task<TmuxOutput> Collect(Process process, string? stdin, CancellationToken cancellationToken)
        {
            try
            {
                if (stdin is not null)
                {
                    await process.StandardInput.WriteAsync(stdin.AsMemory(), cancellationToken).ConfigureAwait(false);
                    process.StandardInput.Close();
                }

                var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
                _ = process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                return new TmuxOutput(process.ExitCode, (await output.ConfigureAwait(false)).TrimEnd('\n'));
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // it ended between the cancel and the kill
                }

                throw;
            }
        }
    }

    private sealed record PaneFacts(int CursorX, int CursorY, int Width, int Height, bool InMode, bool Alt, bool Dead)
    {
        public string Signature => string.Create(CultureInfo.InvariantCulture, $"|{CursorX},{CursorY},{Width},{Height},{InMode},{Alt},{Dead}");
    }

    private readonly record struct TmuxOutput(int ExitCode, string Output);
}
