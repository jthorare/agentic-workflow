using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="Core.Onyx.IOnyxHealth"/> honours: it never throws for a server that isn't there or answers badly, and reports the version only once health answers.</summary>
public abstract class OnyxHealthContract
{
    /// <summary>A rig over a server that behaves as <paramref name="script"/> says.</summary>
    /// <param name="script">What the server does.</param>
    /// <returns>The rig.</returns>
    protected abstract OnyxHealthRig Create(OnyxServerScript script);

    private async Task AssertProbes(OnyxServerScript script)
    {
        using var rig = Create(script);

        var probe = await rig.Health.Probe(rig.Port, TestContext.Current.CancellationToken);

        Expect.Equal(script.Expected, probe);
    }

    /// <summary>Nothing listening is not healthy.</summary>
    [Fact]
    public Task Nothing_listening_is_not_healthy() => AssertProbes(OnyxServerScript.Nothing);

    /// <summary>A healthy server reports its backend version.</summary>
    [Fact]
    public Task A_healthy_server_reports_its_backend_version() => AssertProbes(OnyxServerScript.Ready("v4.9.0"));

    /// <summary>A garbled version body still counts as healthy, with no version.</summary>
    [Fact]
    public Task A_health_answer_with_a_garbled_version_body_is_healthy_with_a_null_version() => AssertProbes(OnyxServerScript.GarbledVersion);

    /// <summary>A missing version route is healthy with no version.</summary>
    [Fact]
    public Task A_health_answer_with_no_version_route_is_healthy_with_a_null_version() => AssertProbes(OnyxServerScript.NoVersionRoute);

    /// <summary>A 5xx on the health route is not healthy.</summary>
    [Fact]
    public Task A_5xx_health_is_not_healthy() => AssertProbes(OnyxServerScript.Failing);
}
