using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RefreshConnections;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Features.ListConnections;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Features.TestConnection;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The in-memory ports the connection slices need, and the slices built on them.</summary>
internal sealed class ConnectionHarness
{
    public ConnectionHarness()
    {
        Ledger = new InMemorySindriStore(new FakeTimeProvider(KnownInstants.Epoch), LedgerProbe);
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

    /// <summary>Gets the process probe the ledger's lease reads.</summary>
    public FakeProcessProbe LedgerProbe { get; } = new();

    /// <summary>Gets the token client the refresh service calls.</summary>
    public FakeOAuthClient OAuth { get; } = new();

    public InMemoryConnectionStore Store { get; } = new();

    public FakeConnectionSecrets Secrets { get; } = new();

    public FakeConnectionPresets Presets { get; set; }

    public FakeSignInSession SignIn { get; } = new();

    public FakeEnvironment Environment { get; } = new();

    public FakeConnectionProbe Probe { get; } = new();

    public FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    /// <summary>Gets the ledger the connection slices append to (the green wires it in; the secret-leak tests read every event from it).</summary>
    public InMemorySindriStore Ledger { get; }

    /// <summary>Gets the log every connection slice writes to (the green wires it in; the secret-leak tests read every line from it).</summary>
    public CapturingConnectionLog Logs { get; } = new();

    public SequentialIdGenerator Ids { get; } = new();

    public ICallerContextAccessor Callers { get; }

    public AddConnectionService Add(ISignInSession? signIn = null)
        => new(Store, Secrets, Presets, signIn ?? SignIn, Environment, Clock, Callers);

    public ListConnectionsService List() => new(Store);

    public GetConnectionService Get() => new(Store);

    public RemoveConnectionService Remove() => new(Store, Secrets);

    /// <summary>The refresh service over the harness's ports, with the ledger and an event factory on the harness clock.</summary>
    /// <returns>The service.</returns>
    public RefreshConnectionsService Refresh()
        => new(Store, Secrets, Presets, OAuth, Ledger, new EventFactory(EventScope.ControlPlane, Clock, Ids, SliceHarness.Json, Callers), Clock);

    /// <summary>Takes the ledger's lease and builds the run a tick would hand the refresh service.</summary>
    /// <returns>The run, at the harness clock.</returns>
    public async Task<ScheduledRun> Tick()
        => new((await Ledger.Acquire(LedgerProbe.Self(), TestContext.Current.CancellationToken)).Value, Clock.GetUtcNow());

    public TestConnectionService Test(IMcpConnectionClient? mcp = null) => new(Store, Secrets, Presets, Probe, Clock, new TypedLog<TestConnectionService>(Logs), mcp);

    /// <summary>Lends the capturing log to a service that wants an ILogger of its own type.</summary>
    /// <typeparam name="T">The service.</typeparam>
    /// <param name="inner">The capturing log.</param>
    private sealed class TypedLog<T>(ILogger inner) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => inner.Log(logLevel, eventId, state, exception, formatter);
    }

    public static AddConnectionRequest Oauth(string name = "linear:work", string? client = "client-123", bool wait = true)
        => new(name, Preset: "linear", Kind: null, Client: client, KeyEnv: null, Scopes: [], Wait: wait);

    public static AddConnectionRequest Rest(string name = "acme:key", string keyEnv = "ACME_KEY")
        => new(name, Preset: null, Kind: "rest", Client: null, KeyEnv: keyEnv, Scopes: [], Wait: true);
}
