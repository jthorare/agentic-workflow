using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
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
    public async ValueTask<Result<ITerminalPaneStream>> Open(string provider, string id, HostSize? size, CancellationToken cancellationToken)
    {
        var found = await locator.Find(provider, id, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var stream = new Stream(options, found.Value, size);
        var probe = await stream.Facts(cancellationToken).ConfigureAwait(false);
        if (probe is null)
        {
            return SindriErrors.AgentPaneGone.With("tmux or the session is gone");
        }

        return stream;
    }

    private sealed class Stream(TmuxHostOptions options, TerminalTarget target, HostSize? size) : ITerminalPaneStream
    {
        private readonly SemaphoreSlim _kick = new(0);
        private HostSize? _size = size;

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

                var captured = await Call(["capture-pane", "-e", "-p", "-t", target.Pane], null, cancellationToken).ConfigureAwait(false);
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

            return await Typed(["send-keys", "-t", target.Pane, key], null, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<Result<Unit>> SendText(string text, CancellationToken cancellationToken)
        {
            if (Encoding.UTF8.GetByteCount(text) > 4096)
            {
                return SindriErrors.InvalidInput.With("Typed text is over 4 KiB");
            }

            return text.Length == 0
                ? Unit.Value
                : await Typed(["send-keys", "-t", target.Pane, "-l", "--", text], null, cancellationToken).ConfigureAwait(false);
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
                : await Typed(["paste-buffer", "-p", "-d", "-b", PasteBuffer, "-t", target.Pane], null, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<Result<Unit>> Resize(HostSize newSize, CancellationToken cancellationToken)
        {
            if (newSize.Cols is < 20 or > 400 || newSize.Rows is < 5 or > 200)
            {
                return SindriErrors.InvalidInput.With("A size is 20 to 400 columns and 5 to 200 rows");
            }

            _size = newSize;   // how Sindri draws; a foreign window is never resized
            if (target.Foreign)
            {
                return Unit.Value;
            }

            return await Typed(
                ["resize-window", "-t", target.Session, "-x", newSize.Cols.ToString(CultureInfo.InvariantCulture), "-y", newSize.Rows.ToString(CultureInfo.InvariantCulture)],
                null,
                cancellationToken).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            _kick.Dispose();
            return ValueTask.CompletedTask;   // detaching is dropping the polling: nothing was attached, nothing is stopped
        }

        /// <summary>The cursor, size and mode facts of the pane, or null when tmux or the pane is gone.</summary>
        public async ValueTask<PaneFacts?> Facts(CancellationToken cancellationToken)
        {
            const string format = "#{cursor_x} #{cursor_y} #{pane_width} #{pane_height} #{pane_in_mode} #{alternate_on} #{pane_dead}";
            var run = await Call(["display-message", "-p", "-t", target.Pane, format], null, cancellationToken).ConfigureAwait(false);
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
                try
                {
                    if (stdin is not null)
                    {
                        await process.StandardInput.WriteAsync(stdin.AsMemory(), deadline.Token).ConfigureAwait(false);
                        process.StandardInput.Close();
                    }

                    var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
                    _ = process.StandardError.ReadToEndAsync(deadline.Token);
                    await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
                    return new TmuxOutput(process.ExitCode, (await output.ConfigureAwait(false)).TrimEnd('\n'));
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        process.Kill();   // never leave a tmux client running
                    }
                    catch (InvalidOperationException)
                    {
                        // it ended between the cancel and the kill
                    }

                    throw;
                }
            }
        }
    }

    private sealed record PaneFacts(int CursorX, int CursorY, int Width, int Height, bool InMode, bool Alt, bool Dead)
    {
        public string Signature => $"|{CursorX},{CursorY},{Width},{Height},{InMode},{Alt},{Dead}";
    }

    private readonly record struct TmuxOutput(int ExitCode, string Output);
}
