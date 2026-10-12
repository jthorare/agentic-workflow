using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Tests;

public sealed class SindriErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(SindriErrors.All).IsSuccess);
}
