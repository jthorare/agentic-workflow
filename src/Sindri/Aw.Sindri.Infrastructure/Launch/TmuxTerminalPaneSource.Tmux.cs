using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The tmux calls of a pane stream: the pane's facts, one allowlisted client call under the 5 s budget, and its output.</summary>
internal sealed partial class TmuxTerminalPaneSource
{
    private sealed partial class PaneStream
    {
        /// <summary>The cursor, size and mode facts of the pane, or null when tmux or the pane is gone.</summary>
        internal async ValueTask<PaneFacts?> Facts(CancellationToken cancellationToken)
        {
            const string format = "#{cursor_x} #{cursor_y} #{pane_width} #{pane_height} #{pane_in_mode} #{alternate_on} #{pane_dead} #{history_size}";
            var run = await Call(["display-message", "-p", "-t", target.Pane, format], stdin: null, cancellationToken).ConfigureAwait(false);
            if (run.ExitCode != 0)
            {
                return null;
            }

            var parts = run.Output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 8 || !parts.All(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            {
                return null;
            }

            var n = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
            return new PaneFacts(n[0], n[1], n[2], n[3], n[4] == 1, n[5] == 1, n[6] == 1, n[7]);
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
                    return await Collect(process, stdin, deadline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // The 5 s budget fired, not the caller: a failed call (the stream closes with 4016 or the key is refused), never a silent cancel.
                    return new TmuxOutput(TimedOut, string.Empty);
                }
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
}
