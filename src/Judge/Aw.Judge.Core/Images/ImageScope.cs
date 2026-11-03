
namespace Aw.Judge.Core.Images;

/// <summary>
/// One call's images as the agent CLIs reach them: the first CLI that needs the directory makes it (Jev settles most calls, so most
/// calls never write one), later ones share it, and the evaluator disposes the scope after the last provider.
/// </summary>
/// <param name="workspaces">Makes the directory.</param>
/// <param name="images">The images that were read.</param>
/// <param name="relativeTo">The directory the images' relative names are taken from, or null.</param>
public sealed class ImageScope(IImageWorkspaces workspaces, IReadOnlyList<ImageAttachment> images, string? relativeTo) : IDisposable
{
    private IImageWorkspace? _workspace;

    /// <summary>Makes the directory on the first call; the same one after.</summary>
    /// <returns>The folder; the scope removes it on dispose.</returns>
    /// <remarks>Throws what <see cref="IImageWorkspaces.Create"/> throws.</remarks>
    public IImageFolder Open() => _workspace ??= workspaces.Create(images, relativeTo);

    /// <inheritdoc />
    public void Dispose() => _workspace?.Dispose();
}
