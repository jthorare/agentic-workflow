using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>
/// Once every verb is open to agent sessions (PR 2 of the agent-first spec), the ledger's actor is the only record of who did a write. This sweeps the
/// command catalog as an owner-started session and checks that nothing a call wrote carries another actor than 'session:...' (or the owner's 'human:...').
/// A service that appends through anything but EventFactory records the scope's 'host:sindri' and shows up here.
/// </summary>
public sealed class WriteAttributionTests
{
    // Rows whose route starts a process, a session, a network call or a stop of the host: a sweep with an empty body must not reach them.
    // The list is a plain allow-nothing filter on the route's text; shrink it as a row proves safe to sweep.
    private static readonly string[] _notSwept =
    [
        "/stop", "/launch", "/sandbox", "/mcp", "/sessions", "/index", "/services", "/jobs", "/forge", "/review", "/linear", "/library", "/context",
        "/connections", "/setup", "/repos", "/profile", "/recipes", "/runs", "/wrap", "/events",
    ];

    [Fact(Timeout = 300_000), Trait("Lane", "integration")]
    public async Task Every_write_event_records_owner_or_session_as_its_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct);
        var ledger = host.App.Services.GetRequiredService<ILedgerStore>();
        var before = (await ledger.ReadAfter(0, 100_000, ct)).Value.Count;
        (string Name, string Value)[] session = [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli")];

        var swept = 0;
        foreach (var row in host.App.Services.GetRequiredService<ICommandCatalog>().Rows.Where(IsSweptWrite))
        {
            var route = "/sindri" + string.Join('/', row.Route!.Split('/').Select(static seg => seg.StartsWith('{') ? "x" : seg));
            using var request = new HttpRequestMessage(new HttpMethod(row.Method!), route)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            };
            foreach (var (name, value) in session)
            {
                _ = request.Headers.TryAddWithoutValidation(name, value);
            }

            using var response = await host.Http.SendAsync(request, ct);
            swept++;
        }

        var written = (await ledger.ReadAfter(0, 100_000, ct)).Value.Skip(before).ToList();
        var strays = written.Where(e => !IsOwnerOrSession(e.Envelope.Actor)).Select(e => $"{e.Envelope.Type} by {e.Envelope.Actor}").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.True(swept > 0, "the sweep reached no catalog row");
        Assert.True(strays.Count == 0, "these events were written during a session's calls without the session as actor:\n" + string.Join('\n', strays));
    }

    private static bool IsSweptWrite(CommandRow row) =>
        (string.Equals(row.Method, "POST", StringComparison.Ordinal) || string.Equals(row.Method, "PUT", StringComparison.Ordinal) || string.Equals(row.Method, "DELETE", StringComparison.Ordinal) || string.Equals(row.Method, "PATCH", StringComparison.Ordinal))
        && row.Route is { } route
        && row.Class is not (ActionClass.Local or ActionClass.StepReport)
        && !_notSwept.Any(skip => route.Contains(skip, StringComparison.Ordinal));

    private static bool IsOwnerOrSession(string actor) =>
        actor.StartsWith("session:", StringComparison.Ordinal) || actor.StartsWith("human:", StringComparison.Ordinal);
}
