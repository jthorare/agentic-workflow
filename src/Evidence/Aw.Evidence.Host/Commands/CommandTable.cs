using System.Runtime.InteropServices;
using System.Text.Json;
using Aw.Evidence.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>
/// Command name → handler. Each handler binds its arguments, calls one service and maps the result to an exit code.
/// Ctrl-C, SIGTERM, SIGHUP (the terminal or agent shell closed) and SIGQUIT cancel the command, so its locks are
/// released on the way out (exit 130). A second signal isn't held back: it terminates the process at once.
/// </summary>
internal static class CommandTable
{
    private static readonly string _commands = string.Join(
        '|',
        RunScriptCommand.Name, ParityCommand.Name, ParityPlanCommand.Name, ParityCommentCommand.Name, ParityStaleCommand.Name,
        ParityPutCommand.Name, PublishPlanCommand.Name, DoctorCommand.Name, CheckProvenanceCommand.Name, InstallBrowserCommand.Name, ApiRunCommand.Name, DbRunCommand.Name,
        DesktopRunCommand.Name, GhPublishCommand.Name, VersionCommand.Name);

    internal static async Task<int> Run(string[] args, IServiceProvider provider)
    {
        if (args.Length == 0)
        {
            return CommandOutput.Usage($"<{_commands}> [arguments]");
        }

        using var cancel = new CancellationTokenSource();
        using var term = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => Stop(context, cancel));
        using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, context => Stop(context, cancel));
        using var hangUp = PosixSignalRegistration.Create(PosixSignal.SIGHUP, context => Stop(context, cancel));
        using var quit = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, context => Stop(context, cancel));
        var json = provider.GetRequiredService<JsonSerializerOptions>();   // the one Kernel JSON profile Composition registered
        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            try
            {
                return await Dispatch(args[0], args[1..], scope.ServiceProvider, json, cancel.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                CommandOutput.Message("interrupted");
                return CommandOutput.Interrupted;
            }
            catch (Exception ex)
            {
                // A bug or a failure outside a Result (a cancellation no signal caused among them): still one line and a
                // documented exit (the locks are already released).
                return CommandOutput.Error(EvidenceErrors.RunFailed.With($"the run failed unexpectedly: {FirstLine(ex.Message)}"));
            }
        }
    }

    private static Task<int> Dispatch(string command, string[] rest, IServiceProvider services, JsonSerializerOptions json, CancellationToken cancellationToken) =>
        command switch
        {
            RunScriptCommand.Name => RunScriptCommand.Run(rest, services, json, cancellationToken),
            ParityCommand.Name => ParityCommand.Run(rest, services, json, cancellationToken),
            ParityPlanCommand.Name => ParityPlanCommand.Run(rest, services, json, cancellationToken),
            ParityCommentCommand.Name => ParityCommentCommand.Run(rest, services, cancellationToken),
            ParityStaleCommand.Name => ParityStaleCommand.Run(rest, services, json, cancellationToken),
            ParityPutCommand.Name => ParityPutCommand.Run(rest, services, json, cancellationToken),
            PublishPlanCommand.Name => PublishPlanCommand.Run(rest, services, json, cancellationToken),
            DoctorCommand.Name => DoctorCommand.Run(rest, services, json, cancellationToken),
            CheckProvenanceCommand.Name => CheckProvenanceCommand.Run(rest, services, json, cancellationToken),
            InstallBrowserCommand.Name => InstallBrowserCommand.Run(rest, services, json, cancellationToken),
            ApiRunCommand.Name => ApiRunCommand.Run(rest, services, cancellationToken),
            DbRunCommand.Name => DbRunCommand.Run(rest, services, cancellationToken),
            DesktopRunCommand.Name => DesktopRunCommand.Run(rest, services, cancellationToken),
            GhPublishCommand.Name => GhPublishCommand.Run(rest, services, json, cancellationToken),
            VersionCommand.Name => VersionCommand.Run(rest, cancellationToken),
            _ => Task.FromResult(CommandOutput.Usage($"<{_commands}> [arguments] (unknown command '{command}')")),
        };

    private static void Stop(PosixSignalContext context, CancellationTokenSource cancel)
    {
        if (cancel.IsCancellationRequested)
        {
            return;   // a second signal: the default action terminates the process (the user insists)
        }

        context.Cancel = true;   // the first: let the command unwind and release its locks
        cancel.Cancel();
    }

    private static string FirstLine(string message)
    {
        var end = message.IndexOf('\n', StringComparison.Ordinal);
        return (end < 0 ? message : message[..end]).Trim();
    }
}
