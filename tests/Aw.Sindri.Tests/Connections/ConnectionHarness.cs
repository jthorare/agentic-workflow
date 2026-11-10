using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Features.ListConnections;
using Aw.Sindri.Features.RefreshConnections;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Features.RenameConnection;
using Aw.Sindri.Features.TestConnection;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Testing.Workflow;
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
        Workflows = new InMemoryWorkflowStore(Clock);
    }

    /// <summary>The workflows a rename checks for triggers that watch the connection.</summary>
    public InMemoryWorkflowStore Workflows { get; }

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

    /// <summary>Gets the memory of failed sign-ins the add flow writes and the get slice reads.</summary>
    public SignInOutcomes Outcomes { get; } = new();

    public FakeEnvironment Environment { get; } = new();

    public FakeConnectionProbe Probe { get; } = new();

    /// <summary>Gets the command gate the test slice's status write waits for.</summary>
    internal CommandGateLock Gate { get; } = new();

    public FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    /// <summary>Gets the ledger the connection slices append to (the green wires it in; the secret-leak tests read every event from it).</summary>
    public InMemorySindriStore Ledger { get; }

    /// <summary>Gets the log every connection slice writes to (the green wires it in; the secret-leak tests read every line from it).</summary>
    public CapturingConnectionLog Logs { get; } = new();

    public SequentialIdGenerator Ids { get; } = new();

    public ICallerContextAccessor Callers { get; }

    public AddConnectionService Add(ISignInSession? signIn = null)
        => new(Store, Secrets, Presets, Environment, Clock, Callers, new OAuthSignInFlow(Store, Secrets, signIn ?? SignIn, new CommandGateAccess(Gate, Clock), Outcomes, Events(), new TypedLog<OAuthSignInFlow>(Logs)));

    /// <summary>Gets the lease the connection events are fenced by; empty until <see cref="Hold"/>.</summary>
    public HeldLease Held { get; } = new();

    /// <summary>Takes the ledger's lease and holds it, so the slices can record 'connection.signed-in'.</summary>
    /// <returns>A task that ends when the lease is held.</returns>
    public async Task Hold() => Held.Hold((await Ledger.Acquire(LedgerProbe.Self(), TestContext.Current.CancellationToken)).Value);

    /// <summary>The event recorder over the harness's lease, ledger and clock.</summary>
    /// <returns>The recorder.</returns>
    public ConnectionEvents Events() => new(Held, Ledger, new EventFactory(EventScope.ControlPlane, Clock, Ids, SliceHarness.Json, Callers), Clock);

    /// <summary>The status writer over the harness's store, secrets, events and command gate.</summary>
    /// <returns>The writer.</returns>
    public ConnectionStatusWriter Writer() => new(Store, Secrets, Events(), new CommandGateAccess(Gate, Clock));

    public ListConnectionsService List() => new(Store);

    public GetConnectionService Get() => new(Store, Outcomes);

    public RemoveConnectionService Remove() => new(Store, Secrets, Events());

    public RenameConnectionService Rename() => new(Store, Secrets, Events(), Workflows);

    /// <summary>The sign-in-again slice over the harness's ports and the same sign-in flow Add builds.</summary>
    /// <returns>The service.</returns>
    public SignInConnectionService SignInAgain(ISignInSession? signIn = null)
        => new(Store, Secrets, Presets, Callers, new OAuthSignInFlow(Store, Secrets, signIn ?? SignIn, new CommandGateAccess(Gate, Clock), Outcomes, Events(), new TypedLog<OAuthSignInFlow>(Logs)), Events());

    /// <summary>The refresh service over the harness's ports, with the ledger and an event factory on the harness clock.</summary>
    /// <returns>The service.</returns>
    /// <param name="oauth">The token client; the scripted one when null.</param>
    public RefreshConnectionsService Refresh(IOAuthClient? oauth = null)
        => new(Store, Secrets, Presets, oauth ?? OAuth, Writer(), Clock, new TypedLog<RefreshConnectionsService>(Logs));

    /// <summary>The refresh service over another connection store (for a store that fails), with the harness's other ports.</summary>
    /// <param name="store">The store.</param>
    /// <returns>The service.</returns>
    public RefreshConnectionsService RefreshOver(IConnectionStore store)
        => new(store, Secrets, Presets, OAuth, new ConnectionStatusWriter(store, Secrets, Events(), new CommandGateAccess(Gate, Clock)), Clock, new TypedLog<RefreshConnectionsService>(Logs));

    /// <summary>Takes the ledger's lease (once; later ticks reuse it) and builds the run a tick would hand the refresh service.</summary>
    /// <returns>The run, at the harness clock.</returns>
    public async Task<ScheduledRun> Tick()
    {
        if (Held.Current is null)
        {
            await Hold();
        }

        return new(Held.Current!, Clock.GetUtcNow());
    }


    /// <summary>The test slice over another probe, with the capturing log, so a probe that echoes the credential is seen by the log canary.</summary>
    /// <param name="probe">The probe.</param>
    /// <returns>The service.</returns>
    public TestConnectionService TestWith(IConnectionProbe probe) => new(Store, Secrets, Presets, probe, Writer(), new TypedLog<TestConnectionService>(Logs));

    public TestConnectionService Test(IMcpConnectionClient? mcp = null) => new(Store, Secrets, Presets, Probe, Writer(), new TypedLog<TestConnectionService>(Logs), mcp);

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
