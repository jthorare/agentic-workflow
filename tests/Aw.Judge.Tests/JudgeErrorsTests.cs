using Aw.Judge.Core.Errors;
using Aw.Kernel;

namespace Aw.Judge.Tests;

public sealed class JudgeErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(JudgeErrors.All).IsSuccess);
}
