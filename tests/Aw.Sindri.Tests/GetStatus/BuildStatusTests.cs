using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>GET /sindri/status says which build serve runs and which is installed (#3052), so the app needs no parsing of the stale-build sentence.</summary>
public sealed class BuildStatusTests
{
    private readonly ViewsHarness _v = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<GetStatusResponse> Status(IBuildStamps? stamps)
    {
        var h = _v.Slice;
        h.Profiles.Live = SliceHarness.Profile("5s");
        _ = await h.HoldLease();
        var service = new GetStatusService(h.Held, new ProfileReads(h.Profiles, h.Profiles), h.Store, _v.Progress, new HealthServiceRows(_v.Views, [], builds: stamps), new InMemorySessionSnapshot());
        return (await service.Handle(new GetStatusRequest(), Token)).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_names_both_builds_and_is_stale_when_they_differ()
    {
        var status = await Status(new Stamps("old-stamp", "new-stamp"));

        Assert.Equal(new BuildStatus("old-stamp", "new-stamp", Stale: true), status.Build);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_is_not_stale_when_the_builds_are_the_same()
    {
        var status = await Status(new Stamps("same", "same"));

        Assert.Equal(new BuildStatus("same", "same", Stale: false), status.Build);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, "new")]
    [InlineData("old", null)]
    [InlineData(null, null)]
    public async Task Status_in_a_dev_run_has_null_stamps_and_is_not_stale(string? running, string? installed)
    {
        var status = await Status(new Stamps(running, installed));

        Assert.Equal(new BuildStatus(running, installed, Stale: false), status.Build);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_without_build_stamps_still_carries_a_build_with_nothing_known()
    {
        var status = await Status(stamps: null);

        Assert.Equal(new BuildStatus(null, null, Stale: false), status.Build);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_json_carries_the_build_in_camel_case()
    {
        var status = await Status(new Stamps("old-stamp", "new-stamp"));

        var json = Encoding.UTF8.GetString(AwJson.Serialize(status, SliceHarness.Json));

        Assert.Contains("\"build\":{\"running\":\"old-stamp\",\"installed\":\"new-stamp\",\"stale\":true}", json, StringComparison.Ordinal);
    }

    private sealed record Stamps(string? Running, string? Installed) : IBuildStamps
    {
        string? IBuildStamps.Running() => Running;

        string? IBuildStamps.Installed() => Installed;
    }
}
