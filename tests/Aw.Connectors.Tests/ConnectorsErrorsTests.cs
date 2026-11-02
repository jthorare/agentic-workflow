using Aw.Connectors.Core;
using Aw.Kernel;

namespace Aw.Connectors.Tests;

public sealed class ConnectorsErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(ConnectorsErrors.All).IsSuccess);
}
