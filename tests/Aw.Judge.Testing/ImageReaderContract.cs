using System.Buffers.Binary;
using System.Security.Cryptography;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>The IImageReader contract: the fake and the file reader both pass it.</summary>
public abstract class ImageReaderContract
{
    /// <summary>A reader over these files.</summary>
    /// <param name="files">File bytes by name, relative to the returned directory.</param>
    /// <param name="links">Symlinks by name, each to the name (a file or a directory in <paramref name="files"/>) it points at; both relative to the returned directory.</param>
    /// <returns>The reader, and the absolute directory the names are under (no trailing slash).</returns>
    protected abstract (IImageReader Reader, string Directory) Create(IReadOnlyDictionary<string, byte[]> files, IReadOnlyDictionary<string, string> links);

    private (IImageReader Reader, string Directory) Create(IReadOnlyDictionary<string, byte[]> files) =>
        Create(files, new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>A PNG gives its size, format and sha256, read from the same bytes.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task A_png_gives_its_size_and_sha256()
    {
        var bytes = Png(640, 480);
        var (reader, directory) = Create(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["a.png"] = bytes });

        var read = await reader.Read([directory + "/a.png"], root: null, TestContext.Current.CancellationToken);

        Expect.That(read.Problem is null && read.Images.Count == 1, "one image and no problem");
        var image = read.Images[0];
        Expect.That(image.Width == 640 && image.Height == 480 && image.Format == ImageFormat.Png, "size and format come from the header");
        Expect.That(image.Sha256 == Convert.ToHexStringLower(SHA256.HashData(bytes)), "sha256 is lowercase hex of the bytes");
        Expect.That(image.Bytes.Span.SequenceEqual(bytes) && image.Path == directory + "/a.png", "the bytes and path are the file's");
    }

    /// <summary>Several files come back in the order asked, and a relative path is taken from the root.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task Files_come_back_in_order_and_relative_paths_are_taken_from_the_root()
    {
        var (reader, directory) = Create(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["a.png"] = Png(10, 20), ["base/b.png"] = Png(30, 40) });

        var read = await reader.Read(["base/b.png", "a.png"], directory, TestContext.Current.CancellationToken);

