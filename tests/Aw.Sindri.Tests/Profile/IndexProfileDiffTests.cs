using System.Text.Json;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>'profile approve' shows the owned index's keys by path (owned index spec §5.8, Uprof): the diff lists each one written, and changing a value is a change.</summary>
public sealed class IndexProfileDiffTests
{
    private static SindriProfile With(string contextJson) =>
        new(1, "thor", Context: JsonDocument.Parse(contextJson).RootElement.Clone());

    private const string Written = """{"sources":{"github":{"repos":["acme/web"],"host":"ghe.acme.com"},"slack":{"channels":[{"id":"C0123ABC","name":"eng"}]},"linear":{"teams":["ENG"]}},"index":{"windowDays":90,"maxChunks":1000,"retainDays":365,"floors":{"keepDistance":0.4},"acknowledgeOnyxLeftover":true}}""";

    private static void Lists(IReadOnlyList<string> paths, string key) => Assert.Contains(paths, p => string.Equals(p, key, StringComparison.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public void A_first_approval_lists_every_owned_index_key_written()
    {
        var paths = ProfileDiff.Between(approved: null, With(Written));

        Lists(paths, "context.sources.github.repos[0]");
        Lists(paths, "context.sources.github.host");
        Lists(paths, "context.sources.slack.channels[0].id");
        Lists(paths, "context.sources.slack.channels[0].name");
        Lists(paths, "context.sources.linear.teams[0]");
        Lists(paths, "context.index.windowDays");
        Lists(paths, "context.index.maxChunks");
        Lists(paths, "context.index.retainDays");
        Lists(paths, "context.index.floors.keepDistance");
        Lists(paths, "context.index.acknowledgeOnyxLeftover");
    }

    [Fact, Trait("Lane", "unit")]
    public void Changing_the_window_or_adding_a_repo_is_a_change_and_the_same_profile_is_not()
    {
        var approved = With(Written);
        var wider = With(Written.Replace("\"windowDays\":90", "\"windowDays\":365", StringComparison.Ordinal));
        var moreRepos = With(Written.Replace("[\"acme/web\"]", "[\"acme/web\",\"acme/api\"]", StringComparison.Ordinal));

        Assert.Equal(["context.index.windowDays"], ProfileDiff.Between(approved, wider));
        Assert.Equal(["context.sources.github.repos[1]"], ProfileDiff.Between(approved, moreRepos));
        Assert.Empty(ProfileDiff.Between(approved, With(Written)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_diff_lists_paths_only_so_a_token_pointer_is_never_shown()
    {
        var paths = ProfileDiff.Between(approved: null, With("""{"sources":{"github":{"token":"env:GH_POINTER_NAME"}}}"""));

        Lists(paths, "context.sources.github.token");
        Assert.DoesNotContain(paths, static p => p.Contains("GH_POINTER_NAME", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Changing_the_doc_directory_names_is_a_change_shown_by_key_path()
    {
        var approved = With("""{"docs":{"directoryNames":["AGENTS.md"]}}""");
        var changed = With("""{"docs":{"directoryNames":["AGENTS.md","NOTES.md"]}}""");

        Lists(ProfileDiff.Between(approved: null, approved), "context.docs.directoryNames[0]");
        Assert.Equal(["context.docs.directoryNames[1]"], ProfileDiff.Between(approved, changed));
    }
}
