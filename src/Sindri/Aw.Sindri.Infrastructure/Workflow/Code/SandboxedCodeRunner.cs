using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>
/// The 'code' block's port (agent-first PR 9c): compiles the body through the cache, then runs 'sindri-code-runner' in the process sandbox: no network, its private /tmp as the only writable place,
/// the profile's repo (when the node names one) read-only. The compiled assembly is copied into that /tmp, so the sandbox needs no other path to it.
/// </summary>
/// <param name="cache">Compiles a body that is not cached.</param>
/// <param name="approved">The live profile: where a node's repo is.</param>
/// <param name="sandbox">Plans and wraps the process.</param>
/// <param name="host">The host facts the plan builder reads.</param>
/// <param name="runAreas">The folder a run's own area is made in.</param>
/// <param name="runnerPath">The runner binary's absolute path.</param>
internal sealed class SandboxedCodeRunner(CompiledCodeCache cache, IApprovedProfile approved, ISandbox sandbox, Func<SandboxHostFacts> host, string runAreas, string runnerPath) : ICodeRunPort
{
    /// <inheritdoc />
    public async ValueTask<Result<string>> Run(CodeRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var assembly = Compiled(run, cancellationToken);
        if (assembly.IsError)
        {
            return assembly.Error;
        }

        var repo = await RepoPath(run.Repo, cancellationToken).ConfigureAwait(false);
        if (repo.IsError)
        {
            return repo.Error;
        }

        var launch = LaunchId.From("LCH-" + Guid.NewGuid().ToString("N"));
        var area = Path.Combine(runAreas, launch.Value);
        try
        {
            _ = Directory.CreateDirectory(Path.Combine(area, "tmp"));
            File.Copy(assembly.Value, Path.Combine(area, "tmp", "code.dll"), overwrite: true);
            var plan = sandbox.PlanProcess(CodeRunSandbox.Request(launch, runnerPath, area, repo.Value, host()));
            if (plan.IsError)
            {
                return SindriErrors.CodeRunnerUnavailable.With(plan.Error.Message);
            }

            var process = new CodeRunnerProcess([runnerPath], argv => sandbox.WrapProcess(plan.Value, argv));
            return await process.Run(DllPath(plan.Value, area), run.InputJson, new CodeLimits(run.Timeout, run.MemoryMb), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)   // boundary: the run area could not be made
        {
            return SindriErrors.CodeRunnerUnavailable.With(e.Message);
        }
        finally
        {
            if (Directory.Exists(area))
            {
                Directory.Delete(area, recursive: true);
            }
        }
    }

    // why: bubblewrap mounts <area>/tmp at /tmp, but Seatbelt has no mount namespace and WrapProcess rewrites env only, so there the runner must get the dll's real path
    private static string DllPath(SandboxPlan plan, string area) =>
        plan.Mechanism == SandboxMechanism.Seatbelt ? Path.Combine(area, "tmp", "code.dll") : "/tmp/code.dll";

    // why: the runner must exist before anything is planned, or bubblewrap exits 1 and the user sees SND-CODE-004 instead of SND-CODE-005
    private Result<string> Compiled(CodeRun run, CancellationToken cancellationToken)
    {
        var code = cache.Resolve(run.Code, cancellationToken);
        if (code.AssemblyPath is not { } assembly)
        {
            return SindriErrors.CodeCompileFailed.With(string.Join("; ", code.Diagnostics.Select(static d => string.Create(CultureInfo.InvariantCulture, $"line {d.Line}, column {d.Column}: {d.Id} {d.Message}"))));
        }

        return File.Exists(runnerPath)
            ? assembly
            : SindriErrors.CodeRunnerUnavailable.With($"the runner binary is not at {runnerPath} (fix: install it beside the Sindri host)");
    }

    // why: a node with no repo reads no files; a named repo must be in the live profile
    private async ValueTask<Result<string?>> RepoPath(string? name, CancellationToken cancellationToken)
    {
        if (name is null)
        {
            return (string?)null;
        }

        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var path = latest.Value?.Profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.Ordinal))?.Path;
        return path is null ? SindriErrors.LaunchRepoMissing.With($"the node's repo '{name}' is not in the profile's repos (fix: add it)") : path;
    }
}
