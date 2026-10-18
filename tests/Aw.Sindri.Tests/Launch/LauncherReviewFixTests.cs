using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>What the review of the launcher PR (#295, run 1) found in the launcher's own files, gates and cancellation.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LauncherReviewFixTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-launcher-review-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeSessionHost _host = new();

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private LauncherOptions Options => new(_home);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Launcher Make(ITerminalViewer viewer) =>
        new(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), _ => _host, viewer, Options, _clock) { ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null) });

    [Fact]
    public async Task A_caller_that_cancels_after_the_session_started_leaves_no_session_and_no_secret()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var viewer = new CancellingViewer(cancel);
        var request = A.LaunchRequest().Build();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Make(viewer).Launch(request, cancel.Token));

        Expect.Equal(0, _host.LiveCount);   // the tmux session is ended
        Assert.False(File.Exists(LaunchSecrets.SecretPathFor(Options.SecretsDirectory, request.Launch)));
        Assert.False(File.Exists(LaunchSecrets.PathFor(Options.SecretsDirectory, request.Launch)));
    }

    [Fact]
    public async Task A_launch_id_whose_hash_file_survived_a_crash_starts_a_session_instead_of_answering_with_a_made_up_one()
    {
        var request = A.LaunchRequest().Build();
        _ = Directory.CreateDirectory(Options.SecretsDirectory);
        await File.WriteAllTextAsync(LaunchSecrets.PathFor(Options.SecretsDirectory, request.Launch), "sha256:stale\n", Token);   // Sindri died after the files, before the host started

        var result = await Make(new FakeTerminalViewer()).Launch(request, Token);

        _ = Assert.IsType<StartedLaunch>(result);
        Expect.Equal(1, _host.Started.Count);
    }

    [Fact]
    public async Task The_sweep_removes_a_dead_launchs_hash_and_policy_files_and_keeps_a_live_ones()
    {
        var dead = LaunchId.From("LCH-" + new string('0', 31) + "1");
        var live = LaunchId.From("LCH-" + new string('0', 31) + "2");
        foreach (var launch in new[] { dead, live })
        {
            LaunchSecrets.Write(Options.SecretsDirectory, launch, "p");
            _ = Directory.CreateDirectory(Options.PolicyDirectory);
            await File.WriteAllTextAsync(StepPolicyFile.PathFor(Options.PolicyDirectory, launch), "{}", Token);
        }

        var old = _clock.GetUtcNow().AddMinutes(-5);
        foreach (var file in Directory.EnumerateFiles(Options.SecretsDirectory).Concat(Directory.EnumerateFiles(Options.PolicyDirectory)))
        {
            File.SetLastWriteTimeUtc(file, old.UtcDateTime);
        }

        var store = new LaunchFileStore(Options);
        var removed = store.SweepStale(_clock.GetUtcNow(), TimeSpan.FromSeconds(60), id => id == live);

        Expect.Equal(3, removed);   // the dead launch's hash, secret and policy file
        Assert.False(File.Exists(LaunchSecrets.PathFor(Options.SecretsDirectory, dead)));
        Assert.False(File.Exists(StepPolicyFile.PathFor(Options.PolicyDirectory, dead)));
        Assert.True(File.Exists(LaunchSecrets.PathFor(Options.SecretsDirectory, live)));
        Assert.Contains(live, store.Launches());   // a launch with only a hash file is still known, so the service can say it is live
    }

    [Fact]
    public async Task Two_turns_for_one_launch_never_overlap_and_the_gate_is_gone_when_idle()
    {
        var gates = new LaunchGates();
        var launch = LaunchId.From("LCH-" + new string('0', 31) + "1");
        var first = await gates.Turn(launch, Token);
        var second = gates.Turn(launch, Token).AsTask();

        await Task.Delay(50, Token);
        Assert.False(second.IsCompleted);   // waits for the first
        first.Dispose();
        using var turn = await second;

        Expect.Equal(1, gates.Open);
        turn.Dispose();
        Expect.Equal(0, gates.Open);
    }

    [Fact]
    public async Task A_waiter_that_gives_up_leaves_no_gate_behind()
    {
        var gates = new LaunchGates();
        var launch = LaunchId.From("LCH-" + new string('0', 31) + "1");
        using var held = await gates.Turn(launch, Token);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var waiting = gates.Turn(launch, cancel.Token).AsTask();

        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Expect.Equal(1, gates.Open);   // still the holder's
    }

    [Theory]
    [InlineData("a.b\"c", "\"a.b\\\"c\"")]
    [InlineData("back\\slash", "\"back\\\\slash\"")]
    [InlineData("line\nbreak", "\"line\\u000Abreak\"")]
    [InlineData("plain-repo", "\"plain-repo\"")]
    public void A_toml_string_cannot_be_ended_early_by_its_value(string value, string expected) =>
        Expect.Equal(expected, McpLaunchConfig.Toml(value));

    [Fact]
    public async Task Two_writes_of_one_path_in_this_process_do_not_share_a_temp_file_and_leave_none_behind()
    {
        var path = Path.Combine(_home, "target.txt");
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => OwnerOnly.WriteAtomically(path, [(byte)i]), Token)));

        Expect.Equal(1, Directory.EnumerateFiles(_home, "target.txt*").Count());   // no .tmp-… file survives

        var blocked = Path.Combine(_home, "dir-in-the-way");
        _ = Directory.CreateDirectory(blocked);   // the move onto a directory fails
        _ = Assert.ThrowsAny<Exception>(() => OwnerOnly.WriteAtomically(blocked, [1]));
        Expect.Equal(0, Directory.EnumerateFiles(_home, "dir-in-the-way.tmp-*").Count());
    }

    [Theory]
    [InlineData("/work/repo", true)]
    [InlineData("\\\\server\\share", true)]
    [InlineData("C:\\work", true)]
    [InlineData("C:/work", true)]
    [InlineData("\\foo", false)]
    [InlineData("relative/path", false)]
    [InlineData("", false)]
    public void A_path_is_absolute_when_rooted_unc_or_drive_lettered(string path, bool absolute) =>
        Expect.Equal(absolute, Placeholders.IsAbsolute(path));

    [Theory]
    [InlineData("[0-9a-f]{8}", "0a1b2c3d", true)]
    [InlineData("[0-9a-f]{8}", "0a1b2c3dzz", false)]   // an unanchored pattern still has to match the whole id
    [InlineData("[0-9a-f]{8}", "zz0a1b2c3d", false)]
    [InlineData("(", "anything", false)]   // does not compile: matches nothing
    public void A_session_id_pattern_matches_the_whole_id(string pattern, string id, bool accepted)
    {
        Expect.Equal(accepted, SessionIdRules.Matches(pattern, id));
        Expect.Equal(accepted, SessionIdRules.Matches(pattern, id));   // the second call takes the cached regex
    }

    private sealed class CancellingViewer(CancellationTokenSource cancel) : ITerminalViewer
    {
        public string Kind => "test";

        public async ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken)
        {
            await cancel.CancelAsync();   // the caller gives up while the window is opening
            cancellationToken.ThrowIfCancellationRequested();
            return ViewerOutcome.Opened;
        }

        public ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new TerminalFacts(Kind, Kind, Present: true, "file", ConfigPath: null, Problem: null, Detail: null));
    }
}
