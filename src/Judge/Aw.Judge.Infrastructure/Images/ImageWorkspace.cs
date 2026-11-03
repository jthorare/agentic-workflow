using Aw.Judge.Core.Images;
using System.Globalization;

namespace Aw.Judge.Infrastructure.Images;

/// <summary>
/// A per-call directory ($TMPDIR/aw-judge-images-&lt;random&gt;/, mode 0700) holding the bytes that were read, for the agent CLIs: the
/// same bytes the other providers saw, written from memory and never re-read from their paths. Creating one first sweeps any
/// such directory older than an hour (a judge killed by a hook's timeout leaves one), without following symlinks. Deleted on dispose.
/// </summary>
internal sealed class ImageWorkspace : IImageWorkspace
{
    private const string Prefix = "aw-judge-images-";

    private ImageWorkspace(string path, IReadOnlyList<string> files)
    {
        Path = path;
        Files = files;
    }

    /// <inheritdoc />
    public string Path { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Files { get; }

    /// <summary>Sweeps old directories, then writes the images into a fresh one.</summary>
    /// <param name="images">The images, in order.</param>
    /// <param name="relativeTo">When set, each file lands under its path relative to this directory; else as image-1.png, image-2.jpg, ...</param>
    /// <returns>The workspace.</returns>
    public static ImageWorkspace Create(IReadOnlyList<ImageAttachment> images, string? relativeTo)
    {
        ArgumentNullException.ThrowIfNull(images);
        Sweep(TimeSpan.FromHours(1));
        var directory = Directory.CreateTempSubdirectory(Prefix).FullName;
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            var names = new List<string>(images.Count);
            for (var i = 0; i < images.Count; i++)
            {
                var name = NameOf(images[i], i, relativeTo);
                var target = System.IO.Path.Combine(directory, name);
                _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target) ?? directory);
                File.WriteAllBytes(target, images[i].Bytes.Span);
                names.Add(name);
            }

            return new ImageWorkspace(directory, names);
        }
        catch
        {
            Remove(directory);
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose() => Remove(Path);

    // A path under relativeTo keeps its relative name ('after.png', 'base/before.png'); anything else is image-N with its format's extension.
    private static string NameOf(ImageAttachment image, int index, string? relativeTo)
    {
        if (relativeTo is not null)
        {
            var relative = System.IO.Path.GetRelativePath(relativeTo, image.Path).Replace('\\', '/');
            if (relative != ".." && !relative.StartsWith("../", StringComparison.Ordinal) && !System.IO.Path.IsPathRooted(relative))
            {
                return relative;
            }
        }

        return "image-" + (index + 1).ToString(CultureInfo.InvariantCulture) + (image.Format == ImageFormat.Png ? ".png" : ".jpg");
    }

    // Best effort: a directory that can't be removed (not ours: its 0700 mode keeps others out) stays.
    private static void Sweep(TimeSpan age)
    {
        try
        {
            foreach (var old in Directory.EnumerateDirectories(System.IO.Path.GetTempPath(), Prefix + "*"))
            {
                var info = new DirectoryInfo(old);
                if (info.LinkTarget is null && DateTime.UtcNow - info.LastWriteTimeUtc > age)
                {
                    Remove(old);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A sweep that can't list the temp directory leaves it as it is; the call goes on.
        }
    }

    private static void Remove(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover temp directory is harmless; the answer stands (the next call's sweep removes it).
        }
    }
}
