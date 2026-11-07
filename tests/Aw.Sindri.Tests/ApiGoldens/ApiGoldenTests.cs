using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P1's response bodies per route and scenario against their golden files (UI spec E-17, plan P1 unit E). Each row
/// starts its own <see cref="GoldenHost"/>, so a row's answer never depends on which rows ran before it.
/// </summary>
public sealed class ApiGoldenTests
{
    // hand-written before their routes (#2076): the Library roots writes in their own slot and the review settings in Ph9's api slot, each registering its rows and leaving this list;
    // the Linear hub's reads (#2077 Ph3 1a) and writes (1b) leave it when api serves them (the edit route's linear-change is served)
    private static readonly string[] _pendingGoldenFolders = ["library-roots", "review-settings", "review-settings-save", "linear-comment", "linear-create", "linear-connection", "linear-connection-teams", "linear-connect", "linear-disconnect-write"];

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

        // the pending files are exactly the rows not yet registered
        var pendingRows = ((IEnumerable<(string Slug, string Scenario)>)[.. LibraryRootScenarios.All, .. ReviewSettingsScenarios.All, .. LinearScenarios.All]).Select(r => $"{r.Slug}/{r.Scenario}.json").Order(StringComparer.Ordinal);
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

    [Theory, Trait("Lane", "unit")]
    [InlineData("/sindri/status", "status")]
    [InlineData("/sindri/profile/approve", "profile-approve")]
    [InlineData("/sindri/items/{itemId}", "items-id")]
    [InlineData("/sindri/items/{id:long}", "items-id")]
    [InlineData("/sindri/workflow/instances/{instanceId}/nonce", "workflow-instances-id-nonce")]
    public void Route_slug_is_the_path_after_sindri_with_slashes_as_dashes_and_each_parameter_as_id(string route, string slug) =>
        Assert.Equal(slug, ApiGolden.Slug(route));
}
