using System.Net;
using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Onyx;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Onyx;

/// <summary>Onyx at a cluster address (deployable-context spec B.3): the profile may say `network: cluster`, but only pod mode and a host in AW_SINDRI_ONYX_HOSTS let Sindri connect.</summary>
public sealed class OnyxClusterPolicyTests
{
    private const string Pointer = "env:ONYX_FIXTURE_KEY";
    private const string ClusterUrl = "http://onyx-api-service.sindri.svc:8080";

    [Fact, Trait("Lane", "unit")]
    public void The_profile_may_name_a_cluster_address_with_network_cluster()
    {
        var settings = OnyxSettings.Create(ClusterUrl, Pointer, "v4.9.0", network: "cluster");

        Assert.True(settings.IsSuccess, settings.IsError ? settings.Error.Message : "");
        Assert.Equal((OnyxNetwork.Cluster, new Uri("http://onyx-api-service.sindri.svc:8080/")), (settings.Value.Network, settings.Value.Url));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("loopback")]
    public void Without_network_cluster_a_cluster_address_is_still_055(string? network)
    {
        var error = OnyxSettings.Create(ClusterUrl, Pointer, "v4.9.0", network: network).Error;

        Assert.Equal("SND-CONTEXT-055", error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_managed_onyx_cannot_be_on_the_cluster_network_and_an_unknown_network_is_055()
    {
        Assert.Equal("SND-CONTEXT-055", OnyxSettings.Create("http://127.0.0.1:3000", Pointer, "v4.9.0", managed: true, network: "cluster").Error.Code);
        var unknown = OnyxSettings.Create(ClusterUrl, Pointer, "v4.9.0", network: "internet").Error;
        Assert.Equal("SND-CONTEXT-055", unknown.Code);
        Assert.Contains("context.sources.onyx.network", unknown.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cluster_url_still_may_not_carry_a_user_a_query_or_a_non_http_scheme()
    {
        foreach (var url in new[] { "http://user:pw@onyx.sindri.svc", "http://onyx.sindri.svc/?k=v", "ftp://onyx.sindri.svc" })
        {
            Assert.Equal("SND-CONTEXT-055", OnyxSettings.Create(url, Pointer, "v4.9.0", network: "cluster").Error.Code);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_desktop_refuses_a_cluster_address_even_when_the_hosts_are_listed()
    {
        var policy = OnyxClusterPolicy.From(deploy: null, hosts: "onyx-api-service.sindri.svc");

        var refused = policy.Check(Cluster()).Error;

        Assert.Equal("SND-CONTEXT-055", refused.Code);
        Assert.Contains("AW_SINDRI_DEPLOY=pod", refused.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Pod_mode_refuses_a_host_that_is_not_listed_and_accepts_a_listed_one_in_any_case()
    {
        var listed = OnyxClusterPolicy.From("pod", "other.svc, Onyx-Api-Service.sindri.svc");
        var unlisted = OnyxClusterPolicy.From("pod", "other.svc");
        var none = OnyxClusterPolicy.From("pod", hosts: null);

        Assert.True(listed.Check(Cluster()).IsSuccess);
        Assert.Contains("AW_SINDRI_ONYX_HOSTS", unlisted.Check(Cluster()).Error.Message, StringComparison.Ordinal);
        Assert.Equal("SND-CONTEXT-055", none.Check(Cluster()).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_loopback_onyx_is_allowed_in_every_mode()
    {
        var local = OnyxSettings.Create("http://127.0.0.1:3000", Pointer, "v4.9.0").Value;

        Assert.True(OnyxClusterPolicy.Desktop.Check(local).IsSuccess);
        Assert.True(OnyxClusterPolicy.From("pod", hosts: null).Check(local).IsSuccess);
        Assert.True(OnyxClusterPolicy.Desktop.Check(new Uri("http://localhost:3000")).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_address_on_the_command_line_that_is_not_loopback_needs_pod_mode_and_a_listed_host()
    {
        var pod = OnyxClusterPolicy.From("pod", "onyx-api-service.sindri.svc");

        Assert.True(pod.Check(new Uri(ClusterUrl)).IsSuccess);
        Assert.False(OnyxClusterPolicy.Desktop.Check(new Uri(ClusterUrl)).IsSuccess);
        Assert.False(pod.Check(new Uri("http://elsewhere.example:8080")).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_client_sends_nothing_to_a_cluster_address_the_environment_refuses()
    {
        var requests = 0;
        using var http = new HttpClient(new CountingHandler(() => requests++));
        var parts = new OnyxSourceParts(http, _ => "on_fixture", new FakeTimeProvider(OnyxSourceHarness.Now), new CapturingLogger<OnyxClient>());
        var source = new OnyxSources(parts).Create(Cluster(), new HashSet<string>(StringComparer.Ordinal));

        var search = await source.Search(OnyxSourceHarness.Query("pane"), TestContext.Current.CancellationToken);
        var health = await source.Probe(TestContext.Current.CancellationToken);

        Assert.Equal("SND-CONTEXT-055", search.Error.Code);
        Assert.Equal((SourceState.Unavailable, "SND-CONTEXT-055"), (health.State, health.Code));
        Assert.Equal(0, requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_client_reaches_a_listed_cluster_host_in_pod_mode()
    {
        var requests = 0;
        using var http = new HttpClient(new CountingHandler(() => requests++));
        var parts = new OnyxSourceParts(http, _ => "on_fixture", new FakeTimeProvider(OnyxSourceHarness.Now), new CapturingLogger<OnyxClient>())
        {
            Policy = OnyxClusterPolicy.From("pod", "onyx-api-service.sindri.svc"),
        };
        var source = new OnyxSources(parts).Create(Cluster(), new HashSet<string>(StringComparer.Ordinal));

        _ = await source.Search(OnyxSourceHarness.Query("pane"), TestContext.Current.CancellationToken);

        Assert.Equal(1, requests);
    }

    private static OnyxSettings Cluster() => OnyxSettings.Create(ClusterUrl, OnyxSourceHarness.Pointer, "v4.9.0", network: "cluster").Value;

    private sealed class CountingHandler(Action onSend) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onSend();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"documents\":[]}") });
        }
    }
}
