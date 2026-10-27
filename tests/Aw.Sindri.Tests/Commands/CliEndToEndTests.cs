using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflow.Cli;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// The CLI's verbs with their real caller headers against a real control plane on loopback (API spec §6, §4.2): a session
/// is refused by the command chain with the CLI's session note, and a human CLI's token passes the caller middleware.
/// </summary>
public sealed class CliEndToEndTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
        _home.Dispose();
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Human_cli_sends_the_token_file_and_passes_the_caller_middleware()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        var hash = (await app.Services.GetRequiredService<IProfileSource>().Load(ct)).Value.Hash;
        var stale = SliceHarness.Profile("9s").Hash;
        await using var scope = app.Services.CreateAsyncScope();
        using var client = Client(app, human: true);
        var verbs = Verbs(app, client, scope.ServiceProvider);

        var staleCode = await verbs.Approve(stale.Value, ct);
        var staleError = _error.ToString();
        var approved = await verbs.Approve(hash.Value, ct);

        Assert.Equal(2, staleCode);
        Assert.StartsWith("SND-PROFILE-003 ", staleError, StringComparison.Ordinal);   // the service answered: the middleware let the human in
        Assert.Equal(0, approved);
        var recorded = (await app.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ProfileApproved, ct)).Value;
        Assert.Equal("human:cli", recorded?.Envelope.Actor);
        await app.StopAsync(ct);
    }

    /// <summary>A control plane in the temp home with a profile, the lease held, this start's token minted to its file and held, and its endpoint recorded.</summary>
    private Task<WebApplication> Started(CancellationToken ct) => CliHost.Started(_home, ct);

    /// <summary>The CLI's client with the headers CommandTable gives it: the real token store, no repo.</summary>
    private static ControlPlaneClient Client(WebApplication app, bool human) =>
        new(app.Services.GetRequiredService<IControlPlaneProbe>(), SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), human ? app.Services.GetRequiredService<IHumanTokenStore>() : new InMemoryHumanTokenStore(), static _ => ValueTask.FromResult<Core.Ids.RepoName?>(null)));

    private Verbs Verbs(WebApplication app, ControlPlaneClient client, IServiceProvider scope) =>
        new(client, app.Services.GetRequiredService<IControlPlaneProbe>(), new Output(_out, _error, SliceHarness.Json), scope);
}
