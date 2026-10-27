using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.ApproveProfile;

/// <summary>
/// The bug check for issue 1147: 'profile approve' said "changed since the last approval: nothing" while the hash changed, because the diff lists a fixed set of keys and the hash covers
/// more (the index keys on a repo and at the top level, context, setup). Every change to a key the canonical JSON carries must show in the diff.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class DiffCoversHashedKeysCheckTests
{
    private static readonly SindriProfile _approved = new(1, "thor", Repos: [new RepoEntry("web", "/work/web")]);

    private static string Hash(SindriProfile profile) =>
        ProfileHashing.Of(ProfileReading.Canonical(profile, ProfileFiles.Options)).Value;

    private static SindriProfile WithRepoIndex(IndexSettings index) => _approved with { Repos = [new RepoEntry("web", "/work/web", index)] };

    [Fact]
    public void Adding_index_roles_and_demote_to_a_repo_shows_both_in_the_diff()
    {
        var live = WithRepoIndex(new IndexSettings(
            Roles: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["test"] = ["**/*.test.ts"] },
            Demote: ["test"]));

        var changes = ProfileDiff.Between(_approved, live);

        Assert.Contains(changes, static c => c.Contains("roles", StringComparison.Ordinal));
        Assert.Contains(changes, static c => c.Contains("demote", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_change_to_a_key_the_hash_covers_changes_the_diff()
    {
        using var context = JsonDocument.Parse("""{"repos":{"web":{"note":"x"}}}""");
        var changed = new (string Key, SindriProfile Live)[]
        {
            ("repo index.denyPaths", WithRepoIndex(new IndexSettings(DenyPaths: ["private/**"]))),
            ("repo index.ignore", WithRepoIndex(new IndexSettings(Ignore: ["demo"]))),
            ("repo index.maxFileKB", WithRepoIndex(new IndexSettings(MaxFileKB: 8))),
            ("repo index.embeddings", WithRepoIndex(new IndexSettings(Embeddings: new EmbeddingSettings(Model: "m")))),
            ("repo index.graph", WithRepoIndex(new IndexSettings(Graph: new GraphSettings(MaxMB: 2)))),
            ("index.denyPaths", _approved with { Index = new IndexSettings(DenyPaths: ["private/**"]) }),
            ("index.utilityGlobs", _approved with { Index = new IndexSettings(UtilityGlobs: ["**/util/**"]) }),
            ("index.maxTotalMB", _approved with { Index = new IndexSettings(MaxTotalMB: 9) }),
            ("setup.mcp", _approved with { Setup = new SetupSettings(SetupSettings.Off) }),
            ("workflow.scoping.historyWindow", _approved with { Workflow = new WorkflowSettings(Scoping: new ScopingSettings(new HistoryWindowSettings(Since: "2026-01-01"))) }),
            ("context", _approved with { Context = context.RootElement.Clone() }),
        };

        foreach (var (key, live) in changed)
        {
            Assert.True(Hash(live) != Hash(_approved), $"{key}: the premise is that the hash changes");
            Assert.True(ProfileDiff.Between(_approved, live).Count > 0, $"{key} changes the hash and the diff says nothing");
        }
    }
}
