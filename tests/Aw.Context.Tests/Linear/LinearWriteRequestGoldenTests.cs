using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Linear;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.Context.Tests.Linear;

/// <summary>The exact request body (query and variables) each of Linear's five mutations is sent with, for a fixed input (Linear hub spec 2026-11-04 §5.2). A change to an operation file or to the variables shows here first.</summary>
[Trait("Lane", "unit")]
public sealed class LinearWriteRequestGoldenTests
{
    private static readonly JsonSerializerOptions _indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Body(LinearRequest request) => JsonNode.Parse(AwJson.Serialize(request, LinearWire.Options))?.ToJsonString(_indented) + "\n";

    private static void Matches(LinearRequest request, string file) => Golden.Match(Body(request), typeof(LinearWriteRequestGoldenTests).Assembly, "Linear/Golden/" + file, Normalization.None);

    private static ExternalId Id(string value) => ExternalId.From(value);

    private static LinearClientId ClientId(int n) => LinearClientId.Parse(string.Create(CultureInfo.InvariantCulture, $"0e5a1b00-0000-4000-8000-{n:D12}")).Value;

    [Fact]
    public void Creating_an_issue_with_every_field_matches_its_golden_file()
    {
        var draft = new LinearIssueDraft(
            Id("team-1"), "Fix the login timeout", "Why it times out", Id("state-1"), Id("user-1"), Priority: 2, Estimate: 3, CycleId: Id("cycle-1"), Id("project-1"), Id("issue-0"), [Id("label-1"), Id("label-2")]);

        Matches(LinearWriteRequests.CreateIssue(draft, ClientId(1)), "write-issue-create.json");
    }

    [Fact]
    public void Creating_an_issue_with_a_title_alone_sends_the_id_the_team_and_the_title() =>
        Matches(LinearWriteRequests.CreateIssue(new LinearIssueDraft(Id("team-1"), "Just a title"), ClientId(2)), "write-issue-create-title-only.json");

    [Fact]
    public void Updating_an_issue_sends_the_set_fields_and_a_cleared_one_as_null()
    {
        var change = new LinearIssueChange(
            Title: LinearFieldChange.Set(value: "Renamed"),
            Description: LinearFieldChange.Set<string?>(value: null),
            StateId: LinearFieldChange.Set(value: Id("state-2")),
            AssigneeId: LinearFieldChange.Set<ExternalId?>(value: null),
            Priority: LinearFieldChange.Set(value: 1),
            Estimate: LinearFieldChange.Set<double?>(value: 5),
            ProjectId: LinearFieldChange.Set<ExternalId?>(value: null),
            ParentId: LinearFieldChange.Set<ExternalId?>(value: Id("issue-0")),
            LabelIds: LinearFieldChange.Set<IReadOnlyList<ExternalId>>(value: []));

        Matches(LinearWriteRequests.UpdateIssue(Id("issue-1"), change), "write-issue-update.json");
    }

    [Fact]
    public void Commenting_matches_its_golden_file() =>
        Matches(LinearWriteRequests.CreateComment(Id("issue-1"), "Looks right to me", ClientId(3)), "write-comment-create.json");

    [Fact]
    public void Editing_a_comment_matches_its_golden_file() =>
        Matches(LinearWriteRequests.UpdateComment(Id("comment-1"), "Edited text"), "write-comment-update.json");

    [Fact]
    public void Linking_a_url_matches_its_golden_file() =>
        Matches(LinearWriteRequests.LinkUrl(Id("issue-1"), new Uri("https://github.com/jthorare/agentic-workflow/pull/2077"), "PR 2077"), "write-attachment-link.json");
}
