using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.AllowDenial;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Sandbox.Denials;

namespace Aw.Sindri.Tests.AllowDenial;

/// <summary>The Allow button (sandbox spec §3.6): a live-profile edit under the blocked run's repo, recorded as 'sandbox.allowed' with the caller's actor; it takes effect once the profile is approved. Only a host or a not-bound home path can be allowed.</summary>
[Lane(TestLane.Unit)]
public sealed class AllowDenialTests
{
    private static readonly DateTimeOffset _noon = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly LaunchId _launch = LaunchId.From(DenialsRendererTests.LaunchOne);

    private readonly SliceHarness _slice = new();
    private readonly FakeWriter _writer = new();
    private readonly InMemoryProfiles _profiles = new();
    private readonly List<DenialEntry> _entries =
    [
        new(DenialKind.Network, "github.com", DenialRules.NoGroup, 3, _noon, _noon),
        new(DenialKind.Path, "/home/o/.cache/tool", DenialRules.NotBound, 1, _noon, _noon),
        new(DenialKind.Path, "/var/run/docker.sock", DenialRules.DenyList, 1, _noon, _noon),
        new(DenialKind.Policy, "git push", DenialRules.InnerPolicy, 1, _noon, _noon),
    ];

    private RepoName? _repo = RepoName.From("web");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // why: the caller lives in an AsyncLocal, so it is entered synchronously in the test itself; one entered inside an awaited helper is gone when the helper returns
    private IDisposable AsHuman() => _slice.Callers.Enter(A.Caller().Human().Build());

    private AllowDenialService Service()
    {
        var files = new StubFiles(new DenialFile(_launch, _noon, _entries, []));
        var owners = new StubOwners(_repo is { } repo ? new Dictionary<LaunchId, LaunchOwner> { [_launch] = new(A.FirstItemId, repo) } : []);
        return new AllowDenialService(files, owners, _writer, _profiles, _slice.Callers, _slice.Held, new SandboxAllowedRecorder(_slice.Store, _slice.Events));
    }

