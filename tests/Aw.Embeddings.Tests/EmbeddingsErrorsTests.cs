using Aw.Kernel;

namespace Aw.Embeddings.Tests;

public sealed class EmbeddingsErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(EmbeddingsErrors.All).IsSuccess);
}
