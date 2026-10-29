using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Features.GetInstanceEvents;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ListInstances;

namespace Aw.Sindri.Tests.GetInstanceEvents;

/// <summary>A Step a sandbox limit ended says which limit, how long it ran and where its work is kept (sandbox spec §8), read from the ledger and never guessed.</summary>
[Lane(TestLane.Unit)]
public sealed class LimitEndingReaderTests
{
    private static readonly DateTimeOffset _noon = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly InstanceAddress _address = InstanceAddress.From(A.FirstItemId.Value + "/step/1");
    private static readonly WorkflowInstanceId _instance = WorkflowInstanceId.Of(_address);
    private static readonly RunToken _run = RunToken.For(1, 2);
    private static readonly LaunchId _launch = AgentLaunches.LaunchIdFor(_instance, _run);

    private readonly SliceHarness _slice = new();
    private LeaseGrant? _grant;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static InstanceEventFields Failed(string reason = "limit") =>
        new(From: "produce", To: "produce", Trigger: "step.failed", Run: _run.Value) { Reason = reason };

    private async Task<LimitEndingReader> Reader(bool ahead, Branches? branches = null)
    {
        _grant = await _slice.HoldLease();
        var item = new ItemRow(
            A.FirstItemId, "Add the tax field", RepoName.From("agentic-workflow"), ItemState.Parked, Step: null, Version: 1, "human:thor", _noon, _noon, _noon,
            Link: null, Alias: null, Body: null, WaitingOn: null, Instance: null, Owner: null, Sequence: 1);
        return new LimitEndingReader(_slice.Store, _slice.Events, new FixedItemViews(failing: false, item), new Profile(), branches ?? new Branches(ahead));
    }

    private async Task Started(DateTimeOffset at) =>
        await Append(EventTypes.LaunchStarted,
            new LaunchStarted(_launch, A.FirstItemId, LaunchMode.Open, AgentId.From("claude-code"), SessionId: null, "sindri", new LaunchViewer("warp", "Warp", ViewerOutcomeKind.Opened, Code: null, Detail: null),
                ["tmux"], CatalogSource.Builtin, "sha256:abc", _address),
            $"launch.outcome:{_launch.Value}", at);

    private async Task Ended(LimitKind kind, long limit, long peak, DateTimeOffset at) =>
        await Append(EventTypes.SessionEnded, SessionEnded.Limited(_launch, new LimitEnd(kind, limit, peak)), $"session.ended:{_launch.Value}", at);

    private async Task Append<T>(string type, T payload, string key, DateTimeOffset at)
    {
        _ = at;   // the slice harness stamps its own clock; the elapsed time is what its clock says between the two rows
        var appended = await _slice.Store.Append(_slice.Events.Entry(type, payload, key, A.FirstItemId.Value), _grant!, Token);
        Expect.True(appended.IsSuccess);
    }

    [Fact]
    public async Task A_memory_limit_names_the_limit_the_seconds_and_the_kept_branch()
    {
        var reader = await Reader(ahead: true);
        await Started(_noon);
        await Ended(LimitKind.Memory, 12L * 1024 * 1024 * 1024, 13L * 1024 * 1024 * 1024, _noon.AddMinutes(14));

        var read = await reader.Add(Failed(), _instance, Token);

        var limit = Assert.IsType<InstanceLimitEnding>(read.Limit);
        Assert.Equal(("memory", "12GiB", $"sindri/{_instance.Value}/2/work"), (limit.Kind, limit.Limit, limit.Branch));
        Assert.True(long.TryParse(limit.Elapsed, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _));
    }

    [Fact]
    public async Task A_wall_limit_is_written_as_the_profile_writes_a_duration()
    {
        var reader = await Reader(ahead: false);
        await Started(_noon);
        await Ended(LimitKind.Wall, 40 * 60, (40 * 60) + 3, _noon.AddMinutes(40));

        var read = await reader.Add(Failed(), _instance, Token);

        var limit = Assert.IsType<InstanceLimitEnding>(read.Limit);
        Assert.Equal(("wall", "40m"), (limit.Kind, limit.Limit));
    }

    [Fact]
    public async Task A_run_branch_with_no_commits_is_not_named()
    {
        var reader = await Reader(ahead: false);
        await Started(_noon);
        await Ended(LimitKind.Memory, 1024L * 1024 * 1024, 2048L * 1024 * 1024, _noon.AddMinutes(1));

        var read = await reader.Add(Failed(), _instance, Token);

        Assert.Null(Assert.IsType<InstanceLimitEnding>(read.Limit).Branch);
    }

    [Fact]
    public async Task The_branch_is_looked_up_in_the_items_repo_checkout()
    {
        var branches = new Branches(ahead: true);
        var reader = await Reader(ahead: true, branches);
        await Started(_noon);
        await Ended(LimitKind.Memory, 1024L * 1024 * 1024, 2048L * 1024 * 1024, _noon.AddMinutes(1));

        _ = await reader.Add(Failed(), _instance, Token);

        Assert.Equal(("/work/agentic-workflow", $"sindri/{_instance.Value}/2/work"), branches.Asked);
    }

    [Theory]
    [InlineData("exit")]
    [InlineData("lost")]
    public async Task A_failure_for_any_other_reason_is_left_as_it_was(string reason)
    {
        var reader = await Reader(ahead: true);
        await Started(_noon);
        await Ended(LimitKind.Memory, 1024L * 1024 * 1024, 2048L * 1024 * 1024, _noon.AddMinutes(1));

        var read = await reader.Add(Failed(reason), _instance, Token);

        Assert.Null(read.Limit);
    }

    [Fact]
    public async Task A_limit_failure_with_no_session_end_on_the_ledger_is_left_as_it_was()
    {
        var reader = await Reader(ahead: true);

        var read = await reader.Add(Failed(), _instance, Token);

        Assert.Null(read.Limit);
    }

    private sealed class Branches(bool ahead) : IRunBranches
    {
        public (string Repository, string Branch)? Asked { get; private set; }

        public ValueTask<bool> HasCommits(string repository, string branch, CancellationToken cancellationToken)
        {
            Asked = (repository, branch);
            return ValueTask.FromResult(ahead);
        }
    }

    private sealed class Profile : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
        {
            var profile = new SindriProfile(1, "thor", Repos: [new RepoEntry("agentic-workflow", "/work/agentic-workflow")]);
            return ValueTask.FromResult(Results.Success<LoadedProfile?>(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml")));
        }
    }
}
