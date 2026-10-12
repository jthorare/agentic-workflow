using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel.Testing;

namespace Aw.Context.Testing.Tests;

/// <summary>The recordings agree with each other and with the spec's fixture definition (§10), and stay synthetic.</summary>
[Lane(TestLane.Unit)]
public sealed partial class FixtureDataTests
{
    [Fact]
    public void The_linear_project_has_one_prd_six_issues_and_comments_on_every_issue()
    {
        _ = Assert.Single(Nodes("linear/projects.json", "projects"));
        var document = Assert.Single(Nodes("linear/documents.json", "documents"));
        Assert.StartsWith("PRD:", (string?)document["title"], StringComparison.Ordinal);
        var issues = Nodes("linear/issues.json", "issues");
        Assert.Equal(6, issues.Count);
        Assert.All(issues, i => Assert.NotEmpty(i["comments"]!["nodes"]!.AsArray()));
        Assert.Equal(2, Nodes("linear/project-updates.json", "projectUpdates").Count);
    }

    [Fact]
    public void Two_current_decisions_and_one_superseded_one_live_in_issue_comments()
    {
        var decisions = Nodes("linear/issues.json", "issues")
            .SelectMany(i => i["comments"]!["nodes"]!.AsArray())
            .Select(c => (string)c!["body"]!)
            .Where(b => b.StartsWith("Decision", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, decisions.Count);
        _ = Assert.Single(decisions, d => d.StartsWith("Decision (replaces", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_merged_pull_request_is_attached_to_its_issue_and_the_unmerged_issue_has_none()
    {
        var issues = Nodes("linear/issues.json", "issues").ToDictionary(i => (string)i["identifier"]!, StringComparer.Ordinal);

        foreach (var pr in SplitPanesFixture.History.PullRequests)
        {
            var attachment = Assert.Single(issues[pr.IssueKey]["attachments"]!["nodes"]!.AsArray());
            Assert.EndsWith(string.Create(CultureInfo.InvariantCulture, $"/pull/{pr.Number}"), (string?)attachment!["url"], StringComparison.Ordinal);
        }

        Assert.Empty(issues["SPL-106"]["attachments"]!["nodes"]!.AsArray());
    }

    [Fact]
    public void Onyx_documents_key_linear_issues_by_uuid_with_the_identifier_in_metadata()
    {
        var issues = Nodes("linear/issues.json", "issues");
        var linearDocs = OnyxDocs().Where(d => (string?)d["search_doc"]!["source_type"] == "linear").ToList();

        Assert.Equal(issues.Count, linearDocs.Count);
        foreach (var issue in issues)
        {
            var doc = Assert.Single(linearDocs, d => (string?)d["search_doc"]!["document_id"] == (string?)issue["id"])["search_doc"]!;
            Assert.Equal((string?)issue["identifier"], (string?)doc["metadata"]!["identifier"]);
            var chunks = linearDocs.Single(d => (string?)d["search_doc"]!["document_id"] == (string?)issue["id"])["chunks"]!.AsArray().Select(c => (string?)c);
            Assert.Equal(issue["comments"]!["nodes"]!.AsArray().Select(c => (string?)c!["body"]).Prepend((string?)issue["description"]), chunks, StringComparer.Ordinal);
            Assert.Equal(DateTimeOffset.Parse((string)issue["updatedAt"]!, CultureInfo.InvariantCulture),
                DateTimeOffset.Parse((string)doc["updated_at"]!, CultureInfo.InvariantCulture));
        }

        var slack = Assert.Single(OnyxDocs(), d => (string?)d["search_doc"]!["source_type"] == "slack")["search_doc"]!;
        var ts = long.Parse(((string)slack["document_id"]!).Split("__")[1].Split('.')[0], CultureInfo.InvariantCulture);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(ts), At(slack["updated_at"]));
    }

    [Fact]
    public void Probes_name_records_and_files_that_exist_in_the_fixture()
    {
        var records = Nodes("linear/issues.json", "issues").Select(i => $"linear:issue/{i["id"]}")
            .Concat(Nodes("linear/documents.json", "documents").Select(d => $"linear:doc/{d["id"]}"))
            .Append("slack:C0FIXTURE01/1784972400.000100")
            .ToHashSet(StringComparer.Ordinal);
        var files = SplitPanesFixture.ResourcePaths.Where(p => p.StartsWith("app/", StringComparison.Ordinal))
            .Select(p => p[(p.IndexOf('/', 4) + 1)..]).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(8, SplitPanesFixture.Probes.Count);
        foreach (var probe in SplitPanesFixture.Probes)
        {
            Assert.All(probe.Expect.Records, r => Assert.Contains(r, records));
            Assert.All(probe.Expect.Files, f => Assert.Contains(f, files));
            Assert.All(probe.Expect.Decisions, d => Assert.Contains(d.Quote, IssueText(d.Record), StringComparison.Ordinal));
        }
    }

    [Fact]
    public void The_conflicting_decision_disagrees_with_the_code_and_the_superseded_one_matches_it()
    {
        var probe = Assert.Single(SplitPanesFixture.Probes, p => p.Id == "minimum-pane-width");
        var current = Assert.Single(probe.Expect.Decisions, d => d.Status == "current");
        var resize = SplitPanesFixture.ReadText("app/05-spl-105/src/layout/PaneResize.ts");

        Assert.Equal("src/layout/PaneResize.ts", current.ConflictsWith);
        Assert.Contains("20 columns", current.Quote, StringComparison.Ordinal);
        Assert.Contains("MIN_PANE_COLUMNS = 10;", resize, StringComparison.Ordinal);
        var superseded = Assert.Single(probe.Expect.Decisions, d => d.Status == "superseded");
        Assert.Contains("10 columns", superseded.Quote, StringComparison.Ordinal);
        Assert.Null(superseded.ConflictsWith);
        var merged = SplitPanesFixture.History.PullRequests.Single(p => p.IssueKey == "SPL-105").MergedAt;
        var comments = Nodes("linear/issues.json", "issues").Single(i => (string?)i["identifier"] == "SPL-105")["comments"]!["nodes"]!.AsArray();
        Assert.True(At(comments.Single(c => ((string)c!["body"]!).Contains(superseded.Quote, StringComparison.Ordinal))!["createdAt"]) < merged);
        Assert.True(At(comments.Single(c => ((string)c!["body"]!).Contains(current.Quote, StringComparison.Ordinal))!["createdAt"]) > merged);
    }

    [Fact]
    public void Everything_is_synthetic_ids_emails_and_hosts()
    {
        foreach (var path in SplitPanesFixture.ResourcePaths)
        {
            var text = SplitPanesFixture.ReadText(path);
            Assert.All(Uuid.Matches(text), m => Assert.StartsWith("0e5a1b00-0000-4000-8000-", m.Value, StringComparison.Ordinal));
            Assert.All(Email.Matches(text), m => Assert.EndsWith("@fixture-labs.invalid", m.Value, StringComparison.Ordinal));
            Assert.All(Url.Matches(text), m => Assert.Contains("fixture-labs", m.Value, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ReadText_names_a_missing_path()
    {
        var error = Assert.Throws<ArgumentException>(() => SplitPanesFixture.ReadText("linear/nope.json"));

        Assert.Contains("linear/nope.json", error.Message, StringComparison.Ordinal);
    }

    private static List<JsonNode> Nodes(string path, string field) =>
        [.. JsonNode.Parse(SplitPanesFixture.ReadText(path))!["data"]![field]!["nodes"]!.AsArray().Select(n => n!)];

    private static DateTimeOffset At(JsonNode? value) => DateTimeOffset.Parse((string)value!, CultureInfo.InvariantCulture); // why: callers pass recorded date fields

    private static List<JsonNode> OnyxDocs() =>
        [.. JsonNode.Parse(SplitPanesFixture.ReadText("onyx/documents.json"))!["documents"]!.AsArray().Select(n => n!)];

    private static string IssueText(string record) =>
        Nodes("linear/issues.json", "issues").Single(i => $"linear:issue/{i["id"]}" == record).ToJsonString();

    [GeneratedRegex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Uuid { get; }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Email { get; }

    [GeneratedRegex(@"https?://[^\s""')]+", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Url { get; }
}