    [Fact]
    public async Task Allowing_a_host_edits_the_live_profile_under_the_runs_repo_and_waits_for_approval()
    {
        _ = await _slice.HoldLease();
        using var entered = AsHuman();
        var result = await Service().Handle(new AllowDenialRequest(_launch, DenialKind.Network, "github.com"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal((AllowDenialState.NeedsApproval, "web", "github.com"), (result.Value.State, result.Value.Repo, result.Value.Entry));
        Assert.Equal([("web", DenialKind.Network, "github.com")], _writer.Calls);
    }

    [Fact]
    public async Task Allowing_a_home_path_writes_it_as_the_profile_holds_it()
    {
        _ = await _slice.HoldLease();
        using var entered = AsHuman();
        _writer.Stored = ".cache/tool";

        var result = await Service().Handle(new AllowDenialRequest(_launch, DenialKind.Path, "/home/o/.cache/tool"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal((AllowDenialState.NeedsApproval, ".cache/tool"), (result.Value.State, result.Value.Entry));
    }

    [Fact]
    public async Task An_entry_the_approved_profile_already_holds_is_allowed()
    {
        _ = await _slice.HoldLease();
        using var entered = AsHuman();
        var approved = new SindriProfile(1, "o", Sandbox: new SandboxSettings(Repos: new Dictionary<string, SandboxRepoSettings>(StringComparer.Ordinal) { ["web"] = new(Hosts: ["github.com"]) }));
        _ = await _profiles.Save(new LoadedProfile(approved, ProfileHashing.Of(AwJson.Serialize(approved, SliceHarness.Json)), "/home/profile.yaml"), Token);

        var result = await Service().Handle(new AllowDenialRequest(_launch, DenialKind.Network, "github.com"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(AllowDenialState.Allowed, result.Value.State);
    }

    [Fact]
    public async Task Allowing_twice_asks_for_approval_twice_and_writes_one_entry()
    {
        _ = await _slice.HoldLease();
        using var entered = AsHuman();
        _ = await Service().Handle(new AllowDenialRequest(_launch, DenialKind.Network, "github.com"), Token);
        _writer.Added = false;   // the live profile already held it

        var again = await Service().Handle(new AllowDenialRequest(_launch, DenialKind.Network, "github.com"), Token);

        Assert.True(again.IsSuccess, again.IsError ? again.Error.ToString() : null);
        Assert.Equal(AllowDenialState.NeedsApproval, again.Value.State);
    }

    [Theory]
    [InlineData(DenialKind.Policy, "git push")]
    [InlineData(DenialKind.Path, "/var/run/docker.sock")]
    [InlineData(DenialKind.Network, "never-blocked.example")]
    public async Task What_cannot_be_allowed_is_refused_and_the_profile_is_untouched(DenialKind kind, string target)
    {
        _ = await _slice.HoldLease();
        using var entered = AsHuman();
        var result = await Service().Handle(new AllowDenialRequest(_launch, kind, target), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.SandboxAllowRefused.Code, result.Error.Code);
        Assert.Empty(_writer.Calls);
    }

    [Fact]
    public async Task An_unknown_launch_is_refused()
    {
        _ = await _slice.HoldLease();
        using var entered = AsHuman();
        var result = await Service().Handle(new AllowDenialRequest(LaunchId.From(DenialsRendererTests.LaunchTwo), DenialKind.Network, "github.com"), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.SandboxAllowRefused.Code, result.Error.Code);
        Assert.Empty(_writer.Calls);
    }

    [Fact]
    public async Task A_run_whose_item_has_no_repo_is_refused()
    {
        _ = await _slice.HoldLease();
        using var entered = AsHuman();
        _repo = null;

        var result = await Service().Handle(new AllowDenialRequest(_launch, DenialKind.Network, "github.com"), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.SandboxAllowRefused.Code, result.Error.Code);
        Assert.Empty(_writer.Calls);
    }

    [Fact]
    public async Task A_launched_session_allows_a_denial_and_the_event_names_that_session()
    {
        _ = await _slice.HoldLease();
        using var entered = _slice.Callers.Enter(A.Caller().Launched().Build());

        var result = await Service().Handle(new AllowDenialRequest(_launch, DenialKind.Network, "github.com"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal([("web", DenialKind.Network, "github.com")], _writer.Calls);
        var recorded = Assert.Single(_slice.Store.Events);
        var payload = _slice.Events.Payload<SandboxEntryAllowed>(recorded).Value;
        Expect.Equal((EventTypes.SandboxAllowed, "session:launch:LCH-00000000000000000000000000000001"), (recorded.Envelope.Type, recorded.Envelope.Actor));
        Expect.Equal((_launch, "web", "Network", "github.com"), (payload.Launch, payload.Repo.Value, payload.Kind, payload.Entry));
    }

    [Fact]
    public async Task A_refusal_records_no_event()
    {
        _ = await _slice.HoldLease();
        using var entered = AsHuman();

        _ = await Service().Handle(new AllowDenialRequest(_launch, DenialKind.Policy, "git push"), Token);

        Assert.Empty(_slice.Store.Events);
    }

    [Fact]
    public void A_launched_session_may_allow_a_denial_like_the_human()
    {
        var row = new CommandCatalog().Rows.Single(r => string.Equals(r.Action.Value, "AllowDenial", StringComparison.Ordinal));

        var launched = Authorization.Decide(row.Class, A.Caller().Launched().Build(), row);
        var human = Authorization.Decide(row.Class, A.Caller().Human().Build(), row);

        Expect.True(launched.Refusal is null);
        Expect.True(human.Refusal is null);
    }

    private sealed class FakeWriter : IProfileSandboxWriter
    {
        public List<(string Repo, DenialKind Kind, string Target)> Calls { get; } = [];

        public string? Stored { get; set; }

        public bool Added { get; set; } = true;

        public ValueTask<Result<SandboxAllowed>> Allow(string repo, DenialKind kind, string target, CancellationToken cancellationToken)
        {
            Calls.Add((repo, kind, target));
            return ValueTask.FromResult<Result<SandboxAllowed>>(new SandboxAllowed(Stored ?? target, Added));
        }
    }

    private sealed class StubOwners(Dictionary<LaunchId, LaunchOwner> owners) : ILaunchOwners
    {
        public ValueTask<IReadOnlyDictionary<LaunchId, LaunchOwner>> Read(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyDictionary<LaunchId, LaunchOwner>>(owners);
    }

    private sealed class StubFiles(params DenialFile[] files) : IDenialFiles
    {
        public ValueTask<IReadOnlyList<DenialFile>> List(LaunchId? launch, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DenialFile>>([.. files.Where(f => launch is not { } one || f.LaunchId == one)]);
    }
}
