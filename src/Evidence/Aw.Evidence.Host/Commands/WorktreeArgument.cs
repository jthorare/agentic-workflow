using Aw.Evidence.Core.Settings;

namespace Aw.Evidence.Host.Commands;

/// <summary>
/// The worktree whose stack slot doctor and provenance target: UI_EVIDENCE_WORKTREE names one outright (refused without a slot there).
/// Otherwise the current checkout's slot is used when it has a slot file, and the main stack when it has none (or no slots are configured).
/// </summary>
internal static class WorktreeArgument
{
    private const string WorktreeVariable = "UI_EVIDENCE_WORKTREE";

    internal static string? Resolve(IEvidenceSettings settings)
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
