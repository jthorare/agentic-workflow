using System.Text.Json;
using Aw.Evidence.Core.Settings;
using Aw.Evidence.Features.Doctor;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>The 'doctor' command: prints the stack's health (the worktree's slot when UI_EVIDENCE_WORKTREE names one or the current checkout has a slot file); exit 2 when unhealthy (each failure also on stderr).</summary>
internal static class DoctorCommand
{
    internal const string Name = "doctor";
    internal const string Shape = "doctor";

    private static readonly CliSpec _spec = new(0, [], []);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is null)
        {
            return CommandOutput.Usage(Shape);
        }

        var service = services.GetRequiredService<IService<DoctorRequest, DoctorResponse>>();
        var result = await service.Handle(new DoctorRequest(WorktreeArgument.Resolve(services.GetRequiredService<IEvidenceSettings>())), cancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Error(result.Error);
        }

        foreach (var failure in result.Value.Failures)
        {
            CommandOutput.Message($"doctor: {failure}");
        }

        return CommandOutput.Json(result.Value, json, result.Value.Healthy ? CommandOutput.Ok : CommandOutput.CheckFailed);
    }
}
