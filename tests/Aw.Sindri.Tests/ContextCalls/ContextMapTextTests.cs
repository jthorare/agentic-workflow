using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The words of `context map status`: a session's counts are its repo's scope, and the line says so, so they aren't read as the version's (doctor counts the whole map).</summary>
public sealed class ContextMapTextTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");

    private static MapStatusView View(ContextRepoName? scopedTo) =>
        new(new MapVersion(1, _repo, Commit: null, KeyVersion: 1, DateTimeOffset.UnixEpoch, MapVersionStatus.Current), new MapCounts(13, 8, 0, 0, 0), Orphaned: 0, Retained: 0, scopedTo);

    [Fact, Trait("Lane", "unit")]
    public void A_scoped_status_says_its_counts_are_the_repos_scope_and_not_the_whole_map()
    {
        var text = ContextMapText.Status(View(_repo));

        var counts = text.Split('\n')[1];
        Assert.StartsWith("13 nodes, 8 edges, 0 aliases, 0 decisions, 0 overrides in aw-split-panes's scope", counts, StringComparison.Ordinal);
        Assert.Contains("not the whole map", counts, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unscoped_status_counts_the_version_without_a_scope_note()
    {
        var text = ContextMapText.Status(View(scopedTo: null));

        Assert.Contains("13 nodes, 8 edges, 0 aliases, 0 decisions, 0 overrides; 0 orphaned overrides", text, StringComparison.Ordinal);
        Assert.DoesNotContain("'s scope", text, StringComparison.Ordinal);
        Assert.DoesNotContain("scoped to", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_status_names_every_mapped_repo_with_its_short_commit_not_only_the_first()
    {
        var view = View(scopedTo: null) with { Heads = [new MapHead(_repo, "84c0e62aa", OnDefault: true), new MapHead(ContextRepoName.From("internal-apps"), "d692bd2", OnDefault: false)] };

        var text = ContextMapText.Status(view);

        Assert.Contains("\nrepos: aw-split-panes @84c0e62, internal-apps @d692bd2 (not on its default branch)\n", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_status_with_no_heads_has_no_repos_line() =>
        Assert.DoesNotContain("repos:", ContextMapText.Status(View(scopedTo: null)), StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void The_publish_state_reads_in_the_specs_short_form_then_Sindris_sentences_indented()
    {
        var view = new MapPublishStatusView("published-stale", "published, code answers stale", "TimedOut", Stale: true, [new StaleRepo("aw-split-panes", "abcdef0", "1234567")], 14, Until: null,
            ["Code answers are from abcdef0 (older than map v14, which is for 1234567)."]);

        var text = ContextMapText.Publish(view);

        Assert.Equal("publish: published stale: v14 (reason timed-out, aw-split-panes stale)\n  Code answers are from abcdef0 (older than map v14, which is for 1234567).\n", text);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_json_carries_the_map_status_and_the_publish_fields_with_its_version_apart()
    {
        var status = """{"version":{"number":13},"counts":{"nodes":1}}"""u8.ToArray();
        var publish = """{"state":"held","word":"held for the code index","reason":null,"stale":false,"staleRepos":[],"version":14,"until":null,"lines":["Map v14 is held; none of your repos are waiting."]}"""u8.ToArray();

        using var merged = System.Text.Json.JsonDocument.Parse(ContextMapText.WithPublish(status, publish));

        var root = merged.RootElement;
        Assert.Equal(13, root.GetProperty("version").GetProperty("number").GetInt32());
        Assert.Equal(("held", false, 14), (root.GetProperty("state").GetString(), root.GetProperty("stale").GetBoolean(), root.GetProperty("publishedVersion").GetInt32()));
        Assert.Equal(JsonValueKindNull, root.GetProperty("reason").ValueKind);
    }

    private const System.Text.Json.JsonValueKind JsonValueKindNull = System.Text.Json.JsonValueKind.Null;
}
