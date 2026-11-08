using System.Text.Json;
using Aw.Evidence.Core.Settings;
using Aw.Evidence.Features.CheckProvenance;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>The 'provenance' command: prints whether the local database holds seed data only (the worktree slot's database when doctor would check that slot).</summary>
internal static class CheckProvenanceCommand
{
    internal const string Name = "provenance";
    internal const string Shape = "provenance";

    private static readonly CliSpec _spec = new(0, [], []);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is null)
        {
            return CommandOutput.Usage(Shape);
        }

        var service = services.GetRequiredService<IService<CheckProvenanceRequest, CheckProvenanceResponse>>();
        var result = await service.Handle(new CheckProvenanceRequest(WorktreeArgument.Resolve(services.GetRequiredService<IEvidenceSettings>())), cancellationToken).ConfigureAwait(false);
        return result.IsError ? CommandOutput.Error(result.Error) : CommandOutput.Json(result.Value, json);
    }
}
