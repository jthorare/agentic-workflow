using System.Net;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ProfileLive;

/// <summary>'sindri profile approve' after approval is gone (#2258 PR 1): it answers with the removal line and exits 0, without needing the control plane.</summary>
public sealed class ProfileApproveVerbTests
{
    [Theory(Timeout = 10_000), Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("1111111111111111111111111111111111111111111111111111111111111111")]
    public async Task profile_approve_prints_the_removal_line_and_exits_0(string? hash)
    {
        var probe = new FakeControlPlaneProbe();   // nothing is running
        using var client = new ControlPlaneClient(probe, SliceHarness.Json, handler: new CliStubApi(HttpStatusCode.OK, "{}"));
        var stdout = new StringWriter();
        var verbs = new Verbs(client, probe, new Output(stdout, new StringWriter(), SliceHarness.Json), new ServiceCollection().BuildServiceProvider());

        var code = await verbs.Approve(hash, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains("removed: edits apply on save", stdout.ToString(), StringComparison.Ordinal);
    }
}
