using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.ListWorkflowRuns;

namespace Aw.Sindri.Tests.ListWorkflowRuns;

/// <summary>#4334: the runs read's filter values as a caller sends them (query string, MCP arguments, CLI flags), and the refusals.</summary>
[Lane(TestLane.Unit)]
public sealed class RunsFilterTests
{
    [Fact]
    public void No_values_keep_every_run()
    {
        var filter = RunsFilter.Parse(status: null, since: null, until: null, before: null);

        Assert.Equal(RunsFilter.None.Statuses, filter.Value.Statuses);
        Assert.True(filter.Value is { Since: null, Until: null, Before: null });
    }

    [Theory]
    [InlineData("running", new[] { WorkflowRunStatus.Queued, WorkflowRunStatus.Running, WorkflowRunStatus.Waiting })]
    [InlineData("ended", new[] { WorkflowRunStatus.Succeeded })]
    [InlineData("succeeded", new[] { WorkflowRunStatus.Succeeded })]
    [InlineData("failed", new[] { WorkflowRunStatus.Failed })]
    [InlineData("cancelled", new[] { WorkflowRunStatus.Cancelled })]
    [InlineData("failed,cancelled", new[] { WorkflowRunStatus.Failed, WorkflowRunStatus.Cancelled })]
    [InlineData("failed, failed", new[] { WorkflowRunStatus.Failed })]
    public void A_status_word_keeps_its_statuses(string status, WorkflowRunStatus[] kept)
    {
        var filter = RunsFilter.Parse(status, since: null, until: null, before: null);

        Assert.Equal(kept, filter.Value.Statuses);
    }

    [Fact]
    public void Since_and_until_are_iso_8601_instants_and_a_date_alone_is_its_midnight_utc()
    {
        var filter = RunsFilter.Parse(status: null, since: "2026-11-23", until: "2026-11-23T09:30:00+02:00", before: null);

        Assert.Equal(new DateTimeOffset(2026, 11, 23, 0, 0, 0, TimeSpan.Zero), filter.Value.Since);
        Assert.Equal(new DateTimeOffset(2026, 11, 23, 7, 30, 0, TimeSpan.Zero), filter.Value.Until);
    }

    [Fact]
    public void A_plus_offset_sent_raw_in_a_query_string_arrives_as_a_space_and_is_read_as_the_plus()
    {
        var filter = RunsFilter.Parse(status: null, since: "2026-11-25T10:00:00.5 02:30", until: "2026-11-25T10:00:00 01:00", before: null);

        Assert.Equal(new DateTimeOffset(2026, 11, 25, 7, 30, 0, 500, TimeSpan.Zero), filter.Value.Since);
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero), filter.Value.Until);
    }

    [Theory]
    [InlineData("2026-11-25 10:00")]
    [InlineData("2026-11-25T10:00:00 Z")]
    [InlineData("2026-11-25T10:00:00 1:00")]
    [InlineData(" 2026-11-25T10:00:00 01:00")]
    [InlineData("2026-11-25T10:00:00  01:00")]
    public void A_space_anywhere_else_is_still_refused(string since)
    {
        var filter = RunsFilter.Parse(status: null, since, until: null, before: null);

        Assert.Equal(SindriErrors.GraphRunsFilterInvalid.Code, filter.Error.Code);
    }

    [Fact]
    public void Before_is_a_run_id()
    {
        var filter = RunsFilter.Parse(status: null, since: null, until: null, before: "morning-check@1:12");

        Assert.Equal(WorkflowRunId.Of(WorkflowSlug.From("morning-check"), 1, 12), filter.Value.Before);
    }

    [Theory]
    [InlineData("done", null, null, null, "'done' is not a run status")]
    [InlineData("failed,", null, null, null, "'' is not a run status")]
    [InlineData(null, "yesterday", null, null, "'yesterday' is not an ISO-8601 instant")]
    [InlineData(null, null, "2026-13-01", null, "'2026-13-01' is not an ISO-8601 instant")]
    [InlineData(null, "2026-11-24T00:00:00Z", "2026-11-23T00:00:00Z", null, "'since' (2026-11-24T00:00:00Z) is after 'until' (2026-11-23T00:00:00Z)")]
    [InlineData(null, null, null, "run-12", "'run-12' is not a run id")]
    public void A_value_it_cant_read_is_SND_GRAPH_134_naming_the_value(string? status, string? since, string? until, string? before, string detail)
    {
        var filter = RunsFilter.Parse(status, since, until, before);

        Assert.Equal(SindriErrors.GraphRunsFilterInvalid.Code, filter.Error.Code);
        Assert.Contains(detail, filter.Error.Message, StringComparison.Ordinal);
    }
}
