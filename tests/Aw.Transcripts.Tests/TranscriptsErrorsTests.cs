using Aw.Kernel;
using Aw.Transcripts.Core;

namespace Aw.Transcripts.Tests;

public sealed class TranscriptsErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(TranscriptsErrors.All).IsSuccess);
}
