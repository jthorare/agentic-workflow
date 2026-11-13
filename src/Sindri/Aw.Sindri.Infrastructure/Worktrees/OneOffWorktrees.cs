using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Worktrees;

/// <summary>
/// The worktree of a one-off session (agents spec §15.4). The folder comes from the toolkit's 'skills/_shared/worktree-path.sh' (one root per repo), the base is the repo's HEAD, and the checkout is made by
/// <see cref="IWorktrees"/>, which never forces. A name whose folder or branch exists gets '-2', '-3'.
/// </summary>
/// <param name="worktrees">Makes and removes the checkout.</param>
/// <param name="environment">Reads one environment variable (AW_TOOLKIT, HOME).</param>
internal sealed class OneOffWorktrees(IWorktrees worktrees, Func<string, string?> environment) : IOneOffWorktrees
{
    private const int Attempts = 20;
    private static readonly TimeSpan _scriptBudget = TimeSpan.FromSeconds(10);

    public async ValueTask<Result<OneOffWorktree>> Make(string repoPath, string name, CancellationToken cancellationToken)
    {
        var head = await GitProcess.Run(repoPath, ["rev-parse", "HEAD"], cancellationToken).ConfigureAwait(false);
        if (head.Exit != 0)
        {
            return SindriErrors.WorktreeGitFailed.With($"git rev-parse HEAD failed in {repoPath}: {head.Error.Trim()}");
        }

        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var candidate = attempt == 1 ? name : string.Create(CultureInfo.InvariantCulture, $"{name}-{attempt}");
            var path = await PathFor(repoPath, candidate, cancellationToken).ConfigureAwait(false);
            if (path.IsError)
            {
                return path.Error;
            }

            var branch = await GitProcess.Run(repoPath, ["rev-parse", "--verify", "--quiet", "refs/heads/" + candidate], cancellationToken).ConfigureAwait(false);
            if (branch.Exit == 0 || Directory.Exists(path.Value))
            {
                continue;
            }

            var made = await worktrees.Create(new WorktreeRequest(repoPath, head.Output, candidate, path.Value), cancellationToken).ConfigureAwait(false);
            return made.IsError ? made.Error : new OneOffWorktree(made.Value.Path, made.Value.Branch);
        }

        return SindriErrors.WorktreeConflict.With($"{name} and its {Attempts} numbered forms all exist in {repoPath}");
    }

    public ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken) => worktrees.Remove(path, cancellationToken);

    public async ValueTask<string> StatusSummary(string path, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(path))
        {
            return string.Empty;
        }

        var status = await GitProcess.Run(path, ["status", "--short"], cancellationToken).ConfigureAwait(false);
        return status.Exit == 0 ? string.Join('\n', status.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(10)) : string.Empty;
    }

    private async ValueTask<Result<string>> PathFor(string repoPath, string name, CancellationToken cancellationToken)
    {
        var toolkit = environment("AW_TOOLKIT") is { Length: > 0 } set ? set : Path.Combine(environment("HOME") ?? string.Empty, ".agentic-workflow", "toolkit");
        var script = Path.Combine(toolkit, "skills", "_shared", "worktree-path.sh");
        if (!File.Exists(script))
        {
            return SindriErrors.WorktreeInvalid.With($"{script} is missing (fix: ./setup.sh)");
        }

        var start = ChildEnvironment.Start("bash", [script, name]);
        start.WorkingDirectory = repoPath;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        try
        {
            using var process = Process.Start(start) ?? throw new Win32Exception("bash did not start");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_scriptBudget);
            var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            var error = process.StandardError.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            var path = (await output.ConfigureAwait(false)).Trim();
            return process.ExitCode == 0 && path.StartsWith('/')
                ? path
                : SindriErrors.WorktreeInvalid.With($"worktree-path.sh refused: {(await error.ConfigureAwait(false)).Trim()}");
        }
        catch (Win32Exception e)
        {
            return SindriErrors.WorktreeInvalid.With($"bash can't run: {e.Message}");
        }
    }
}
