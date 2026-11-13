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
            ("index.concepts dependsOn", _approved with { Index = new IndexSettings(Concepts: [new ConceptSettings(Name: "Billing", DependsOn: ["orders"])]) }),
            ("index.concepts aliases", _approved with { Index = new IndexSettings(Concepts: [new ConceptSettings(Name: "Billing", Aliases: ["payments"])]) }),
            ("index.concepts owns", _approved with { Index = new IndexSettings(Concepts: [new ConceptSettings(Name: "Billing", Owns: ["invoices"])]) }),
            ("index.pages action concepts", _approved with { Index = new IndexSettings(Pages: [new PageSettings(Name: "Schedule week", Actions: [new PageActionSettings(Name: "Submit", Concepts: ["scheduling"])])]) }),
            ("setup.mcp", _approved with { Setup = new SetupSettings(SetupSettings.Off) }),
            ("agents.idleOwedMinutes", _approved with { Agents = new AgentSettings(IdleOwedMinutes: 20) }),
            ("agents.slotWaitMinutes", _approved with { Agents = new AgentSettings(SlotWaitMinutes: 45) }),
            ("agents.page", _approved with { Agents = new AgentSettings(Page: AgentSettings.PageRows) }),
            ("setup.skipAccounts", _approved with { Setup = new SetupSettings { SkipAccounts = true } }),
            ("setup.skipFirstWorkflow", _approved with { Setup = new SetupSettings { SkipFirstWorkflow = true } }),
            ("setup.oauth.github", _approved with { Setup = new SetupSettings { Oauth = new SetupOauthSettings(Github: "Iv1.abc") } }),
            ("setup.oauth.linear", _approved with { Setup = new SetupSettings { Oauth = new SetupOauthSettings(Linear: "lin_abc") } }),
            ("review.login", _approved with { Review = new PrReviewSettings(Login: "octocat") }),
            ("review.settings checks need", _approved with { Review = new PrReviewSettings(Settings: new Dictionary<string, PrReviewSettingsEntry>(StringComparer.Ordinal) { ["api"] = new(Checks: [new PrReviewCheck("tests", "Tests", Need: new PrReviewNeed(Ci: "build"))]) }) }),
            ("review.projects", _approved with { Review = new PrReviewSettings(Projects: [new PrReviewProject("prj_1", Repos: ["acme/api"])]) }),
            ("todo.obsidian.vault", _approved with { Todo = new TodoSettings(new TodoObsidianSettings(Vault: "notes")) }),
            ("todo.obsidian.note", _approved with { Todo = new TodoSettings(new TodoObsidianSettings(Note: "Todo.md")) }),
            ("workflow.scoping.historyWindow", _approved with { Workflow = new WorkflowSettings(Scoping: new ScopingSettings(new HistoryWindowSettings(Since: "2026-01-01"))) }),
            ("workflow.poll.intervalSeconds", _approved with { Workflow = new WorkflowSettings(Poll: new PollSettings(IntervalSeconds: 60)) }),
            ("workflow.poll.catchUpPages", _approved with { Workflow = new WorkflowSettings(Poll: new PollSettings(CatchUpPages: 5)) }),
            ("workflow.http.hosts", _approved with { Workflow = new WorkflowSettings { Http = new HttpSettings(["api.example.com"]) } }),
            ("workflow.callDepth", _approved with { Workflow = new WorkflowSettings { CallDepth = 2 } }),
            ("workflow.editor", _approved with { Workflow = new WorkflowSettings { Editor = "steps" } }),
            ("workflow.forEach.maxItems", _approved with { Workflow = new WorkflowSettings(ForEach: new ForEachSettings(MaxItems: 50)) }),
            ("workflow.forEach.batchSize", _approved with { Workflow = new WorkflowSettings(ForEach: new ForEachSettings(BatchSize: 5)) }),
            ("context", _approved with { Context = context.RootElement.Clone() }),
        };

        Array.ForEach(changed, c => AssertDiffSees(c.Key, c.Live));
    }

    private static void AssertDiffSees(string key, SindriProfile live)
    {
        Assert.True(Hash(live) != Hash(_approved), $"{key}: the premise is that the hash changes");
        Assert.True(ProfileDiff.Between(_approved, live).Count > 0, $"{key} changes the hash and the diff says nothing");
    }
}
