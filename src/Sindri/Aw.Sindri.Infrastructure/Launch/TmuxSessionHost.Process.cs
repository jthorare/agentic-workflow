using System.ComponentModel;
using System.Diagnostics;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Launch;

internal sealed partial class TmuxSessionHost
{
    /// <summary>Whether an environment variable of Sindri's own process stays out of a tmux client that may start the server: the AW_ variables and anything named like a secret.</summary>
    private static bool IsSecretName(string name) =>
        name.StartsWith("AW_", StringComparison.Ordinal) || name.EndsWith("_TOKEN", StringComparison.Ordinal) || name.EndsWith("_API_KEY", StringComparison.Ordinal) || name.EndsWith("_SECRET", StringComparison.Ordinal);

    private async Task<TmuxRun> Exec(IReadOnlyList<string> args, bool socket, TimeSpan? budget, CancellationToken cancellationToken, bool scrub = false)
    {
        var start = ChildEnvironment.Start(options.Executable, socket ? ["-L", options.Socket, .. args] : args);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        _ = start.Environment.Remove("TMUX");   // a pane's own server is never ours
        if (scrub)
        {
            // the first client starts the server and the server keeps that client's environment as its global one (show-environment -g): none of Sindri's secrets goes in
            foreach (var name in start.Environment.Keys.Where(IsSecretName).ToList())
            {
                _ = start.Environment.Remove(name);
            }
        }

        if (options.TmuxTmpDir is { } tmpDir)
        {
            start.Environment["TMUX_TMPDIR"] = tmpDir;
            _ = Directory.CreateDirectory(tmpDir);
        }

        using var process = Process.Start(start) ?? throw new Win32Exception("tmux did not start");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (budget is { } limit)
        {
            deadline.CancelAfter(limit);
        }

        try
        {
            var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            var error = process.StandardError.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            return new TmuxRun(process.ExitCode, (await output.ConfigureAwait(false)).Trim(), (await error.ConfigureAwait(false)).Trim());
        }
        catch (OperationCanceledException)
        {
            await Reap(process).ConfigureAwait(false);   // never leave a tmux client running; the server it may have started is the caller's to clean up
            throw;
        }
    }

    private static async Task Reap(Process process)
    {
        try
        {
            process.Kill();
        }
        catch (InvalidOperationException)
        {
            return;   // it ended between the cancel and the kill
        }

        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await process.WaitForExitAsync(wait.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // killed and still not reaped after 2 s: the OS will; the caller must not hang on it
        }
    }
}
