using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Tests.Line;

public sealed class LineErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_line_catalogue_is_valid_together_with_sindris_the_engines_and_scopings() =>
        Assert.True(ErrorCatalogue.Validate([.. SindriErrors.All, .. WorkflowErrors.All, .. ScopingErrors.All, .. LineErrors.All]).IsSuccess);

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-LINE-001", ErrorKind.Conflict)]
    [InlineData("SND-LINE-002", ErrorKind.Conflict)]
    [InlineData("SND-LINE-003", ErrorKind.Invalid)]
    [InlineData("SND-LINE-004", ErrorKind.Invalid)]
    public void Each_code_has_its_kind(string code, ErrorKind kind) =>
        Assert.Equal(kind, Assert.Single(LineErrors.All, e => e.Code == code).Kind);
}
