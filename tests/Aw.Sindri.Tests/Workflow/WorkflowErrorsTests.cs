using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Tests.Workflow;

public sealed class WorkflowErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_workflow_catalogue_is_valid_together_with_sindris() =>
        Assert.True(ErrorCatalogue.Validate([.. SindriErrors.All, .. WorkflowErrors.All]).IsSuccess);

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-RECIPE", 15)]
    [InlineData("SND-WF", 19)]
    [InlineData("SND-BLOCK", 4)]
    public void Every_code_of_an_area_is_live_or_retired_and_none_is_both(string area, int last)
    {
        var live = WorkflowErrors.All.Select(e => e.Code).Where(c => c.StartsWith(area + "-", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        var retired = WorkflowErrors.Retired.Where(c => c.StartsWith(area + "-", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        var expected = Enumerable.Range(1, last).Select(n => string.Create(CultureInfo.InvariantCulture, $"{area}-{n:000}"));

        Assert.Empty(live.Intersect(retired, StringComparer.Ordinal));
        Assert.Equal(expected, live.Union(retired, StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_retired_codes_are_the_three_the_spec_retires() =>
        Assert.Equal(["SND-RECIPE-004", "SND-WF-002", "SND-WF-007"], WorkflowErrors.Retired, StringComparer.Ordinal);

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-RECIPE-001", ErrorKind.Invalid)]
    [InlineData("SND-RECIPE-002", ErrorKind.NotFound)]
    [InlineData("SND-RECIPE-010", ErrorKind.NotFound)]
    [InlineData("SND-RECIPE-012", ErrorKind.Invalid)]
    [InlineData("SND-RECIPE-013", ErrorKind.Forbidden)]
    [InlineData("SND-RECIPE-014", ErrorKind.Unavailable)]
    [InlineData("SND-RECIPE-015", ErrorKind.Unavailable)]
    [InlineData("SND-WF-006", ErrorKind.Invalid)]
    [InlineData("SND-WF-010", ErrorKind.Unavailable)]
    [InlineData("SND-WF-011", ErrorKind.Forbidden)]
    [InlineData("SND-WF-014", ErrorKind.Invalid)]
    [InlineData("SND-WF-015", ErrorKind.Unavailable)]
    public void Each_code_has_the_spec_s_kind(string code, ErrorKind kind) =>
        Assert.Equal(kind, Assert.Single(WorkflowErrors.All, e => e.Code == code).Kind);
}
