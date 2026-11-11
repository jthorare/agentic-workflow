using System.Reflection;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>The event catalog (API spec §3.4): every type Sindri records is listed once, and the audit row stays private.</summary>
public sealed class EventCatalogTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_event_type_is_in_the_event_catalog()
    {
        var constants = typeof(EventTypes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!);
        var recorded = constants.Append(EventTypes.ServiceRun(ServiceName.From("heartbeat"))).ToList();
        var listed = EventCatalog.All.Select(e => e.Type).ToList();

        Assert.Equal(listed.Count, listed.Distinct(StringComparer.Ordinal).Count());
        Assert.All(recorded, type => Assert.Contains(type, listed, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Command_handled_is_not_public()
    {
        var handled = Assert.Single(EventCatalog.All, e => e.Type == EventTypes.CommandHandled);

        Assert.False(handled.Public);
        Assert.Null(handled.PublicPayload);
        Assert.All(EventCatalog.All.Where(e => e.Public), e => Assert.NotNull(e.PublicPayload));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_item_changing_state_invalidates_the_views_that_void_or_drop_rows_with_it()
    {
        // why: closing an item voids its decisions and requests and drops its needs-you rows
        var invalidates = Assert.Single(EventCatalog.All, e => e.Type == EventTypes.ItemStateChanged).Invalidates;

        Expect.Sequence([ApiViews.Queue, ApiViews.Items, ApiViews.Item, ApiViews.Timeline, ApiViews.NeedsYou, ApiViews.Requests, ApiViews.Decisions], invalidates);
    }

    [Fact, Trait("Lane", "unit")]
    public void Approving_the_profile_invalidates_the_needs_you_view_that_drops_the_drift_row() =>
        Assert.Contains(ApiViews.NeedsYou, Assert.Single(EventCatalog.All, e => e.Type == EventTypes.ProfileApproved).Invalidates, StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_progress_line_invalidates_the_job_and_the_jobs_list_that_serves_its_line_count() =>
        Expect.Sequence([ApiViews.Job, ApiViews.Jobs], Assert.Single(EventCatalog.All, e => e.Type == EventTypes.JobProgress).Invalidates);

    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.run.started", "workflow.run.started:<run id>", typeof(WorkflowRunPublic))]
    [InlineData("workflow.run.finished", "workflow.run.finished:<run id>", typeof(WorkflowRunPublic))]
    [InlineData("workflow.run.cancelled", "workflow.run.cancelled:<run id>", typeof(WorkflowRunPublic))]
    [InlineData("workflow.created", "workflow.created:<workflow id>", typeof(WorkflowChangedPublic))]
    [InlineData("workflow.revised", "workflow.revised:<command id>", typeof(WorkflowChangedPublic))]
    [InlineData("workflow.activated", "workflow.activated:<command id>", typeof(WorkflowChangedPublic))]
    [InlineData("workflow.paused", "workflow.paused:<command id>", typeof(WorkflowChangedPublic))]
    [InlineData("workflow.archived", "workflow.archived:<workflow id>", typeof(WorkflowChangedPublic))]
    [InlineData("workflow.node.started", "workflow.node.started:<run id>:<node id>:<item index>", typeof(WorkflowNodePublic))]
    [InlineData("workflow.node.finished", "workflow.node.finished:<run id>:<node id>:<item index>", typeof(WorkflowNodePublic))]
    [InlineData("workflow.node.failed", "workflow.node.failed:<run id>:<node id>:<item index>", typeof(WorkflowNodePublic))]
    public void Each_graph_run_event_is_catalogued_publicly_with_ids_only_under_its_name_and_key_shape(string type, string keyShape, Type payload)
    {
        var entry = Assert.Single(EventCatalog.All, e => e.Type == type);

        Assert.True(entry.Public);   // #2823: the app re-reads the Workflows page and runs on these; the payload carries ids only, the trigger JSON and error text stay in the ledger
        Assert.Equal(payload, entry.PublicPayload);
        Assert.Equal(keyShape, entry.KeyShape);
        Assert.Contains(type, typeof(EventTypes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetRawConstantValue()!), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_four_project_events_are_public_and_invalidate_only_the_projects_view()
    {
        string[] types = [EventTypes.ProjectAttached, EventTypes.ProjectChanged, EventTypes.ProjectDetached, EventTypes.ProjectHidden];

        foreach (var type in types)
        {
            var entry = Assert.Single(EventCatalog.All, e => e.Type == type);
            Assert.True(entry.Public);
            Assert.NotNull(entry.PublicPayload);
            Expect.Sequence([ApiViews.Projects], entry.Invalidates);
        }

        Assert.Contains(ApiViews.Projects, ApiViews.All, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("linear.issue.created", "linear.issue.created:<clientId>")]
    [InlineData("linear.comment.posted", "linear.comment.posted:<clientId>")]
    [InlineData("linear.issue.changed", "linear.issue.changed:<issue key>:<commandId>")]
    public void Each_linear_write_event_is_catalogued_privately_under_its_name_and_key_shape(string type, string keyShape)
    {
        var entry = Assert.Single(EventCatalog.All, e => e.Type == type);

        Assert.False(entry.Public);   // the actor and the issue key stay in the ledger; Linear's own pages show the write
        Assert.Null(entry.PublicPayload);
        Assert.Equal(keyShape, entry.KeyShape);
        Assert.Contains(type, typeof(EventTypes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetRawConstantValue()!), StringComparer.Ordinal);
    }
}
