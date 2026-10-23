using System.Net;
using System.Net.Sockets;
using Aw.Context.Core.Onyx;
using Aw.Context.Testing.Fakes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxAccount;
using Aw.Sindri.Features.OnyxAttach;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAttach;

// `onyx attach` over the real health adapter against FakeOnyxServer, reached at a cluster name (the name resolves to the fake's loopback port, as a Service name would resolve in the cluster).
[Lane(TestLane.Integration)]
public sealed class OnyxAttachClusterNameTests
{
    private const string Host = "onyx-api-service.sindri.svc";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Attach_reaches_a_fake_onyx_by_its_cluster_name_checks_its_version_and_runs_the_account_stage()
    {
        await using var onyx = await FakeOnyxServer.Start();
        using var client = new HttpClient(Resolving(onyx.Url.Port));
        var rig = new Rig(new HttpOnyxHealth(client));
        var url = $"http://{Host}:{onyx.Url.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        var response = await rig.Service.Handle(new OnyxAttachRequest(url), Token);

        Assert.True(response.IsSuccess, response.IsError ? response.Error.Message : "");
        Assert.Equal(url, response.Value.Url);
        Assert.Equal((url, "keychain:onyx/sindri-onyx"), (rig.Profile.Section!.Value.Url, rig.Profile.Section.Value.Token));
        Assert.Contains(onyx.Requests, static r => r.Path == "/api/health");
        Assert.Contains(onyx.Requests, static r => r.Path == "/api/version");
    }

    [Fact]
    public async Task Attach_refuses_a_release_other_than_the_pin_without_signing_in()
    {
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxVersion = "v4.8.0" });
        using var client = new HttpClient(Resolving(onyx.Url.Port));
        var rig = new Rig(new HttpOnyxHealth(client));

        var response = await rig.Service.Handle(new OnyxAttachRequest($"http://{Host}:{onyx.Url.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)}"), Token);

        Assert.Equal("SND-ONYX-005", response.Error.Code);
        Assert.Equal(0, rig.Provisioning.SignIns);
    }

    // Every connection goes to the fake's loopback port, whatever name was asked for.
    private static SocketsHttpHandler Resolving(int port) => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        ConnectCallback = async (_, cancellationToken) =>
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        },
    };

    private sealed class Rig
    {
        public Rig(IOnyxHealth health)
        {
            var pins = new FakeOnyxPins(new EmbeddedOnyxPins().Pins);
            Prompts.Answer("owner@example.com", "owner-login-value-0042");
            var minter = new OnyxKeyMinter(Prompts, Provisioning, Secrets, Secrets, new FakeRandomHex(), Progress);
            var account = new OnyxAccountStage(new OnyxAccountService(new FakeOnyxWorkspace(), Prompts, new FakeBrowser(), minter, Profile, Progress));
            Service = new OnyxAttachService(OnyxClusterPolicy.From("pod", Host), health, pins, account, Progress);
        }

        public FakeOnyxPrompts Prompts { get; } = new();

        public FakeOnyxProvisioning Provisioning { get; } = new();

        public FakeSecretWriter Secrets { get; } = new();

        public FakeOnyxProfileWriter Profile { get; } = new();

        public CapturingOnyxProgress Progress { get; } = new();

        public OnyxAttachService Service { get; }
    }
}
