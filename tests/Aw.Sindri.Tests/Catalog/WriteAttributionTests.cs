using System.Collections.Concurrent;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>
/// Once every verb is open to agent sessions (PR 2 of the agent-first spec), the ledger's actor is the only record of who did a write. For every write
/// route of the command catalog this runs the API golden scenarios of that route (their real bodies and seeding) as a session Sindri launched, then
/// reads the events the call appended: every one must carry 'session:launch:&lt;launch id&gt;', never the human's, the host's default 'host:sindri'
/// or an unlabelled 'session:...'; and each route must have appended at least one such event in some scenario. A write that records no event, or
/// records the wrong actor, fails here.
/// </summary>
public sealed class WriteAttributionTests
{
    private const string Launch = "LCH-0000000000000000000000000000ff01";

    // Write routes (slug, as ApiGolden.Slug names it) this check skips, each with the reason; only a route that writes no ledger event belongs here.
    private static readonly Dictionary<string, string> _notSwept = new(StringComparer.Ordinal)
    {
        ["stop"] = "stops the host, so the ledger can't be read after it; its actor is covered by the stop tests",
        ["profile-approve"] = "removed (410, #2258 PR 1): the route writes nothing",
        ["profile"] = "the profile write stays the owner's until the settings PR (#2374), and it writes the profile file, not a ledger event",
        ["linear-connect"] = "a connection setting outside PR 2's list (LinearConnectionRules): the owner's",
        ["linear-disconnect-write"] = "a connection setting outside PR 2's list (LinearConnectionRules): the owner's",
        ["views-rebuild"] = "replays the ledger into views.db: it appends no event",
    };

    [Fact(Timeout = 900_000), Trait("Lane", "integration")]
    public async Task Every_write_a_launched_session_makes_records_that_launch_as_its_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        var catalogRows = await CatalogWriteSlugs(ct);
        var scenarios = ApiScenarios.All.Where(r => catalogRows.Contains(r.Slug) && !_notSwept.ContainsKey(r.Slug) && !IsNotASessionCall(r)).ToList();
        var launched = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);   // slug -> a scenario appended an event by the launch
        var strays = new ConcurrentBag<string>();
        var broken = new ConcurrentBag<string>();

        // why: each case starts its own host with file watchers; two at a time keeps the run under the box's inotify limit (128 per user)
        await Parallel.ForEachAsync(scenarios, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = ct }, async (row, token) =>
        {
            await using var host = await ApiScenarios.Start(row.Slug, row.Scenario, token);
            var ledger = host.App.Services.GetRequiredService<ILedgerStore>();
            LaunchSecrets.Write(host.App.Services.GetRequiredService<LauncherOptions>().SecretsDirectory, LaunchId.From(Launch), "proof");
            var before = (await ledger.ReadAfter(0, 100_000, token)).Value.Count;
            try
            {
                using (Callers.CallerRequests.AsLaunchedSession(Launch))   // the scenario's own call, with the seeding it does through Human(), as the launched session
                {
                    _ = await ApiScenarios.Body(host, row.Slug, row.Scenario, token);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)   // why: one run lists every broken case, not just the first
            {
                broken.Add($"{row.Slug}/{row.Scenario}: {e.GetType().Name}: {e.Message.ReplaceLineEndings(" ")}");
                return;
            }

            var written = (await ledger.ReadAfter(0, 100_000, token)).Value.Skip(before).Select(e => e.Envelope).ToList();
            if (written.Any(e => IsByLaunch(e.Actor)))
            {
                launched[row.Slug] = true;
            }

            foreach (var envelope in written.Where(e => IsWrongActor(e.Actor)))
            {
                strays.Add($"{row.Slug}/{row.Scenario}: {envelope.Type} by {envelope.Actor}");
            }
        });

        var silent = scenarios.Select(r => r.Slug).Distinct(StringComparer.Ordinal).Where(slug => !launched.ContainsKey(slug)).Order(StringComparer.Ordinal).ToList();
        var noScenario = catalogRows.Where(slug => !_notSwept.ContainsKey(slug) && !ApiScenarios.All.Any(r => string.Equals(r.Slug, slug, StringComparison.Ordinal))).Order(StringComparer.Ordinal).ToList();

        Assert.True(scenarios.Count > 0, "the sweep reached no scenario");
        Assert.True(broken.IsEmpty, "these scenarios threw as a launched session:\n" + string.Join('\n', broken.Order(StringComparer.Ordinal)));
        Assert.True(strays.IsEmpty, $"these events were written during a launched session's calls without 'session:launch:{Launch}' as actor:\n" + string.Join('\n', strays.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));
        Assert.True(silent.Count == 0, "these write routes appended no event by the launched session in any scenario (no event, or another actor; a route that writes no event goes in _notSwept with its reason):\n" + string.Join('\n', silent));
        Assert.True(noScenario.Count == 0, "these catalog write routes have no API golden scenario to run (add one, or list the route in _notSwept with its reason):\n" + string.Join('\n', noScenario));
    }

    private static async Task<HashSet<string>> CatalogWriteSlugs(CancellationToken ct)
    {
        await using var host = await GoldenHost.Start(temporary: true, ct);
        return host.App.Services.GetRequiredService<ICommandCatalog>().Rows
            .Where(IsWrite)
            .Select(static row => ApiGolden.Slug("/sindri" + row.Route))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsWrite(CommandRow row) =>
        (string.Equals(row.Method, "POST", StringComparison.Ordinal) || string.Equals(row.Method, "PUT", StringComparison.Ordinal) || string.Equals(row.Method, "DELETE", StringComparison.Ordinal) || string.Equals(row.Method, "PATCH", StringComparison.Ordinal))
        && row.Route is { Length: > 1 }
        && row.Class is not (ActionClass.Local or ActionClass.StepReport);

    /// <summary>Scenarios about the human's own credentials or an app origin: a launched session has no such reading.</summary>
    private static bool IsNotASessionCall((string Slug, string Scenario) row) =>
        row.Scenario.StartsWith("human-", StringComparison.Ordinal)
        || row.Scenario.StartsWith("prover-", StringComparison.Ordinal)
        || row.Scenario.StartsWith("unknown-caller", StringComparison.Ordinal)
        || row.Scenario.StartsWith("refused-origin", StringComparison.Ordinal);

    private static bool IsByLaunch(string actor) => string.Equals(actor, "session:launch:" + Launch, StringComparison.Ordinal);

    /// <summary>A launched session's call must not leave the human's actor, the scope's default host actor, or a session actor without the launch.</summary>
    private static bool IsWrongActor(string actor) =>
        !IsByLaunch(actor)
        && (actor.StartsWith("human:", StringComparison.Ordinal) || actor.StartsWith("session:", StringComparison.Ordinal) || string.Equals(actor, "host:sindri", StringComparison.Ordinal));
}
