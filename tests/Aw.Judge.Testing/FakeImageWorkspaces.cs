using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Questions;

namespace Aw.Judge.Testing;

/// <summary>The IImageWorkspaces fake: workspaces held in memory, named the way the real ones name their files. Passes ImageWorkspacesContract.</summary>
public sealed class FakeImageWorkspaces : IImageWorkspaces
{
    private readonly List<FakeImageWorkspace> _created = [];

    /// <summary>Every workspace made, in order.</summary>
    public IReadOnlyList<FakeImageWorkspace> Created => _created;

    /// <inheritdoc />
    public IImageWorkspace Create(IReadOnlyList<ImageAttachment> images, string? relativeTo)
    {
        ArgumentNullException.ThrowIfNull(images);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        for (var i = 0; i < images.Count; i++)
        {
            files[NameOf(images[i], i, relativeTo)] = images[i].Bytes.ToArray();
        }

        var workspace = new FakeImageWorkspace("/fake/aw-judge-images-" + (_created.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), files);
        _created.Add(workspace);
        return workspace;
    }

    private static string NameOf(ImageAttachment image, int index, string? relativeTo)
    {
        var prefix = relativeTo?.TrimEnd('/') + "/";
        return relativeTo is not null && image.Path.StartsWith(prefix, StringComparison.Ordinal)
            ? image.Path[prefix.Length..]
            : "image-" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + (image.Format == ImageFormat.Png ? ".png" : ".jpg");
    }
}
