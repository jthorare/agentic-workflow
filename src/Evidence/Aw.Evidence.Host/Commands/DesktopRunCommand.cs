using Aw.Evidence.Features.DesktopRun;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>
/// The 'desktop-run' command: binds its arguments, calls DesktopRun and prints the summary exactly as it was written to summary.json (and
/// nothing else) on stdout. The exit code is the summary's outcome: 0 passed, 2 failed, 1 refused, 4 errored; 75 when a lock stayed busy
/// (no summary then). A refusal or an error also puts its AW-EVIDENCE line on stderr. Without a summary: a usage mistake exits 1 with the usage line;
/// a failure before the manifest is read (the run directory, the manifest file) is an error line and exit 1 (input) or 4 (the environment's I/O).
/// </summary>
internal static class DesktopRunCommand
{
    internal const string Name = "desktop-run";
    internal const string Shape = "desktop-run <screens.json> <run-dir> [--repo dir] [--app-build sha] [--config file] [--screens a,b] [--lock-timeout s]";

    private static readonly CliSpec _spec = new(2, ["--repo", "--app-build", "--config", "--screens", CliArguments.LockTimeoutFlag], []);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is not { } a || a.LockTimeout() is not { } timeout)
        {
            return CommandOutput.Usage(Shape);
        }

        var request = new DesktopRunRequest(
            Path.GetFullPath(a[0]), Path.GetFullPath(a[1]), a.PathValue("--repo") ?? Directory.GetCurrentDirectory(), timeout, a.Value("--app-build"), a.PathValue("--config"),
            a.Value("--screens")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var service = services.GetRequiredService<IService<DesktopRunRequest, DesktopRunResponse>>();
        var result = await service.Handle(request, cancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Error(result.Error);
        }

        var summary = result.Value.Summary;
        Console.Out.Write(result.Value.Document);
        if ((summary.Refusal ?? summary.Error) is { } coded)
        {
            CommandOutput.Message($"{coded.Code} {coded.Message}");
        }

        return summary.ExitCode();
    }
}
