using Aw.Forge.Core;
using Aw.Kernel;

namespace Aw.Forge.Tests;

public sealed class ForgeErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(ForgeErrors.All).IsSuccess);
}
