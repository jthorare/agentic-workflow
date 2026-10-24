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
    private const string WorktreeVariable = "UI_EVIDENCE_WORKTREE";

    private static readonly CliSpec _spec = new(0, [], []);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is null)
        {
            return CommandOutput.Usage(Shape);
        }

        var service = services.GetRequiredService<IService<DoctorRequest, DoctorResponse>>();
        var result = await service.Handle(new DoctorRequest(Worktree(services.GetRequiredService<IEvidenceSettings>())), cancellationToken).ConfigureAwait(false);
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

    // UI_EVIDENCE_WORKTREE names a worktree outright (doctor refuses it without a slot there). Otherwise the current checkout's slot is checked
    // when it has a slot file, and the main stack when it has none (or no slots are configured).
    private static string? Worktree(IEvidenceSettings settings)
    {
        if (Environment.GetEnvironmentVariable(WorktreeVariable) is { } named && !string.IsNullOrWhiteSpace(named))
        {
            return named;
        }

        if (settings.Slots is not { } slots)
        {
            return null;
        }

        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return File.Exists(Path.Combine(directory.FullName, slots.FileName)) ? directory.FullName : null;
            }
        }

        return null;
    }
}
