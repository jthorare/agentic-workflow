using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.AllowDenial;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Sandbox.Denials;

namespace Aw.Sindri.Tests.AllowDenial;

/// <summary>The Allow button (sandbox spec §3.6): a draft profile edit under the blocked run's repo, then the owner's approval. Only a host or a not-bound home path can be allowed.</summary>
[Lane(TestLane.Unit)]
public sealed class AllowDenialTests
{
    private static readonly DateTimeOffset _noon = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly LaunchId _launch = LaunchId.From(DenialsRendererTests.LaunchOne);

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

    private AllowDenialService Service()
    {
        var files = new StubFiles(new DenialFile(_launch, _noon, _entries, []));
        var owners = new StubOwners(_repo is { } repo ? new Dictionary<LaunchId, LaunchOwner> { [_launch] = new(A.FirstItemId, repo) } : []);
        return new AllowDenialService(files, owners, _writer, _profiles);
    }

    [Fact]
    public async Task Allowing_a_host_edits_the_live_profile_under_the_runs_repo_and_waits_for_approval()
    {
        var result = await Service().Handle(new AllowDenialRequest(_launch, DenialKind.Network, "github.com"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal((AllowDenialState.NeedsApproval, "web", "github.com"), (result.Value.State, result.Value.Repo, result.Value.Entry));
        Assert.Equal([("web", DenialKind.Network, "github.com")], _writer.Calls);
    }

    [Fact]
    public async Task Allowing_a_home_path_writes_it_as_the_profile_holds_it()
    {
        _writer.Stored = ".cache/tool";

        var result = await Service().Handle(new AllowDenialRequest(_launch, DenialKind.Path, "/home/o/.cache/tool"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal((AllowDenialState.NeedsApproval, ".cache/tool"), (result.Value.State, result.Value.Entry));
    }

    [Fact]
    public async Task An_entry_the_approved_profile_already_holds_is_allowed()
    {
        var approved = new SindriProfile(1, "o", Sandbox: new SandboxSettings(Repos: new Dictionary<string, SandboxRepoSettings>(StringComparer.Ordinal) { ["web"] = new(Hosts: ["github.com"]) }));
        _ = await _profiles.Save(new LoadedProfile(approved, ProfileHashing.Of(AwJson.Serialize(approved, SliceHarness.Json)), "/home/profile.yaml"), Token);

        var result = await Service().Handle(new AllowDenialRequest(_launch, DenialKind.Network, "github.com"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(AllowDenialState.Allowed, result.Value.State);
    }

    [Fact]
    public async Task Allowing_twice_asks_for_approval_twice_and_writes_one_entry()
    {
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
        var result = await Service().Handle(new AllowDenialRequest(_launch, kind, target), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.SandboxAllowRefused.Code, result.Error.Code);
        Assert.Empty(_writer.Calls);
    }

    [Fact]
    public async Task An_unknown_launch_is_refused()
    {
        var result = await Service().Handle(new AllowDenialRequest(LaunchId.From(DenialsRendererTests.LaunchTwo), DenialKind.Network, "github.com"), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.SandboxAllowRefused.Code, result.Error.Code);
        Assert.Empty(_writer.Calls);
    }

    [Fact]
    public async Task A_run_whose_item_has_no_repo_is_refused()
    {
        _repo = null;

        var result = await Service().Handle(new AllowDenialRequest(_launch, DenialKind.Network, "github.com"), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.SandboxAllowRefused.Code, result.Error.Code);
        Assert.Empty(_writer.Calls);
    }

    [Fact]
    public void Allow_is_a_human_verb_a_launched_session_is_refused()
    {
        var row = new CommandCatalog().Rows.Single(r => string.Equals(r.Action.Value, "AllowDenial", StringComparison.Ordinal));

        var launched = Authorization.Decide(row.Class, A.Caller().Launched().Build(), row);
        var human = Authorization.Decide(row.Class, A.Caller().Human().Build(), row);

        Expect.Equal(SindriErrors.AuthRefused.Code, launched.Refusal?.Code);
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
