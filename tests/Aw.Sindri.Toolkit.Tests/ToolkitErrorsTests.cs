using Aw.Kernel;

namespace Aw.Sindri.Toolkit.Tests;

public sealed class ToolkitErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(ToolkitErrors.All).IsSuccess);
}
