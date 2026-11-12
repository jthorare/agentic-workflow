using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>The status says why the live profile doesn't load: 'missing' (a new home) or 'invalid' (a file that won't parse or validate), so the app never matches on text (#3113).</summary>
public sealed class StatusProblemKindTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetStatusService Service(IProfileSource live, IApprovedProfile approved) => new(_h.Held, new ProfileReads(approved, live), _h.Store, new ProjectorProgress(_h.Clock), new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot());

    [Fact, Trait("Lane", "unit")]
    public async Task A_home_with_no_profile_file_says_missing()
    {
        var status = (await Service(_h.Profiles, _h.Profiles).Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(("missing", true), (status.Profile.ProblemKind, status.Profile.Problem is not null));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_the_source_calls_invalid_says_invalid()
    {
        _h.Profiles.Live = SindriErrors.ProfileInvalid.With("/home/thor/profile.yaml: line 3: unknown key 'tennant'");

        var status = (await Service(_h.Profiles, new NoApproval()).Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal("invalid", status.Profile.ProblemKind);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_that_loads_has_no_problem_kind()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live;
        _ = await _h.HoldLease();
        _ = await _h.Profiles.Save(live, Token);

        var status = (await Service(_h.Profiles, _h.Profiles).Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal((null, null), (status.Profile.ProblemKind, status.Profile.Problem));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("schemaVersion: [unclosed\n  user: : thor\n\t- bad")]
    [InlineData("schemaVersion: 1\nuser: thor\nunknownKey: 1\n")]
    public async Task A_profile_file_that_exists_but_does_not_parse_or_validate_says_invalid_never_missing(string text)
    {
        using var files = new ProfileFiles();
        files.Write(text);

        var status = (await Service(files.Source, new NoApproval()).Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal("invalid", status.Profile.ProblemKind);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_profile_file_on_disk_says_missing()
    {
        using var files = new ProfileFiles();

        var status = (await Service(files.Source, new NoApproval()).Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal("missing", status.Profile.ProblemKind);
    }

    /// <summary>An approval read that finds nothing approved, so the status goes on to the live read.</summary>
    private sealed class NoApproval : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<LoadedProfile?>(value: null));
    }
}
