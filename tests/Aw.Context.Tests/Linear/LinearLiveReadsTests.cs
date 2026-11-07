using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Linear;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.Linear;

/// <summary>The live reads (Linear hub spec 2026-11-04 D6) over the stateful fake Linear: one request per call, a missing record is null, a record outside the profile's teams is missing.</summary>
[Trait("Lane", "integration")]
public sealed class LinearLiveReadsTests : IAsyncLifetime
{
    private LinearHubWorld _world = null!;
    private ILinearLiveReads _reads = null!;
    private LinearSettings _settings = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _world = await LinearHubWorld.Start(seed => seed
            .Team("ENG").Team("OPS")
            .Member("Ada Fixture")
            .Label("bug")
            .Issue("ENG", "Fix the login timeout", new FakeLinearIssueSeed { Description = "Details", State = "In Progress", Assignee = "Ada Fixture", Priority = 2, Estimate = 3, Labels = ["bug"] })
            .Issue("OPS", "Rotate the keys")
            .Comment("ENG-1", "first comment"));
        _reads = _world.Provider.GetRequiredService<ILinearLiveReads>();
        _settings = LinearSettings.Create(_world.Linear.Url.ToString(), "env:" + LinearHubWorld.KeyName, ["ENG"]).Value;
    }

    public ValueTask DisposeAsync() => _world.DisposeAsync();

    [Fact]
    public async Task An_issue_is_read_by_key_with_the_fields_a_write_needs()
    {
        var issue = _world.Linear.Issues.Single(static i => i.Team == "ENG");

        var read = await _reads.Issue(_settings, "ENG-1", Ct);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : "");
        var view = read.Value!;
        Assert.Equal(issue.Id, view.Id.Value);
        Assert.Equal("ENG-1", view.Key.ToString());
        Assert.Equal("ENG", view.TeamKey);
        Assert.Equal("Fix the login timeout", view.Title);
        Assert.Equal("Details", view.Description);
        Assert.Equal(_world.Linear.StateId("ENG", "In Progress"), view.StateId.Value);
        Assert.Equal("In Progress", view.StateName);
        Assert.Equal(_world.Linear.MemberId("Ada Fixture"), view.AssigneeId?.Value);
        Assert.Equal(2, view.Priority);
        Assert.Equal(3, view.Estimate);
        Assert.Equal([_world.Linear.LabelId("bug")], view.LabelIds.Select(static l => l.Value), StringComparer.Ordinal);
        Assert.Null(view.CycleId);
        Assert.Null(view.ProjectId);
        Assert.Null(view.ParentId);
    }

    [Fact]
    public async Task An_issue_is_read_by_id_too()
    {
        var id = _world.Linear.Issues.Single(static i => i.Team == "ENG").Id;

        var read = await _reads.Issue(_settings, id, Ct);

        Assert.Equal("ENG-1", read.Value!.Key.ToString());
    }

    [Fact]
    public async Task A_missing_issue_is_null_and_not_an_error()
    {
        var read = await _reads.Issue(_settings, "ENG-99", Ct);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : "");
        Assert.Null(read.Value);
    }

    [Fact]
    public async Task An_issue_of_a_team_outside_the_profile_is_null()
    {
        var read = await _reads.Issue(_settings, "OPS-1", Ct);

        Assert.True(read.IsSuccess);
        Assert.Null(read.Value);
    }

    [Fact]
    public async Task A_refused_key_passes_through_as_its_own_error()
    {
        _world.Linear.Switches.RevokeKey = true;

        var read = await _reads.Issue(_settings, "ENG-1", Ct);

        Assert.True(read.IsError);
        Assert.Equal("SND-CONTEXT-071", read.Error.Code);
    }

    [Fact]
    public async Task A_comment_is_read_by_id_with_its_issue()
    {
        var comment = _world.Linear.Comments.Single();

        var read = await _reads.Comment(_settings, ExternalId.From(comment.Id), Ct);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : "");
        Assert.Equal("first comment", read.Value!.Body);
        Assert.Equal(comment.IssueId, read.Value.IssueId.Value);
        Assert.Equal(comment.Id, read.Value.Id.Value);
    }

    [Fact]
    public async Task A_missing_comment_is_null()
    {
        var read = await _reads.Comment(_settings, ExternalId.From("99999999-9999-4999-8999-999999999999"), Ct);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : "");
        Assert.Null(read.Value);
    }

    [Fact]
    public async Task An_attachment_is_found_by_its_url_on_the_issue()
    {
        var id = _world.Linear.Issues.Single(static i => i.Team == "ENG").Id;
        await _world.Mutate(
            "mutation M($issueId: String!, $url: String!, $title: String) { attachmentLinkURL(issueId: $issueId, url: $url, title: $title) { success } }",
            new System.Text.Json.Nodes.JsonObject { ["issueId"] = id, ["url"] = "https://github.com/acme/app/pull/7", ["title"] = "PR 7" });

        var has = await _reads.HasAttachment(_settings, ExternalId.From(id), new Uri("https://github.com/acme/app/pull/7"), Ct);
        var other = await _reads.HasAttachment(_settings, ExternalId.From(id), new Uri("https://github.com/acme/app/pull/8"), Ct);

        Assert.True(has.Value);
        Assert.False(other.Value);
    }

    [Fact]
    public async Task An_attachment_of_a_missing_issue_is_false()
    {
        var has = await _reads.HasAttachment(_settings, ExternalId.From("99999999-9999-4999-8999-999999999999"), new Uri("https://github.com/acme/app/pull/7"), Ct);

        Assert.True(has.IsSuccess, has.IsError ? has.Error.Message : "");
        Assert.False(has.Value);
    }
}
