using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Features.Prepare;

/// <summary>
/// The host-side prepare of a Step's worktree (sandbox spec §3.8, SB-11): for each package directory the repo declares, the lockfile is read at the run's base commit, a cached install is
/// copied in on a hit, and a miss installs (scripts off, then only the allowed packages' scripts) and fills the cache. A repo with no declaration, or a launch with no base commit, is left alone.
/// </summary>
/// <param name="profile">The approved profile, for the repo's <c lang="text">sandbox.repos.&lt;repo&gt;.prepare</c>.</param>
/// <param name="source">Reads the lockfiles at the base commit.</param>
/// <param name="cache">The installs kept per lockfile hash.</param>
/// <param name="commands">Runs the install.</param>
/// <param name="slots">The box-wide heavy-job lock.</param>
/// <param name="clock">Times the prepare's limit.</param>
internal sealed class WorkspacePrepare(IApprovedProfile profile, IPrepareSource source, IPrepareCache cache, ICheckCommands commands, IHeavySlots slots, TimeProvider clock) : IWorkspacePrepare
{
    /// <summary>The name of the install command, as <see cref="ICheckCommands"/> is told it.</summary>
    internal const string InstallName = "prepare-install";

    /// <summary>The name of the rebuild command that runs the allowed install scripts.</summary>
    internal const string RebuildName = "prepare-rebuild";

    /// <summary>The prepare's own limit (spec §3.8): the slot wait and every install share it.</summary>
    internal static readonly TimeSpan DefaultLimit = TimeSpan.FromMinutes(10);

    private const string Lockfile = "package-lock.json";

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> Prepare(LaunchRequest request, string workDirectory, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var loaded = approved.Value?.Profile;
        var dirs = RepoDeclaration.For(loaded?.Sandbox, RepoNameOf(loaded, request.RepoPath)).Prepare?.Dirs;
        if (dirs is not { Count: > 0 })
        {
            return Unit.Value;   // no declaration, no prepare, no -021
        }

        if (request.BaseCommit is not { Length: > 0 } baseCommit)
        {
            return Unit.Value;   // never the worktree's lockfile: an agent may have edited it (spec §3.8). The launcher logs why it had no base.
        }

        var run = new Run(request.RepoPath, baseCommit, workDirectory, clock.GetUtcNow() + DefaultLimit, progress);
        foreach (var dir in dirs)
        {
            var done = await One(run, dir, cancellationToken).ConfigureAwait(false);
            if (done.IsError)
            {
                return done;
            }
        }

        return Unit.Value;
    }

    private async ValueTask<Result<Unit>> One(Run run, PrepareDir dir, CancellationToken cancellationToken)
    {
        var relative = (dir.Dir ?? ".").Trim('/');
        var lockPath = relative.Length == 0 || string.Equals(relative, ".", StringComparison.Ordinal) ? Lockfile : relative + "/" + Lockfile;
        var read = await source.FileAtCommit(run.Repo, run.Base, lockPath, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return Fail(SandboxReasons.InstallFailed, $"can't read {lockPath} at {run.Base}: {read.Error.Message}");
        }

        if (read.Value is not { } lockfile)
        {
            return Fail(SandboxReasons.LockfileMissing, $"{lockPath} does not exist at the base commit {run.Base}");
        }

        var allow = dir.AllowScripts ?? [];
        var key = PrepareKey.Of(lockfile, allow);
        var target = Path.Combine(run.WorkDirectory, relative);
        if (!cache.Has(key))
        {
            return await Miss(run, (key, target), allow, cancellationToken).ConfigureAwait(false);
        }

        var restored = await cache.Restore(key, target, cancellationToken).ConfigureAwait(false);
        return restored.IsError ? Fail(SandboxReasons.InstallFailed, $"can't copy the cached packages into {target}: {restored.Error.Message}") : Unit.Value;
    }

