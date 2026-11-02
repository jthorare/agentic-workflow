using System.Net;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

public sealed class EgressPolicyTests
{
    private static readonly IReadOnlyList<PlanGroup> _groups =
    [
        new("model", ["api.anthropic.com"]),
        new("packages", ["api.nuget.org", "*.nuget.org"]),
        new("repo", ["10.1.2.3"]),
    ];

    private static EgressPolicy Policy() => new(_groups, new FakeEgressResolver(new Dictionary<string, IPAddress[]>(StringComparer.Ordinal)
    {
        ["api.anthropic.com"] = [IPAddress.Parse("160.79.104.10")],
        ["globalcdn.nuget.org"] = [IPAddress.Parse("13.107.4.50")],
        ["api.nuget.org"] = [IPAddress.Parse("10.0.0.5")],
    }));

    [Fact, Trait("Lane", "unit")]
    public async Task Static_group_host_is_allowed_with_its_resolved_address()
    {
        var decision = await Policy().Decide("api.anthropic.com", 443, TestContext.Current.CancellationToken);

        Assert.Equal(EgressVerdict.Allowed, decision.Verdict);
        Assert.Equal(IPAddress.Parse("160.79.104.10"), decision.Address);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wildcard_group_host_is_allowed() =>
        Assert.Equal(EgressVerdict.Allowed, (await Policy().Decide("globalcdn.nuget.org", 443, TestContext.Current.CancellationToken)).Verdict);

    [Theory, Trait("Lane", "unit")]
    [InlineData("evil.example", 443)]
    [InlineData("api.anthropic.com", 22)]
    [InlineData("anthropic.com.evil.example", 443)]
    public async Task Host_or_port_outside_the_groups_is_denied(string host, int port) =>
        Assert.Equal(EgressVerdict.Denied, (await Policy().Decide(host, port, TestContext.Current.CancellationToken)).Verdict);

    [Fact, Trait("Lane", "unit")]
    public async Task Private_resolution_is_refused_unless_a_group_lists_it()
    {
        var refused = await Policy().Decide("api.nuget.org", 443, TestContext.Current.CancellationToken);
        var listed = await Policy().Decide("10.1.2.3", 443, TestContext.Current.CancellationToken);

        Assert.Equal(EgressVerdict.Refused, refused.Verdict);
        Assert.Equal(EgressVerdict.Allowed, listed.Verdict);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("::1")]
    [InlineData("192.168.1.1")]
    public async Task Address_literals_not_in_a_group_are_denied(string literal) =>
        Assert.Equal(EgressVerdict.Denied, (await Policy().Decide(literal, 80, TestContext.Current.CancellationToken)).Verdict);
}
