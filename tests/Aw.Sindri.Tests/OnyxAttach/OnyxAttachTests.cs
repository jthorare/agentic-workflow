using Aw.Context.Core.Onyx;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxAccount;
using Aw.Sindri.Features.OnyxAttach;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAttach;

// `sindri onyx attach <url>`: the environment's rule for a cluster address, the health and version check, then the account stage against that address.
public sealed class OnyxAttachTests
{
    private const string Url = "http://onyx-api-service.sindri.svc:8080";
    private const string Login = "owner-login-value-0042";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Rig
    {
        public Rig(string? deploy = "pod", string? hosts = "onyx-api-service.sindri.svc")
        {
            Pins = new FakeOnyxPins(new EmbeddedOnyxPins().Pins);
            Health = new FakeOnyxHealth { Default = new OnyxProbe(Healthy: true, Pins.Pins.Version) };
            Prompts.Answer("owner@example.com", Login);
            var minter = new OnyxKeyMinter(Prompts, Provisioning, Secrets, Secrets, new FakeRandomHex(), Progress);
            var account = new OnyxAccountStage(new OnyxAccountService(Workspace, Prompts, Browser, minter, Profile, Progress));
            Service = new OnyxAttachService(OnyxClusterPolicy.From(deploy, hosts), Health, Pins, account, Progress);
        }

        public FakeOnyxPins Pins { get; }

        public FakeOnyxHealth Health { get; }

        public FakeOnyxWorkspace Workspace { get; } = new();

        public FakeOnyxPrompts Prompts { get; } = new();

        public FakeBrowser Browser { get; } = new();

        public FakeOnyxProvisioning Provisioning { get; } = new();

        public FakeSecretWriter Secrets { get; } = new();

        public FakeOnyxProfileWriter Profile { get; } = new();

        public CapturingOnyxProgress Progress { get; } = new();

        public OnyxAttachService Service { get; }

        public ValueTask<Kernel.Result<OnyxAccountStageResult>> Attach(string url = Url) => Service.Handle(new OnyxAttachRequest(url), Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_healthy_pinned_onyx_at_a_listed_cluster_host_gets_the_account_stage_against_that_address()
    {
        var rig = new Rig();

        var response = (await rig.Attach()).Value;

        Assert.Equal(Url, response.Url);
        Assert.Equal(["signup", "key", "profile"], response.Steps.Select(static s => s.Step), StringComparer.Ordinal);
        Assert.Equal(new Uri(Url), Assert.Single(rig.Health.ProbedOrigins).Url);
        Assert.All(rig.Provisioning.Origins, origin => Assert.Equal(new Uri(Url), origin.Url));
        Assert.Equal((Url, "keychain:onyx/sindri-onyx", rig.Pins.Pins.Version), rig.Profile.Section);
        Assert.Equal(new Uri(Url + "/auth/signup"), Assert.Single(rig.Browser.Opened));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_desktop_refuses_a_cluster_address_before_asking_it_anything()
    {
        var rig = new Rig(deploy: null);

        var error = (await rig.Attach()).Error;

        Assert.Equal("SND-CONTEXT-055", error.Code);
        Assert.Empty(rig.Health.ProbedOrigins);
        Assert.Equal(0, rig.Provisioning.SignIns);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pod_mode_refuses_a_host_that_is_not_in_the_environments_list()
    {
        var rig = new Rig(hosts: "some-other-host");

        var error = (await rig.Attach()).Error;

        Assert.Equal("SND-CONTEXT-055", error.Code);
        Assert.Contains("AW_SINDRI_ONYX_HOSTS", error.Message, StringComparison.Ordinal);
        Assert.Empty(rig.Health.ProbedOrigins);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("onyx-api-service")]
    [InlineData("ftp://onyx-api-service.sindri.svc")]
    [InlineData("http://user:pw@onyx-api-service.sindri.svc")]
    [InlineData("http://onyx-api-service.sindri.svc:8080/api")]
    [InlineData("http://onyx-api-service.sindri.svc:8080/?x=1")]
    public async Task Anything_but_a_base_url_is_invalid_input(string url)
    {
        var rig = new Rig();

        var error = (await rig.Attach(url)).Error;

        Assert.Equal(SindriErrors.InvalidInput.Code, error.Code);
        Assert.Empty(rig.Health.ProbedOrigins);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://127.0.0.1:3000")]
    [InlineData("http://localhost:3000")]
    public async Task A_loopback_address_is_up_not_attach(string url)
    {
        var rig = new Rig();

        var error = (await rig.Attach(url)).Error;

        Assert.Equal(SindriErrors.InvalidInput.Code, error.Code);
        Assert.Contains("sindri onyx up", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_onyx_that_does_not_answer_health_is_not_ready_and_nothing_is_set_up()
    {
        var rig = new Rig();
        rig.Health.Default = new OnyxProbe(Healthy: false, BackendVersion: null);

        var error = (await rig.Attach()).Error;

        Assert.Equal(SindriErrors.OnyxNotReady.Code, error.Code);
        Assert.Contains("/api/health", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, rig.Provisioning.SignIns);
        Assert.Null(rig.Profile.Section);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("v4.8.0")]
    public async Task A_version_other_than_the_pin_is_not_ready_and_names_the_pin(string? version)
    {
        var rig = new Rig();
        rig.Health.Default = new OnyxProbe(Healthy: true, version);

        var error = (await rig.Attach()).Error;

        Assert.Equal(SindriErrors.OnyxNotReady.Code, error.Code);
        Assert.Equal(0, rig.Provisioning.SignIns);
        if (version is not null)
        {
            Assert.Contains(rig.Pins.Pins.Version, error.Message, StringComparison.Ordinal);
        }
    }
}