    // A miss installs under one heavy slot and fills the cache.
    private async ValueTask<Result<Unit>> Miss(Run run, (string Key, string Target) at, IReadOnlyList<string> allow, CancellationToken cancellationToken)
    {
        run.Progress?.Report("Preparing the workspace");
        // Deviation from spec 3.8 (lead's decision, 2026-10-28): the spec says `heavy --expensive npm ci`; Sindri already holds the box-wide slot itself through IHeavySlots and runs the tool with
        // AW_HEAVY_LOCK_HELD=1 (ToolCheckCommands), as RepoCommandRunner does, so there is no dependency on an external wrapper and the install still queues like any expensive job.
        var wait = Remaining(run);
        var taken = await slots.Take("sindri workspace-prepare", wait, run.Progress, cancellationToken).ConfigureAwait(false);
        if (taken.IsError)
        {
            return Fail(SandboxReasons.InstallFailed, $"the heavy-job lock can't be used: {taken.Error.Message}");
        }

        if (taken.Value.Lease is not { } lease)
        {
            return Fail(SandboxReasons.Timeout, $"every heavy slot stayed busy for {Minutes(wait)} min: {taken.Value.Busy}");
        }

        await using (lease.ConfigureAwait(false))
        {
            var installed = await Install(run, at.Target, allow, cancellationToken).ConfigureAwait(false);
            if (installed.IsError)
            {
                return installed;
            }

            var stored = await cache.Store(at.Key, at.Target, cancellationToken).ConfigureAwait(false);
            return stored.IsError ? Fail(SandboxReasons.InstallFailed, $"can't fill the cache from {at.Target}: {stored.Error.Message}") : Unit.Value;
        }
    }

    // `npm ci --ignore-scripts`, then `npm rebuild <allowed>`: npm has no per-package allow list, so scripts are off for the install and only the named packages' scripts run afterwards.
    private async ValueTask<Result<Unit>> Install(Run run, string target, IReadOnlyList<string> allow, CancellationToken cancellationToken)
    {
        List<CheckInvocation> steps = [new CheckInvocation(InstallName, ["npm", "ci", "--ignore-scripts"], Remaining(run))];
        if (allow.Count > 0)
        {
            steps.Add(new CheckInvocation(RebuildName, ["npm", "rebuild", .. allow], Remaining(run)));
        }

        foreach (var step in steps)
        {
            var left = run.Deadline - clock.GetUtcNow();
            if (left <= TimeSpan.Zero)
            {
                return Fail(SandboxReasons.Timeout, $"the prepare's limit of {Minutes(DefaultLimit)} min ran out before '{string.Join(' ', step.Argv)}' could start");
            }

            var ran = await commands.Run(step with { Timeout = left }, target, cancellationToken).ConfigureAwait(false);
            if (ran.IsError)
            {
                return Fail(SandboxReasons.InstallFailed, $"'{string.Join(' ', step.Argv)}' can't be started: {ran.Error.Message}");
            }

            if (ran.Value.TimedOut)
            {
                return Fail(SandboxReasons.Timeout, $"'{string.Join(' ', step.Argv)}' ran past the prepare's limit of {Minutes(DefaultLimit)} min{Tail(ran.Value)}");
            }

            if (!ran.Value.Passed)
            {
                return Fail(SandboxReasons.InstallFailed, $"'{string.Join(' ', step.Argv)}' exited {ran.Value.ExitCode.ToString(CultureInfo.InvariantCulture)}{Tail(ran.Value)}");
            }
        }

        return Unit.Value;
    }

    private TimeSpan Remaining(Run run)
    {
        var left = run.Deadline - clock.GetUtcNow();
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    private static string Tail(CheckRun run) => run.Tail.Length == 0 ? string.Empty : "; its output ended: " + run.Tail;

    private static string Minutes(TimeSpan time) => time.TotalMinutes.ToString("0", CultureInfo.InvariantCulture);

    private static Error Fail(string reason, string detail) => SandboxRefusal.For(SindriErrors.LaunchPrepareFailed, reason, detail);

    private static RepoName RepoNameOf(SindriProfile? loaded, string repoPath) =>
        RepoName.From(loaded?.Repos?.FirstOrDefault(r => string.Equals(r.Path.TrimEnd('/'), repoPath.TrimEnd('/'), StringComparison.Ordinal))?.Name ?? Path.GetFileName(repoPath.TrimEnd('/')));

    private sealed record Run(string Repo, string Base, string WorkDirectory, DateTimeOffset Deadline, IProgress<string>? Progress);
}
