using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Features.GetSandboxStatus;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetSandboxStatus;

/// <summary>The sandbox status read (sandbox spec §8): the newest check and the configuration, with no probe run.</summary>
[Lane(TestLane.Unit)]
public sealed class GetSandboxStatusServiceTests
{
    private const long Gib = 1024L * 1024 * 1024;
    private static readonly DateTimeOffset _noon = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SandboxBlocker NoBwrap() => SandboxBlocker.Of(SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoBwrap));

    private static GetSandboxStatusService Service(LastSandboxCheck last, SandboxSettings? settings = null, params (string Repo, LearnHistory History)[] learn) =>
        new(last, new Approved(settings, [.. learn.Select(static l => l.Repo)]), new Machine(62 * Gib), new Learn(learn.ToDictionary(static l => l.Repo, static l => l.History, StringComparer.Ordinal)), new Platform(), new FakeTimeProvider(_noon));

    private static async Task<SandboxStatusResponse> Read(GetSandboxStatusService service) =>
        (await service.Handle(new GetSandboxStatusRequest(), Token)).Value;

    [Fact]
    public async Task Before_any_check_it_says_so_and_still_reports_the_configuration()
    {
        var status = await Read(Service(new LastSandboxCheck()));

        Assert.Null(status.CheckedAt);
        Assert.Null(status.Ready);
        Assert.Empty(status.Blockers);
        Assert.Equal(("12GiB", "400%", 2048), (status.Limits.MemoryMax, status.Limits.CpuQuota, status.Limits.TasksMax));
        Assert.Equal(ModelLoginSource.OwnerCopy, status.Login);
        Assert.Empty(status.Learn);
    }

    [Fact]
    public async Task After_a_blocked_check_it_carries_the_time_and_the_blockers_in_the_owners_words()
    {
        var last = new LastSandboxCheck();
        last.Record(new SandboxCheckRecord(_noon.AddMinutes(-5), new SandboxCheckReport([NoBwrap()], [])));

        var status = await Read(Service(last));

        Assert.Equal((_noon.AddMinutes(-5), false), (status.CheckedAt, status.Ready));
        var blocker = Assert.Single(status.Blockers);
        Assert.Equal(("SND-LAUNCH-014", "no-bwrap"), (blocker.Code, blocker.Reason));
        Assert.Equal("none", status.Mechanism);
    }

    [Fact]
    public async Task After_a_passing_check_the_mechanism_is_the_boxs()
    {
        var last = new LastSandboxCheck();
        last.Record(new SandboxCheckRecord(_noon, new SandboxCheckReport([], [])));

        var status = await Read(Service(last));

        Assert.Equal((true, "bubblewrap"), (status.Ready, status.Mechanism));
    }

    [Fact]
    public async Task The_profile_sets_the_limits_the_login_and_the_groups()
    {
        var settings = new SandboxSettings(
            Limits: new SandboxLimits(Memory: "8GiB", Cpu: "25%", Tasks: 512),
            Network: new SandboxNetwork(Groups: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["mirrors"] = ["mirror.example.com"] }),
            Login: new SandboxLogin(ModelLoginSource.SeparateLogin));

        var status = await Read(Service(new LastSandboxCheck(), settings));

        Assert.Equal(("8GiB", "200%", 512), (status.Limits.MemoryMax, status.Limits.CpuQuota, status.Limits.TasksMax));
        Assert.Equal(ModelLoginSource.SeparateLogin, status.Login);
        Assert.Contains("mirrors", status.Groups, StringComparer.Ordinal);
        Assert.Contains("model", status.Groups, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_repo_in_learn_mode_ends_seven_days_after_its_first_learn_run()
    {
        var settings = new SandboxSettings(Network: new SandboxNetwork(Mode: NetworkMode.Learn));
        var first = _noon.AddDays(-2);

        var status = await Read(Service(new LastSandboxCheck(), settings, ("agentic-workflow", new LearnHistory(first, LearnRuns: 3, EndedAt: null)), ("quiet", new LearnHistory(FirstLearnRun: null, LearnRuns: 0, EndedAt: null))));

        var line = Assert.Single(status.Learn);
        Assert.Equal(("agentic-workflow", first.AddDays(7)), (line.Repo, line.EndsAt));
    }

    [Fact]
    public async Task A_repo_whose_learn_mode_was_ended_is_not_listed()
    {
        var settings = new SandboxSettings(Network: new SandboxNetwork(Mode: NetworkMode.Learn));

        var status = await Read(Service(new LastSandboxCheck(), settings, ("agentic-workflow", new LearnHistory(_noon.AddDays(-1), 2, EndedAt: _noon.AddHours(-1)))));

        Assert.Empty(status.Learn);
    }

    private sealed class Platform : ISandboxPlatformName
    {
        public string Mechanism => "bubblewrap";
    }

    private sealed class Machine(long ram) : ISandboxMachine
    {
        public long RamBytes => ram;

        public int Cores => 8;
    }

    private sealed class Learn(IReadOnlyDictionary<string, LearnHistory> histories) : ILearnHistory
    {
        public ValueTask<LearnHistory> Read(string repo, CancellationToken cancellationToken) =>
            ValueTask.FromResult(histories.TryGetValue(repo, out var h) ? h : new LearnHistory(FirstLearnRun: null, LearnRuns: 0, EndedAt: null));
    }

    private sealed class Approved(SandboxSettings? settings, IReadOnlyList<string> repos) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
        {
            var profile = new SindriProfile(1, "thor", Repos: [.. repos.Select(static r => new RepoEntry(r, "/work/" + r))], Sandbox: settings);
            return ValueTask.FromResult(Results.Success<LoadedProfile?>(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml")));
        }
    }
}
