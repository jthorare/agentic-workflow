using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P1's response bodies per route and scenario against their golden files (UI spec E-17, plan P1 unit E). Each row
/// starts its own <see cref="GoldenHost"/>, so a row's answer never depends on which rows ran before it.
/// </summary>
public sealed class ApiGoldenTests
{
    // hand-written before their routes (#2076): the PR page lands in Ph5c, the Library roots writes in their own slot and the review settings in Ph9's api slot, each registering its rows and leaving this list
    private static readonly string[] _pendingGoldenFolders = ["reviews-pr", "library-roots", "review-settings", "review-settings-save"];

    [Fact, Trait("Lane", "integration")]
    public void Every_golden_file_has_a_scenario()
    {
        var root = Golden.SourcePath(typeof(ApiGoldenTests).Assembly, "Golden/Api");
        var all = Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();
        var files = all
            .Where(f => !_pendingGoldenFolders.Any(p => f.StartsWith(p + "/", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal);
        var expected = ApiScenarios.All.Concat(WorkflowViewGoldenTests.All).Concat(ServedWorkflowRoutesTests.Goldens).Select(r => $"{r.Slug}/{r.Scenario}.json").Order(StringComparer.Ordinal);

        Assert.Equal(expected, files);

        // the pending files are exactly the rows not yet registered
        var pendingRows = ((IEnumerable<(string Slug, string Scenario)>)[.. ReviewScenarios.All.Where(r => r.Slug == "reviews-pr"), .. LibraryRootScenarios.All, .. ReviewSettingsScenarios.All]).Select(r => $"{r.Slug}/{r.Scenario}.json").Order(StringComparer.Ordinal);
        Assert.Equal(pendingRows, all.Where(f => _pendingGoldenFolders.Any(p => f.StartsWith(p + "/", StringComparison.Ordinal))).Order(StringComparer.Ordinal));
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
