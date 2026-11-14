using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P1's response bodies per route and scenario against their golden files (UI spec E-17, plan P1 unit E). Each row
/// starts its own <see cref="GoldenHost"/>, so a row's answer never depends on which rows ran before it.
/// </summary>
public sealed class ApiGoldenTests
{
    // hand-written before their routes (#2076): the Library roots writes in their own slot and the review settings in Ph9's api slot, each registering its rows and leaving this list;
    // the Linear hub's rows (#2077 Ph3) all left it when api served them;
    // the todo goldens (#2264 Ph1a) left it when api served GET /sindri/todos; todos-note left it when todo Ph3b served the note routes (#2418)
    private static readonly string[] _pendingGoldenFolders = ["library-roots", "review-settings", "review-settings-save"];

    private static bool IsPending(string file) => _pendingGoldenFolders.Any(p => file.StartsWith(p + "/", StringComparison.Ordinal));

    [Fact, Trait("Lane", "integration")]
    public void Every_golden_file_has_a_scenario()
    {
        var root = Golden.SourcePath(typeof(ApiGoldenTests).Assembly, "Golden/Api");
        var all = Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();
        var files = all
            .Where(f => !IsPending(f))
            .Order(StringComparer.Ordinal);
        var expected = ApiScenarios.All.Select(r => $"{r.Slug}/{r.Scenario}.json").Order(StringComparer.Ordinal);

        Assert.Equal(expected, files);

        // the pending files are exactly the rows not yet registered
        var pendingRows = ((IEnumerable<(string Slug, string Scenario)>)[.. LibraryRootScenarios.All, .. ReviewSettingsScenarios.All]).Select(r => $"{r.Slug}/{r.Scenario}.json").Order(StringComparer.Ordinal);
        Assert.Equal(pendingRows, all.Where(IsPending).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(ApiScenarios.All, r => _pendingGoldenFolders.Contains(r.Slug));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_scenario_group_has_its_golden_test_class_and_every_class_its_group()
    {
        const string suffix = "ApiGoldenTests";
        var classes = typeof(ApiGoldenTests).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(ApiGoldenTests).Namespace && t.Name.EndsWith(suffix, StringComparison.Ordinal) && t != typeof(ApiGoldenTests))
            .Select(t => t.Name[..^suffix.Length])
            .Order(StringComparer.Ordinal);

        Assert.Equal(ApiScenarios.Groups.Keys.Order(StringComparer.Ordinal), classes);
    }

    /// <summary>
    /// Scenarios that are about the human's own credentials (no token, a stale one, the prover label, an app origin): a session has none of these, so they
    /// have no session reading.
    /// </summary>
    private static bool IsHumanCredentialRow((string Slug, string Scenario) row) =>
        row.Scenario.StartsWith("human-", StringComparison.Ordinal)
        || row.Scenario.StartsWith("prover-", StringComparison.Ordinal)
        || row.Scenario.StartsWith("unknown-caller", StringComparison.Ordinal)
        || row.Scenario.StartsWith("refused-origin", StringComparison.Ordinal);

    /// <summary>
    /// Every Golden/Api case with its human headers swapped for an owner-started session's (PR 2 of the agent-first spec: every verb is open to sessions).
    /// A session's answer may differ from the human's golden (text is fenced, writes name the session), so this checks the one thing PR 2 promises:
    /// no case answers SND-AUTH-002 as a session unless its human golden is that refusal too. It only reads goldens, so AW_GOLDEN_UPDATE never writes one.
    /// </summary>
    internal const int SessionShards = 20;   // #2534: one test over every row took 147 s on CI (315 s on a loaded box); 10 shards still took 65-69 s each under the full integration lane

    internal static List<(string Slug, string Scenario)> SessionRows() =>
        [.. ApiScenarios.All.Where(r => !IsHumanCredentialRow(r) && !IsPending(r.Slug + "/") && !IsStillOwnerOnly(r.Slug))];

    [Fact, Trait("Lane", "unit")]
    public void The_session_shards_partition_the_session_rows_exactly_once()
    {
        var all = SessionRows();
        var union = Enumerable.Range(0, SessionShards).SelectMany(s => ApiGoldenRows.Slice(all, s, SessionShards)).ToList();

        Assert.NotEmpty(all);
        Assert.Equal(all.Count, union.Count);
        Assert.Equal(all.Order(), union.Order());
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_session_shard_has_its_class()
    {
        var shards = typeof(ApiGoldenTests).Assembly.GetTypes().Select(static t => t.Name).Where(static n => n.StartsWith("SessionGoldenShard", StringComparison.Ordinal)).Order(StringComparer.Ordinal);

        Assert.Equal(Enumerable.Range(0, SessionShards).Select(static i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"SessionGoldenShard{i}Tests")).Order(StringComparer.Ordinal), shards);   // why: a shard with no class would skip its rows silently
    }

    internal static async Task RunSessionShard(int shard)
    {
        var ct = TestContext.Current.CancellationToken;
        var rows = ApiGoldenRows.Slice(SessionRows(), shard, SessionShards);
        Assert.NotEmpty(rows);
        var refused = new System.Collections.Concurrent.ConcurrentBag<string>();
        var broken = new System.Collections.Concurrent.ConcurrentBag<string>();

        // why: each case starts its own host with file watchers; two at a time keeps the run under the box's inotify limit (128 per user)
        await Parallel.ForEachAsync(rows, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = ct }, async (row, token) =>
        {
            // why: the scenario's setup (seeding, map imports) stays the human's; only the call the golden records goes out as the session
            await using var host = await ApiScenarios.Start(row.Slug, row.Scenario, token);
            string body;
            try
            {
                using (Callers.CallerRequests.AsSession())   // an AsyncLocal: each row's own flow carries it
                {
                    body = await ApiScenarios.Body(host, row.Slug, row.Scenario, token);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)   // why: one run lists every broken case, not just the first
            {
                broken.Add($"{row.Slug}/{row.Scenario}: {e.GetType().Name}: {e.Message.ReplaceLineEndings(" ")}");
                return;
            }

            if (body.Contains("SND-AUTH-002", StringComparison.Ordinal) && !HumanGolden(row).Contains("SND-AUTH-002", StringComparison.Ordinal))
            {
                refused.Add($"{row.Slug}/{row.Scenario}");
            }
        });

        Assert.True(refused.IsEmpty && broken.IsEmpty,
            "these cases refuse a session with SND-AUTH-002 but not the human:\n" + string.Join('\n', refused.Order(StringComparer.Ordinal))
            + "\nthese cases threw as a session:\n" + string.Join('\n', broken.Order(StringComparer.Ordinal)));
    }

    // why: the profile and settings writes stay the owner's until the settings PR (#2374) removes that refusal, and the Linear hub's connection
    // writes are a connection setting outside PR 2's list (LinearConnectionRules); both are named in PR 2's body as follow-ups
    // why: sessions-start and sessions-worktree are the person's by the agents spec (A7, #3380): a session gets SND-AUTH-002 before validation, so its answer can't match the human's refusal goldens
    private static bool IsStillOwnerOnly(string slug) =>
        slug.StartsWith("profile", StringComparison.Ordinal) || slug.StartsWith("linear-connect", StringComparison.Ordinal) || slug.StartsWith("linear-disconnect", StringComparison.Ordinal)
        || string.Equals(slug, "workflows-slug-activate", StringComparison.Ordinal) || string.Equals(slug, "workflows-slug-pause", StringComparison.Ordinal)   // a session is refused (#3199)
        || string.Equals(slug, "sessions-start", StringComparison.Ordinal) || string.Equals(slug, "sessions-worktree", StringComparison.Ordinal) || string.Equals(slug, "sessions-id-id-stop", StringComparison.Ordinal);

    private static string HumanGolden((string Slug, string Scenario) row) =>
        File.ReadAllText(Golden.SourcePath(typeof(ApiGoldenTests).Assembly, ApiGolden.PathOf(row.Slug, row.Scenario)));

    [Theory, Trait("Lane", "unit")]
    [InlineData("/sindri/status", "status")]
    [InlineData("/sindri/profile/approve", "profile-approve")]
    [InlineData("/sindri/items/{itemId}", "items-id")]
    [InlineData("/sindri/items/{id:long}", "items-id")]
    [InlineData("/sindri/workflow/instances/{instanceId}/nonce", "workflow-instances-id-nonce")]
    public void Route_slug_is_the_path_after_sindri_with_slashes_as_dashes_and_each_parameter_as_id(string route, string slug) =>
        Assert.Equal(slug, ApiGolden.Slug(route));
}
