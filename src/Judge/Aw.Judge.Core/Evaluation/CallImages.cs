using Aw.Judge.Core.Images;

namespace Aw.Judge.Core.Evaluation;

/// <summary>The images one call sends, read once, and the scope the agent CLIs open them from (null when there are none).</summary>
internal sealed record CallImages(IReadOnlyList<ImageAttachment> Images, ImageScope? Scope)
{
    /// <summary>A call with no images.</summary>
    public static CallImages None { get; } = new(Images: [], Scope: null);
}
