using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Setup;

/// <summary><see cref="IPreCommitGit"/> over git. Not hardened: the repo is one the owner approved or is committing in, and core.hooksPath must be read, not blanked.</summary>
internal sealed class GitPreCommit : IPreCommitGit
{
    public string StableBin { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "sindri");

    public string RunningBin => Environment.ProcessPath ?? "sindri";

    public async ValueTask<string?> ReadHook(string path, CancellationToken cancellationToken) =>
        File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : null;

    public async ValueTask WriteHook(string path, string text, CancellationToken cancellationToken)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: git-path always names a file inside a hooks directory
        await File.WriteAllTextAsync(path, text, cancellationToken).ConfigureAwait(false);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    public async ValueTask<Result<HookLocation>> HookPath(string? repo, CancellationToken cancellationToken)
    {
        var folder = repo ?? ".";
        var hook = await GitProcess.Run(folder, ["rev-parse", "--path-format=absolute", "--git-path", "hooks/pre-commit"], cancellationToken).ConfigureAwait(false);
        var common = hook.Exit == 0 ? await GitProcess.Run(folder, ["rev-parse", "--path-format=absolute", "--git-common-dir"], cancellationToken).ConfigureAwait(false) : hook;
        if (hook.Exit != 0 || common.Exit != 0)
        {
            return SindriErrors.ScrubNotARepo.With($"{Path.GetFullPath(folder)} is not a git repository");
        }

        var root = common.Output.TrimEnd('/') + "/";
        return new HookLocation(hook.Output, hook.Output.StartsWith(root, StringComparison.Ordinal));
    }

    public async ValueTask<Result<IReadOnlyList<AddedLine>>> StagedAddedLines(string? repo, CancellationToken cancellationToken)
    {
        var folder = repo ?? ".";
        var inside = await GitProcess.Run(folder, ["rev-parse", "--git-dir"], cancellationToken).ConfigureAwait(false);
        if (inside.Exit != 0)
        {
            return SindriErrors.ScrubNotARepo.With($"{Path.GetFullPath(folder)} is not a git repository");
        }

        var diff = await GitProcess.Run(folder, ["-c", "core.quotepath=false", "diff", "--cached", "--no-color", "--unified=0", "--no-renames", "--no-ext-diff"], cancellationToken).ConfigureAwait(false);
        if (diff.Exit != 0)
        {
            return SindriErrors.ScrubGitFailed.With($"git diff --cached failed in {Path.GetFullPath(folder)}: {diff.Error.Trim()}");
        }

        IReadOnlyList<AddedLine> lines = [.. DiffAddedLines.Parse(diff.Output).Select(static l => new AddedLine(l.Path, l.Line, l.Text))];
        return Results.Success(lines);
    }
}
