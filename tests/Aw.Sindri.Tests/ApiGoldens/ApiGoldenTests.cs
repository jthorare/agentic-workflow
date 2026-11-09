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
    // the todo goldens (#2264 Ph1a) left it when api served GET /sindri/todos; todos-note leaves it when todo Ph3b serves the note routes
    private static readonly string[] _pendingGoldenFolders = ["library-roots", "review-settings", "review-settings-save", "todos-note"];

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
        var expected = ApiScenarios.All.Concat(WorkflowViewGoldenTests.All).Concat(ServedWorkflowRoutesTests.Goldens).Select(r => $"{r.Slug}/{r.Scenario}.json").Order(StringComparer.Ordinal);

        Assert.Equal(expected, files);

        // the pending files are exactly the rows not yet registered (todos-note: todo Ph3b, #2264)
        var pendingRows = ((IEnumerable<(string Slug, string Scenario)>)[.. LibraryRootScenarios.All, .. ReviewSettingsScenarios.All, ("todos-note", "note"), ("todos-note", "hold"), ("todos-note", "conflict")]).Select(r => $"{r.Slug}/{r.Scenario}.json").Order(StringComparer.Ordinal);
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
    /// A case whose answer differs only because it was a session is a gate PR 2 should have removed, or a golden that records the caller. Until the
    /// authorization PRs (A and B of PR 2) land, expect the rows they open to stay red.
    /// </summary>
    [Fact(Timeout = 600_000), Trait("Lane", "integration")]
    public async Task The_API_goldens_pass_when_run_as_a_session()
    {
        var ct = TestContext.Current.CancellationToken;
        var rows = ApiScenarios.All.Where(r => !IsHumanCredentialRow(r) && !IsPending(r.Slug + "/")).ToList();
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();

        await Parallel.ForEachAsync(rows, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (row, token) =>
        {
            using var session = Callers.CallerRequests.AsSession();   // an AsyncLocal: each row's own flow carries it
            try
            {
                await ApiGoldenRows.Match(row.Slug, row.Scenario, token);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                failures.Add($"{row.Slug}/{row.Scenario}: {e.GetType().Name}: {e.Message.Split('\n')[0]}");
            }
        });

        Assert.True(failures.IsEmpty, "these goldens differ when run as a session:\n" + string.Join('\n', failures.Order(StringComparer.Ordinal)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/sindri/status", "status")]
    [InlineData("/sindri/profile/approve", "profile-approve")]
    [InlineData("/sindri/items/{itemId}", "items-id")]
    [InlineData("/sindri/items/{id:long}", "items-id")]
    [InlineData("/sindri/workflow/instances/{instanceId}/nonce", "workflow-instances-id-nonce")]
    public void Route_slug_is_the_path_after_sindri_with_slashes_as_dashes_and_each_parameter_as_id(string route, string slug) =>
        Assert.Equal(slug, ApiGolden.Slug(route));
}
