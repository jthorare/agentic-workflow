using Aw.Judge.Core.Images;

namespace Aw.Judge.Infrastructure.Images;

/// <summary>Makes <see cref="ImageWorkspace"/>s under the temp directory.</summary>
internal sealed class ImageWorkspaces : IImageWorkspaces
{
    /// <inheritdoc />
    public IImageWorkspace Create(IReadOnlyList<ImageAttachment> images, string? relativeTo) => ImageWorkspace.Create(images, relativeTo);
}
