using System.Reflection;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Infrastructure.Workflows;
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
    [InlineData(WorkflowEventType.Started, "workflow.started")]
    [InlineData(WorkflowEventType.Transition, "workflow.transition")]
    [InlineData(WorkflowEventType.Refused, "workflow.refused")]
    [InlineData(WorkflowEventType.Shifted, "workflow.shifted")]
    [InlineData(WorkflowEventType.Notified, "workflow.notified")]
    [InlineData(WorkflowEventType.Expired, "workflow.expired")]
    [InlineData(WorkflowEventType.Heavy, "workflow.heavy")]
    public void Every_type_the_interpreter_mirrors_is_catalogued_under_the_engines_name(WorkflowEventType type, string name)
    {
        var mirrored = RecipeWorkflow.TypeOf(type);

        Assert.Equal(name, mirrored);
        Assert.Contains(mirrored, EventCatalog.All.Select(e => e.Type), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_effect_refusal_is_not_mirrored_before_the_effect_phase() =>
        // why: spec 5.7 names the dispatch refusal outbox.refused (P6); P4 has no effects, so no made-up kind is emitted
        Assert.Throws<NotSupportedException>(() => RecipeWorkflow.TypeOf(WorkflowEventType.EffectRefused));

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
}
