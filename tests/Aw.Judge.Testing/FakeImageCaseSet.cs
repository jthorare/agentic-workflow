using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Testing;

/// <summary>The IImageCaseSet fake: the cases it is given, in order; none when it is given none.</summary>
/// <param name="cases">The cases.</param>
public sealed class FakeImageCaseSet(params ImageCase[] cases) : IImageCaseSet
{
    /// <inheritdoc />
    public IReadOnlyList<ImageCase> Cases { get; } = cases;
}