        Expect.That(read.Problem is null, "no problem");
        Expect.That(read.Images.Select(i => i.Path).SequenceEqual([directory + "/base/b.png", directory + "/a.png"], StringComparer.Ordinal), "absolute paths, in the order asked");
        Expect.That(read.Images.Select(i => i.Width).SequenceEqual([30, 10]), "each file's own size");
    }

    /// <summary>A missing file is a problem that names the path, with no images.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task A_missing_file_is_a_problem_naming_the_path()
    {
        var (reader, directory) = Create(new Dictionary<string, byte[]>(StringComparer.Ordinal));

        var read = await reader.Read([directory + "/gone.png"], root: null, TestContext.Current.CancellationToken);

        Expect.That(read.Problem is { } problem && problem.Contains("gone.png", StringComparison.Ordinal), "the problem names the path");
        Expect.That(read.Images.Count == 0, "no images");
    }

    /// <summary>A file that isn't a PNG or JPEG is a problem, whatever its name.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task A_file_that_is_not_a_png_or_jpeg_is_a_problem()
    {
        var (reader, directory) = Create(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["fake.png"] = "GIF89a not a png"u8.ToArray() });

        var read = await reader.Read([directory + "/fake.png"], root: null, TestContext.Current.CancellationToken);

        Expect.That(read.Problem is { } problem && problem.Contains("fake.png", StringComparison.Ordinal), "the problem names the path");
        Expect.That(read.Images.Count == 0, "no images");
    }

    /// <summary>A file outside the root is refused.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task A_file_outside_the_root_is_a_problem()
    {
        var (reader, directory) = Create(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["in/a.png"] = Png(1, 1), ["out/b.png"] = Png(1, 1) });

        var read = await reader.Read([directory + "/out/b.png"], directory + "/in", TestContext.Current.CancellationToken);

        Expect.That(read.Problem is not null && read.Images.Count == 0, "a path outside the root is a problem and nothing is read");
    }

    /// <summary>A JPEG gives its size and format.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task A_jpeg_gives_its_size()
    {
        var (reader, directory) = Create(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["p.jpg"] = Jpeg(3024, 1964) });

        var read = await reader.Read([directory + "/p.jpg"], root: null, TestContext.Current.CancellationToken);

        Expect.That(read.Problem is null && read.Images.Count == 1, "one image and no problem");
        Expect.That(read.Images[0].Format == ImageFormat.Jpeg && read.Images[0].Width == 3024 && read.Images[0].Height == 1964, "size and format from the first SOFn");
    }

    /// <summary>A directory is a problem that says so.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task A_directory_is_a_problem()
    {
        var (reader, directory) = Create(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["d/x.png"] = Png(1, 1) });

        var read = await reader.Read([directory + "/d"], root: null, TestContext.Current.CancellationToken);

        Expect.That(read.Problem is { } problem && problem.Contains("directory", StringComparison.Ordinal) && read.Images.Count == 0, "a directory is refused as a directory");
    }

    /// <summary>A '..' that stays inside the root is folded away; one that leaves it is refused.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task Dot_dot_is_folded_and_one_that_leaves_the_root_is_refused()
    {
        var (reader, directory) = Create(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["in/a.png"] = Png(7, 8), ["out/b.png"] = Png(1, 1) });

        var inside = await reader.Read(["../in/a.png"], directory + "/in", TestContext.Current.CancellationToken);
        var outside = await reader.Read(["../out/b.png"], directory + "/in", TestContext.Current.CancellationToken);

        Expect.That(inside.Problem is null && inside.Images.Count == 1 && inside.Images[0].Path == directory + "/in/a.png", "the folded path is the file's");
        Expect.That(outside.Problem is { } problem && problem.Contains("outside", StringComparison.Ordinal) && outside.Images.Count == 0, "a '..' out of the root is outside it");
    }

    /// <summary>A symlinked directory in the middle of the path counts: one that leads out of the root is refused, one that stays inside is read under its own name.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task A_symlink_in_a_middle_directory_is_resolved()
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["outside/x.png"] = Png(1, 1), ["in/real/y.png"] = Png(5, 6) };
        var links = new Dictionary<string, string>(StringComparer.Ordinal) { ["in/escape"] = "outside", ["in/shortcut"] = "in/real" };
        var (reader, directory) = Create(files, links);

        var escaped = await reader.Read(["escape/x.png"], directory + "/in", TestContext.Current.CancellationToken);
        var inside = await reader.Read(["shortcut/y.png"], directory + "/in", TestContext.Current.CancellationToken);

        Expect.That(escaped.Problem is { } problem && problem.Contains("outside", StringComparison.Ordinal) && escaped.Images.Count == 0, "a link out of the root is refused");
        Expect.That(inside.Problem is null && inside.Images.Count == 1 && inside.Images[0].Path == directory + "/in/shortcut/y.png", "a link inside the root is read under the name it was given");
    }

    // An APP0 segment, a DHT segment (marker C4, not a frame) and an SOF0 frame header.
    private static byte[] Jpeg(int width, int height)
    {
        var sof = new byte[19];
        sof[0] = 0xFF;
        sof[1] = 0xC0;
        BinaryPrimitives.WriteUInt16BigEndian(sof.AsSpan(2), 17);
        sof[4] = 8;
        BinaryPrimitives.WriteUInt16BigEndian(sof.AsSpan(5), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(sof.AsSpan(7), (ushort)width);
        sof[9] = 3;
        byte[] app0 = [0xFF, 0xE0, 0x00, 0x10, .. "JFIF\0"u8, 1, 1, 0, 0, 1, 0, 1, 0, 0];
        byte[] dht = [0xFF, 0xC4, 0x00, 0x05, 0x00, 0x01, 0x02];
        return [0xFF, 0xD8, .. app0, .. dht, .. sof];
    }

    // A PNG signature and IHDR chunk: all the judge reads.
    private static byte[] Png(int width, int height)
    {
        var header = new byte[29];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(8), 13);
        "IHDR"u8.CopyTo(header.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(20), (uint)height);
        header[24] = 8;
        header[25] = 2;
        return header;
    }
}
