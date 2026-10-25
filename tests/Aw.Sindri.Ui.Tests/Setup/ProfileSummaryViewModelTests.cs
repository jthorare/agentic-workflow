using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The approval's summary in plain words (setup-once spec §5.2, api #782), as the profile route's goldens serve it.</summary>
[Lane(TestLane.Unit)]
public sealed class ProfileSummaryViewModelTests
{
    private static ProfileSummaryViewModel Shown(string scenario)
    {
        var summary = new ProfileSummaryViewModel();
        summary.Show(AwJson.Deserialize<ProfileRead>(System.Text.Encoding.UTF8.GetBytes(GoldenFiles.Api("profile", scenario)), SindriApiClient.Json).Value.Summary);
        return summary;
    }

    private static ProfileSummaryView Served(IReadOnlyList<ProfileSummaryRepo>? repos = null, IReadOnlyList<string>? services = null, IReadOnlyList<string>? allowance = null,
        IReadOnlyList<string>? connections = null, IReadOnlyList<string>? humanOnly = null) => new(repos ?? [], services ?? [], allowance ?? [], connections ?? [], humanOnly ?? []);

    [Fact]
    public void A_changed_profile_lists_the_service_sentence_and_leaves_the_empty_groups_out()
    {
        var summary = Shown("changed");

        var group = Assert.Single(summary.Groups);
        Assert.Equal(("Services", false), (group.Heading, group.HasDetail));
        Assert.Equal(["Sindri will run the scorer service every 1 hour"], group.Lines, StringComparer.Ordinal);
        Assert.True(summary.HasSummary);
    }

    [Fact]
    public void An_added_repo_shows_its_name_path_and_size_over_its_sentences()
    {
        var summary = Shown("repo-added");

        var repo = Assert.Single(summary.Groups);
        Assert.Equal(("demo", "/work/demo, 1,234 files, 12.6 MB", true), (repo.Heading, repo.Detail, repo.HasDetail));
        Assert.Equal(3, repo.Lines.Count);
        Assert.StartsWith("Sindri will read /work/demo", repo.Lines[0], StringComparison.Ordinal);
        Assert.Equal("demo, /work/demo, 1,234 files, 12.6 MB", repo.Name);
    }

    [Fact]
    public void An_unapproved_profile_shows_only_what_the_profile_itself_sets()
    {
        var summary = Shown("unapproved");

        var group = Assert.Single(summary.Groups);
        Assert.Equal("Services", group.Heading);
        Assert.Equal(["Sindri will run the heartbeat service every 5 seconds"], group.Lines, StringComparer.Ordinal);
    }

    [Fact]
    public void A_profile_with_nothing_to_approve_shows_nothing()
    {
        var summary = Shown("approved");

        Assert.Empty(summary.Groups);
        Assert.False(summary.HasSummary);
    }

    [Fact]
    public void Groups_come_in_the_spec_order_repos_services_allowance_connections_kept_for_you()
    {
        var summary = new ProfileSummaryViewModel();

        summary.Show(Served(
            repos: [new ProfileSummaryRepo("a", "/a", 1, 0.1, ["r1"]), new ProfileSummaryRepo("b", "/b", 2, 0.2, [])],
            services: ["s1"],
            allowance: ["w1"],
            connections: ["c1"],
            humanOnly: ["h1"]));

        Assert.Equal(["a", "b", "Services", "What workflows may do", "Outside systems", "Waits for your answer"], summary.Groups.Select(g => g.Heading), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(1204L, 38.2, "/r, 1,204 files, 38.2 MB")]
    [InlineData(1L, 0.04, "/r, 1 file, 0.0 MB")]
    [InlineData(0L, 0.0, "/r, 0 files, 0.0 MB")]
    [InlineData(1234567L, 1024.0, "/r, 1,234,567 files, 1,024.0 MB")]
    public void A_repos_count_and_size_are_grouped_and_to_one_decimal(long files, double megabytes, string expected)
    {
        var summary = new ProfileSummaryViewModel();

        summary.Show(Served(repos: [new ProfileSummaryRepo("r", "/r", files, megabytes, [])]));

        Assert.Equal(expected, Assert.Single(summary.Groups).Detail);
    }

    [Fact]
    public void A_later_null_clears_what_was_shown()
    {
        var summary = Shown("repo-added");

        summary.Show(summary: null);

        Assert.Empty(summary.Groups);
        Assert.False(summary.HasSummary);
    }
}
