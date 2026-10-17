using Aw.Redaction;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The 'exec-template' viewer (API P10a, unit U6): spawns the user's terminal command (placeholders expanded, no shell),
/// keeps the first 2 KB of its stderr through <see cref="IRedactor"/>, and watches 3 s for a tmux client. A non-zero exit
/// in the window, or a spawn that fails, is a failure; an exit 0 with no client is unconfirmed, never opened (a
/// single-instance terminal can exit 0 without a window).
/// </summary>
internal sealed class ExecTemplateViewer(ExecTemplateOptions options, ISessionHost host, TimeProvider time, IRedactor? redactor = null) : ITerminalViewer
{
    /// <inheritdoc/>
    public string Kind => "exec-template";

    /// <inheritdoc/>
    public async ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var values = new PlaceholderValues(
            Bin: options.Argv[0],
            Cwd: request.Cwd ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Title: request.Title,
            Name: request.Title,
            Seed: string.Empty,
            Cmd: request.AttachArgv);
        var argv = Placeholders.Expand(options.Argv, values);
        if (argv.IsError)
        {
            return ViewerProcess.Failed(argv.Error.Message);
        }

        var (child, problem) = ViewerProcess.Start(argv.Value, options.Environment);
        if (child is null)
        {
            return ViewerProcess.Failed(problem ?? "the viewer did not start");
        }

        var outcome = await ViewerProcess.Watch(host, time, new ViewerProcess.WatchPlan(request, () => Failure(child), child.Exited), cancellationToken).ConfigureAwait(false);
        ViewerProcess.Reap(child);
        return outcome;
    }

    /// <inheritdoc/>
    public async ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken)
    {
        var found = await ViewerProcess.OnLoginPath(options.Argv[0], cancellationToken).ConfigureAwait(false);
        var gui = ViewerProcess.HasGuiSession(options.GetEnv);
        var problem = !found ? "not-installed" : gui ? null : "no-gui-session";
        var detail = options.Argv.Any(a => a.Contains("{title}", StringComparison.Ordinal))
            ? null
            : "the template has no {title}: windows open untitled";
        return new TerminalFacts(Kind, options.Name, problem is null, "file", ConfigPath: null, problem, detail);
    }

    private async ValueTask<ViewerOutcome?> Failure(ViewerProcess.Child child)
    {
        if (!child.Exited.IsCompleted || child.Process.ExitCode == 0)
        {
            return null;
        }

        var code = child.Process.ExitCode;
        return ViewerProcess.Failed(await ViewerProcess.Detail(child, code, redactor).ConfigureAwait(false));
    }
}
