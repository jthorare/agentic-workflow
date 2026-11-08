using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Features.ListConnections;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Features.TestConnection;
using Aw.Sindri.Testing.Connections;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The in-memory ports the connection slices need, and the slices built on them.</summary>
internal sealed class ConnectionHarness
{
    public ConnectionHarness()
    {
        Callers = new AsyncLocalCallerContextAccessor(Ids);
        Presets = new FakeConnectionPresets(Linear);
    }

    /// <summary>A linear-shaped preset (no client id, https endpoints, a probe URL).</summary>
    public static ConnectionPreset Linear { get; } = new(
        "linear",
        "Linear",
        ConnectionKind.OAuth,
        AuthorizeUrl: "https://linear.example.test/oauth/authorize",
        TokenUrl: "https://api.linear.example.test/oauth/token",
        RevokeUrl: "https://api.linear.example.test/oauth/revoke",
        DefaultScopes: ["read", "write"],
        HostGroup: "linear",
        LoopbackAllowed: true,
        SignInNote: "Create an OAuth application first.",
        PollableEvents: [],
        ProbeUrl: "https://api.linear.example.test/me");

    public InMemoryConnectionStore Store { get; } = new();

    public FakeConnectionSecrets Secrets { get; } = new();

    public FakeConnectionPresets Presets { get; set; }

    public FakeSignInSession SignIn { get; } = new();

    public FakeEnvironment Environment { get; } = new();

    public FakeConnectionProbe Probe { get; } = new();

    public FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    public SequentialIdGenerator Ids { get; } = new();

    public ICallerContextAccessor Callers { get; }

    public AddConnectionService Add(ISignInSession? signIn = null)
        => new(Store, Secrets, Presets, signIn ?? SignIn, Environment, Clock, Callers);

    public ListConnectionsService List() => new(Store);

    public GetConnectionService Get() => new(Store);

    public RemoveConnectionService Remove() => new(Store, Secrets);

    public TestConnectionService Test() => new(Store, Secrets, Presets, Probe, Clock);

    public static AddConnectionRequest Oauth(string name = "linear:work", string? client = "client-123", bool wait = true)
        => new(name, Preset: "linear", Kind: null, Client: client, KeyEnv: null, Scopes: [], Wait: wait);

    public static AddConnectionRequest Rest(string name = "acme:key", string keyEnv = "ACME_KEY")
        => new(name, Preset: null, Kind: "rest", Client: null, KeyEnv: keyEnv, Scopes: [], Wait: true);
}
