using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Extract;

public sealed class ProfileRecognizerExtractionTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");
    private static readonly ProfileRecognizer _flags = new("flag", "config/*.yaml", "rollout: (?<name>[a-z-]+)", "name");

    private static readonly RepoFile[] _files =
    [
        new("config/flags.yaml", "rollout: link-expiry\nrollout: dark-mode\n"),
        new("src/ids.ts", "export type CommandId = \"close-pane\";"),
    ];

    [Fact, Trait("Lane", "unit")]
    public void A_profile_recognizer_finds_a_suggested_surface_in_a_file_the_built_in_table_skips()
    {
        var found = MapExtractor.Extract(_repo, _files, RecognizerSet.From([_flags]));

        var node = found.Map.Nodes.Single(n => n.Key.Value == "surface:flag/link-expiry");
        Assert.Equal((Provenance.Deterministic, 0.8, "link-expiry"), (node.Provenance, node.Confidence, node.Label));
        Assert.Contains(found.Map.Nodes, n => n.Key.Value == "surface:flag/dark-mode");
        Assert.Contains(found.Map.Nodes, n => n.Key.Value == "surface:command/close-pane");   // the built-ins still run
        Assert.Contains(found.Map.Edges, e => e.From.Value == "code:config/flags.yaml" && e.To.Value == "surface:flag/link-expiry" && e.Type == EdgeType.Implements);
        Assert.False(found.TimedOut);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_the_recognizer_the_same_files_propose_no_flag()
    {
        Assert.DoesNotContain(MapExtractor.Extract(_repo, _files, RecognizerSet.None).Map.Nodes, n => n.Key.Value.StartsWith("surface:flag/", StringComparison.Ordinal));
        Assert.DoesNotContain(MapExtractor.Extract(_repo, _files).Nodes, n => n.Key.Value.StartsWith("surface:flag/", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_value_that_is_not_a_valid_key_is_skipped_not_a_failure()
    {
        var odd = new RepoFile("config/flags.yaml", "rollout: ok\nrollout: \nrollout: a\tb\n");
        var loose = new ProfileRecognizer("flag", "config/*.yaml", "rollout: (?<name>[^\\n]*)", "name");

        var found = MapExtractor.Extract(_repo, [odd], RecognizerSet.From([loose]));

        Assert.Equal(["surface:flag/ok"], found.Map.Nodes.Where(n => n.Key.Kind == NodeKind.Surface).Select(n => n.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pattern_that_runs_out_of_time_marks_the_extraction_timed_out_and_keeps_the_rest()
    {
        // A lookahead keeps this pattern on the backtracking engine (LinearRegex's fallback), the only path that can still time out.
        var catastrophic = new ProfileRecognizer("flag", "config/*.yaml", "^(?=a)(?<name>(a+)+)$", "name");
        var files = new[] { new RepoFile("config/flags.yaml", new string('a', 60) + "!"), _files[1] };

        var found = MapExtractor.Extract(_repo, files, RecognizerSet.From([catastrophic]));

        Assert.True(found.TimedOut);
        Assert.Contains(found.Map.Nodes, n => n.Key.Value == "surface:command/close-pane");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_set_wants_only_the_paths_its_globs_match()
    {
        var set = RecognizerSet.From([_flags]);

        Assert.True(set.Wants("config/flags.yaml"));
        Assert.False(set.Wants("src/config/flags.yaml"));
        Assert.False(RecognizerSet.None.Wants("config/flags.yaml"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_hash_is_empty_for_no_recognizers_and_otherwise_moves_with_their_content()
    {
        var other = new ProfileRecognizer("flag", "config/*.yaml", "toggle: (?<name>[a-z-]+)", "name");
        var same = new ProfileRecognizer("flag", "config/*.yaml", "rollout: (?<name>[a-z-]+)", "name");

        Assert.Equal("", RecognizerSet.None.Hash, StringComparer.Ordinal);
        Assert.Equal("", RecognizerSet.From([]).Hash, StringComparer.Ordinal);
        Assert.Matches("^r[0-9a-f]{8}$", RecognizerSet.From([_flags]).Hash);
        Assert.Equal(RecognizerSet.From([_flags]).Hash, RecognizerSet.From([same]).Hash, StringComparer.Ordinal);
        Assert.NotEqual(RecognizerSet.From([_flags]).Hash, RecognizerSet.From([other]).Hash, StringComparer.Ordinal);
    }
}
