using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Tests.GetWorkflow;

/// <summary>GET /sindri/workflows/{slug}?revision=N|latest: what the route accepts. Anything else is invalid input, never the active spec.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowRevisionQueryTests
{
    [Fact]
    public void No_revision_reads_the_one_the_workflow_runs()
    {
        var request = WorkflowReadEndpoints.Show("morning-check", revision: null).Value;

        Assert.Equal((null, false), (request.Revision, request.Newest));
    }

    [Fact]
    public void Latest_reads_the_newest_revision()
    {
        var request = WorkflowReadEndpoints.Show("morning-check", "latest").Value;

        Assert.Equal((null, true), (request.Revision, request.Newest));
    }

    [Fact]
    public void A_number_reads_that_revision()
    {
        var request = WorkflowReadEndpoints.Show("morning-check", "2").Value;

        Assert.Equal((2, false), (request.Revision, request.Newest));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("2.5")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("Latest")]
    [InlineData("99999999999")]
    public void Anything_else_is_invalid_input(string revision)
    {
        var result = WorkflowReadEndpoints.Show("morning-check", revision);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    [Fact]
    public void A_bad_slug_is_still_invalid_input()
    {
        var result = WorkflowReadEndpoints.Show("Not A Slug", "latest");

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }
}
