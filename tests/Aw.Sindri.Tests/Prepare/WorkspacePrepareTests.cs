using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Features.Prepare;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Checks;
using Aw.Sindri.Testing.Prepare;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Prepare;

/// <summary>The host-side prepare (sandbox spec §3.8, SB-11): the cache key comes from the base commit, the install goes through heavy, a failure is SND-LAUNCH-021 with its reason, a hit copies, and only the allowed packages run install scripts.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkspacePrepareTests
{
    private const string Repo = "/work/web";
    private const string Worktree = "/work/web/.claude/worktrees/sindri-run";
    private const string Lockfile = "scripts/package-lock.json";

    private readonly FakePrepareSource _source = new FakePrepareSource().With("base1", Lockfile, "LOCK-A").With("base2", Lockfile, "LOCK-B");
    private readonly InMemoryPrepareCache _cache = new();
    private readonly InMemoryCheckCommands _commands = new();
    private readonly FakeHeavySlots _slots = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    private static readonly CheckRun _ok = CheckRun.Of(0, timedOut: false, TimeSpan.FromSeconds(1), "ok");

    public WorkspacePrepareTests()
    {
        _ = _commands.Answer(WorkspacePrepare.InstallName, _ok).Answer(WorkspacePrepare.RebuildName, _ok);
    }

    private WorkspacePrepare Make(params PrepareDir[] dirs) => new(new Approved(dirs), _source, _cache, _commands, _slots, _clock);

    private static LaunchRequest Step(string? baseCommit) =>
        A.LaunchRequest().InDirectory(Repo).InWorktree(Worktree).Unattended(new StepArgs(Model: null, Policy: null, Ref: new StepRef("build", "agent"))).Build() with { BaseCommit = baseCommit };

    private static PrepareDir Scripts(params string[] allow) => new("scripts", "npm ci", allow.Length == 0 ? ["esbuild"] : allow);

    [Fact]
    public async Task Prepare_cache_key_reads_the_base_commit()
    {
        _ = _cache.Holding(PrepareKey.Of("LOCK-A", ["esbuild"]));

        var prepared = await Make(Scripts()).Prepare(Step("base1"), Worktree, progress: null, TestContext.Current.CancellationToken);

        Expect.True(prepared.IsSuccess);
        Expect.Equal(("/work/web", "base1", Lockfile), Expect.Only(_source.Reads));   // the commit's lockfile, never the worktree's
        Expect.Equal(PrepareKey.Of("LOCK-A", ["esbuild"]), Expect.Only(_cache.Restored).Key);
        Expect.True(PrepareKey.Of("LOCK-A", ["esbuild"]) != PrepareKey.Of("LOCK-B", ["esbuild"]));
        Expect.True(PrepareKey.Of("LOCK-A", ["esbuild"]) != PrepareKey.Of("LOCK-A", ["esbuild", "sharp"]));
    }

    [Fact]
    public async Task Prepare_runs_through_heavy()
    {
        var heldDuringInstall = new List<int>();
        _ = _commands.Answer(WorkspacePrepare.InstallName, _ =>
        {
            heldDuringInstall.Add(_slots.Held);
            return _ok;
        });

        var prepared = await Make(Scripts()).Prepare(Step("base1"), Worktree, progress: null, TestContext.Current.CancellationToken);

        Expect.True(prepared.IsSuccess);
        Expect.Equal(1, _slots.Taken);
        Expect.Equal(1, _slots.Released);   // released on every path
        Expect.Equal(1, Expect.Only(heldDuringInstall));   // the install ran while the slot was held
        Expect.Equal(PrepareKey.Of("LOCK-A", ["esbuild"]), Expect.Only(_cache.Stored).Key);   // a miss fills the cache
    }

    [Fact]
    public async Task Prepare_failure_is_021_with_its_reason()
    {
        var passed = await Make(Scripts()).Prepare(Step("base1"), Worktree, progress: null, TestContext.Current.CancellationToken);
        _cache.Clear();
        _ = _commands.Answer(WorkspacePrepare.InstallName, CheckRun.Of(1, timedOut: false, TimeSpan.FromSeconds(1), "npm ERR! missing: left-pad@1.0.0"));
        var install = await Make(Scripts()).Prepare(Step("base1"), Worktree, progress: null, TestContext.Current.CancellationToken);
        _cache.Clear();
        _ = _commands.Answer(WorkspacePrepare.InstallName, CheckRun.Of(-1, timedOut: true, TimeSpan.FromMinutes(10), "still going"));
        var timeout = await Make(Scripts()).Prepare(Step("base1"), Worktree, progress: null, TestContext.Current.CancellationToken);
        var missing = await Make(Scripts()).Prepare(Step("base3"), Worktree, progress: null, TestContext.Current.CancellationToken);   // base3 has no lockfile

        Expect.True(passed.IsSuccess);   // the control: a passing install is not a failure
        Expect.Equal(SindriErrors.LaunchPrepareFailed.Code, install.Error.Code);
        Expect.Equal(SandboxReasons.InstallFailed, SandboxRefusal.ReasonOf(install.Error));
        Expect.True(install.Error.Message.Contains("npm ERR! missing: left-pad@1.0.0", StringComparison.Ordinal) && install.Error.Message.Contains('1', StringComparison.Ordinal));   // the exit and the tail of the output
        Expect.Equal(SandboxReasons.Timeout, SandboxRefusal.ReasonOf(timeout.Error));
        Expect.Equal(SindriErrors.LaunchPrepareFailed.Code, missing.Error.Code);
        Expect.Equal(SandboxReasons.LockfileMissing, SandboxRefusal.ReasonOf(missing.Error));
        Expect.Equal(0, _slots.Held);
    }

    [Fact]
    public async Task Prepare_hit_copies_without_running_install()
    {
        _ = _cache.Holding(PrepareKey.Of("LOCK-A", ["esbuild"]));

        var prepared = await Make(Scripts()).Prepare(Step("base1"), Worktree, progress: null, TestContext.Current.CancellationToken);

        Expect.True(prepared.IsSuccess);
        Expect.Equal(Path.Combine(Worktree, "scripts"), Expect.Only(_cache.Restored).Directory);
        Expect.Equal(0, _commands.Calls.Count);
        Expect.Equal(0, _slots.Taken);   // a hit never waits for a slot
        Expect.Equal(0, _cache.Stored.Count);
    }

    [Fact]
    public async Task Prepare_does_not_run_scripts_outside_allowScripts()
    {
        var prepared = await Make(Scripts("esbuild", "sharp")).Prepare(Step("base1"), Worktree, progress: null, TestContext.Current.CancellationToken);

        Expect.True(prepared.IsSuccess);
        var calls = _commands.Calls;
        Expect.Equal(2, calls.Count);
        Expect.Sequence(["npm", "ci", "--ignore-scripts"], calls[0].Command.Argv);   // nothing runs a script at install
        Expect.Sequence(["npm", "rebuild", "esbuild", "sharp"], calls[1].Command.Argv);   // only the listed packages do
        Expect.Equal(Path.Combine(Worktree, "scripts"), calls[0].WorkingDirectory);
    }

    [Fact]
    public async Task Prepare_without_a_base_commit_or_a_declaration_does_nothing()
    {
        var noBase = await Make(Scripts()).Prepare(Step(baseCommit: null), Worktree, progress: null, TestContext.Current.CancellationToken);
        var undeclared = await Make().Prepare(Step("base1"), Worktree, progress: null, TestContext.Current.CancellationToken);

        Expect.True(noBase.IsSuccess && undeclared.IsSuccess);
        Expect.Equal(0, _source.Reads.Count);   // never a fallback to the worktree's lockfile
        Expect.Equal(0, _commands.Calls.Count);
    }

    private sealed class Approved(PrepareDir[] dirs) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
        {
            var declared = dirs.Length == 0 ? new SandboxRepoSettings() : new SandboxRepoSettings(Prepare: new SandboxPrepare(dirs));
            var profile = new SindriProfile(1, "thor", Repos: [new RepoEntry("web", Repo)], Sandbox: new SandboxSettings(Repos: new Dictionary<string, SandboxRepoSettings>(StringComparer.Ordinal) { ["web"] = declared }));
            return ValueTask.FromResult(Results.Success<LoadedProfile?>(new LoadedProfile(profile, ProfileHashing.Of("{}"u8), "/home/profile.yaml")));
        }
    }
}
