using System.Text.Json;
using Aw.Evidence.Features.RunScript;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>The 'run' command: binds its arguments, calls RunScript and prints the summary (exit 2 when a step failed or broke).</summary>
internal static class RunScriptCommand
{
    internal const string Name = "run";
    internal const string Shape =
        "run <script.json> <run-dir> [--baseline png] [--app-build sha] [--fixtures id] [--cache verdict-cache.json] [--at base|head] " +
        "[--host url] [--allow-preview-host] [--allow-writes] [--lock-timeout s]";

    private static readonly CliSpec _spec = new(
        2,
        ["--baseline", "--app-build", "--fixtures", "--cache", "--host", "--at", CliArguments.LockTimeoutFlag],
        ["--allow-preview-host", "--allow-writes"]);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is not { } a || a.LockTimeout() is not { } timeout || !AtFlag.TryParse(a.Value("--at"), out var at))
        {
            return CommandOutput.Usage(Shape);
        }

        var request = new RunScriptRequest(
            Path.GetFullPath(a[0]), Path.GetFullPath(a[1]), timeout, a.PathValue("--baseline"), a.Value("--app-build"), a.Value("--fixtures"),
            a.PathValue("--cache"), a.Value("--host"), a.Has("--allow-preview-host"), a.Has("--allow-writes"), at);
        var service = services.GetRequiredService<IService<RunScriptRequest, RunScriptResponse>>();
        var result = await service.Handle(request, cancellationToken).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Error(result.Error)
            : CommandOutput.Json(result.Value.Summary, json, result.Value.Passed ? CommandOutput.Ok : CommandOutput.CheckFailed);
    }
}
