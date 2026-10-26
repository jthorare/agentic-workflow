using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// A control plane whose answers are byte-stable, for the API goldens (UI spec E-17, plan P1 unit E): real adapters in a
/// temp home with a fixed profile, a <see cref="FakeTimeProvider"/> at <see cref="KnownInstants.Epoch"/>, sequential ids,
/// a fixed pid, the lease held (StartProcess under service:host) and a fixed human token held, on loopback through the
/// pipeline Program runs (the wiring of CliEndToEndTests). Each scenario starts its own, so no golden depends on the
/// order the scenarios run in.
/// </summary>
public sealed class GoldenHost : IAsyncDisposable
{
    /// <summary>The pid the fake process probe reports for this process.</summary>
    public const int Pid = 4242;

    /// <summary>The profile every golden host starts with: one scheduled service, so status lists it.</summary>
    public const string ProfileYaml = "schemaVersion: 1\nuser: golden\nservices:\n  heartbeat: { every: 5s }\n";

    private readonly TempHome? _ownHome;
    private readonly List<IDisposable> _owned = [];
    private readonly TempHome? _elsewhere;
    private WebApplication? _app;

    private readonly string _profileYaml;
    private readonly IReadOnlyDictionary<string, string>? _environment;
    private readonly Action<IServiceCollection>? _extra;
    private readonly Action<WebApplication>? _map;
    private readonly SequentialIdGenerator _ids;

    private GoldenHost(bool temporary, string profileYaml, IReadOnlyDictionary<string, string>? environment, Action<IServiceCollection>? extra, Action<WebApplication>? map, string? home, uint idScope)
    {
        _ids = new SequentialIdGenerator(idScope);
        _ownHome = home is null ? new TempHome() : null;
        Home = home ?? _ownHome!.Path;   // why: _ownHome is set on the line above whenever home is null
        _map = map;
        _elsewhere = temporary ? null : new TempHome();
        _profileYaml = profileYaml;
        _environment = environment;
        _extra = extra;
    }

    /// <summary>The token the host holds.</summary>
    public HumanToken Token { get; } = new("golden-human-token");

    /// <summary>The temp home (normalized to '&lt;home&gt;' in the goldens).</summary>
    public string Home { get; }

    /// <summary>A client on the host's loopback address.</summary>
    public HttpClient Http { get; private set; } = new();

    /// <summary>The host's clock now.</summary>
    /// <returns>The instant.</returns>
    public DateTimeOffset Now() => App.Services.GetRequiredService<TimeProvider>().GetUtcNow();

    /// <summary>The running host.</summary>
    public WebApplication App => _app ?? throw new InvalidOperationException("the host isn't started");

    /// <summary>Starts a host. <paramref name="temporary"/> false points TMPDIR and AW_STATE_DIR away from its home, so the prover label is refused.</summary>
    /// <param name="temporary">Whether the home counts as temporary (honours the prover label).</param>
    /// <param name="ct">Cancellation.</param>
    /// <param name="profileYaml">The live profile it starts with (<see cref="ProfileYaml"/> unless a test needs another).</param>
    /// <param name="environment">More environment variables the host reads (fixture secrets behind 'env:' pointers), or null.</param>
    /// <param name="extra">More service replacements, after the fixed ones (a test's log sink).</param>
    /// <param name="map">More routes, mapped after the product's (a test-only route; never part of the production host), or null.</param>
    /// <param name="idScope">The scope of the sequential ids; a second host on one home (a restart) takes another, so its ids don't repeat the first one's.</param>
    /// <param name="home">An existing home to start on (a restart: a second host on the first one's home, which the caller removes), or null for a fresh temp home.</param>
    /// <returns>The started host.</returns>
    public static async Task<GoldenHost> Start(bool temporary, CancellationToken ct, string profileYaml = ProfileYaml,
        IReadOnlyDictionary<string, string>? environment = null, Action<IServiceCollection>? extra = null, Action<WebApplication>? map = null, string? home = null, uint idScope = 0)
    {
        var host = new GoldenHost(temporary, profileYaml, environment, extra, map, home, idScope);
        try
        {
            await host.Run(ct);
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    /// <summary>Hands the host something to dispose after it stops (a fixture repo the profile maps).</summary>
    /// <param name="resource">The resource.</param>
    public void Own(IDisposable resource) => _owned.Add(resource);

    /// <summary>The live profile's hash.</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The hash.</returns>
    public async Task<ProfileHash> LiveHash(CancellationToken ct) =>
        (await App.Services.GetRequiredService<IProfileSource>().Load(ct)).Value.Hash;

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync(CancellationToken.None);
            await _app.DisposeAsync();
        }

        _ownHome?.Dispose();
        _elsewhere?.Dispose();
        _owned.ForEach(o => o.Dispose());
    }

    private async Task Run(CancellationToken ct)
    {
        _ = Directory.CreateDirectory(Path.Combine(Home, "profile"));
        await File.WriteAllTextAsync(Path.Combine(Home, "profile", "profile.yaml"), _profileYaml, ct);
        var app = LoopbackBinding.Build([], Environment, new FakeTimeProvider(KnownInstants.Epoch), services =>
        {
            // why: an ordinary build embeds the checkout's recipes/sindri (#878), and unapproved shipped recipes would add a setup row to every golden; a test that needs a shipped set replaces this in 'extra'
            _ = services.Replace(ServiceDescriptor.Singleton<IEmbeddedRecipes>(FakeEmbeddedRecipes.Empty));
            // why: the setup route's Onyx step asks Docker and the loopback ports of the box the test runs on, and a box with a hung Docker would fail every route test (#1121); a test that needs another replaces these in 'extra'
            _ = services.Replace(ServiceDescriptor.Singleton<IContainerRuntime>(new FakeContainerRuntime(FakeContainerRuntime.Healthy)));
            _ = services.Replace(ServiceDescriptor.Singleton<IHostProbes>(new FakeHostProbes()));
            _extra?.Invoke(services);
            _ = services.Replace(ServiceDescriptor.Singleton<IIdGenerator>(_ids));
            _ = services.Replace(ServiceDescriptor.Singleton<IProcessProbe>(new FakeProcessProbe(Pid)));
            NoTemporal.Use(services);   // the Temporal side is faked: the goldens are about the API
        });
        _app = app;
        _ = Pipeline.Configure(app);
        _map?.Invoke(app);
        using (app.Services.GetRequiredService<ICallerContextAccessor>().EnterService("host"))   // as serve starts it
        {
            await using var scope = app.Services.CreateAsyncScope();
            _ = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>()
                .Handle(new StartProcessRequest(), ct)).Value;
        }

        app.Services.GetRequiredService<HeldHumanToken>().Hold(Token);
        await app.StartAsync(ct);
        Http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };
    }

    private string? Environment(string name) => name switch
    {
        "AW_SINDRI_HOME" => Home,
        "HOME" => Path.Combine(Home, "provider-home"),            // doctor's provider files: never the real ones
        "AW_SINDRI_CLAUDE_BIN" => Path.Combine(Home, "no-claude"),  // mcp register --apply: never the real claude
        "TMPDIR" when _elsewhere is not null => Path.Combine(_elsewhere.Path, "tmp"),
        "AW_STATE_DIR" when _elsewhere is not null => Path.Combine(_elsewhere.Path, "state"),
        _ => _environment is not null && _environment.TryGetValue(name, out var value) ? value : null,
    };
}
