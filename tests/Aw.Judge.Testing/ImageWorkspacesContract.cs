using Aw.Judge.Core.Images;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>The IImageWorkspaces contract: the fake and the temp-directory adapter both pass it.</summary>
public abstract class ImageWorkspacesContract
{
    /// <summary>The workspaces under test.</summary>
    /// <returns>The port.</returns>
    protected abstract IImageWorkspaces Create();

    /// <summary>A file in a workspace.</summary>
    /// <param name="workspace">A workspace made by <see cref="Create"/>.</param>
    /// <param name="name">A name in its <see cref="IImageFolder.Files"/>.</param>
    /// <returns>The bytes, or null when the file is gone.</returns>
    protected abstract byte[]? Read(IImageWorkspace workspace, string name);

    /// <summary>Files under the relative-to directory keep their relative names, and the bytes written are the bytes read.</summary>
    [Fact]
    public void Files_keep_their_relative_names_and_the_bytes_that_were_read()
    {
        using var workspace = Create().Create([Image("/ev/after.png", [1, 2, 3]), Image("/ev/base/before.png", [4, 5])], "/ev");

        Expect.That(workspace.Files.SequenceEqual(["after.png", "base/before.png"], StringComparer.Ordinal), "relative names, in order");
        Expect.That(Read(workspace, "after.png") is [1, 2, 3] && Read(workspace, "base/before.png") is [4, 5], "the bytes that were read");
    }

    /// <summary>Without a directory to be relative to, files are image-N with their format's extension.</summary>
    [Fact]
    public void Without_a_directory_files_are_numbered()
    {
        using var workspace = Create().Create([Image("/x/one.png", [1]), Image("/y/two.jpg", [2], ImageFormat.Jpeg)], relativeTo: null);

        Expect.That(workspace.Files.SequenceEqual(["image-1.png", "image-2.jpg"], StringComparer.Ordinal), "numbered by position, extension by format");
    }

    /// <summary>Disposing removes the files.</summary>
    [Fact]
    public void Disposing_removes_the_files()
    {
        var workspace = Create().Create([Image("/ev/after.png", [1])], "/ev");

        workspace.Dispose();

        Expect.That(Read(workspace, "after.png") is null, "the file is gone");
    }

    private static ImageAttachment Image(string path, byte[] bytes, ImageFormat format = ImageFormat.Png) =>
        new() { Path = path, Sha256 = "00", Bytes = bytes, Width = 1, Height = 1, Format = format };
}
