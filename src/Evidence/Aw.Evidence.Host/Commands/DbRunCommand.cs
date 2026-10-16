using Aw.Evidence.Core.Data;
using Aw.Evidence.Features.DbRun;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>
/// The 'db-run' command: binds its arguments, calls DbRun and prints the summary exactly as it was written to summary.json (and nothing else)
/// on stdout. The exit code is the summary's outcome: 0 passed, 2 failed, 1 refused, 4 errored; 75 when a lock stayed busy (no summary then).
/// A refusal or an error also puts its AW-EVIDENCE line on stderr.
/// </summary>
internal static class DbRunCommand
{
    internal const string Name = "db-run";
    internal const string Shape = "db-run <script.json> <run-dir> --at base|head [--repo dir] [--app-build sha] [--config file] [--lock-timeout s]";

    private static readonly CliSpec _spec = new(2, ["--at", "--repo", "--app-build", "--config", CliArguments.LockTimeoutFlag], []);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is not { } a || a.LockTimeout() is not { } timeout || AtOf(a.Value("--at")) is not { } at)
        {
            return CommandOutput.Usage(Shape);
        }

        var request = new DbRunRequest(
            Path.GetFullPath(a[0]), Path.GetFullPath(a[1]), at, timeout, a.PathValue("--repo") ?? Directory.GetCurrentDirectory(), a.Value("--app-build"), a.PathValue("--config"));
        var result = await services.GetRequiredService<IService<DbRunRequest, DbRunResponse>>().Handle(request, cancellationToken).ConfigureAwait(false);
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

    private static DataRunAt? AtOf(string? value) => value switch
    {
        "base" => DataRunAt.Base,
        "head" => DataRunAt.Head,
        _ => null,
    };
}
