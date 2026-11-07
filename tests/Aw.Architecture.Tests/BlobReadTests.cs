using Aw.Architecture.Tests.Rules;

namespace Aw.Architecture.Tests;

/// <summary>ARCH-BLOB-001 (#2253): no source file reads a BLOB with GetFieldValue&lt;byte[]&gt;; SqliteRead.GetBlob does it strictly.</summary>
[Trait("Lane", "integration")]
public sealed class BlobReadTests
{
    [Fact]
    public void No_source_file_reads_a_blob_with_a_converting_accessor()
    {
        var source = Path.Combine(Solution.RepoRoot, "src");
        Assert.True(Directory.Exists(source));
        var files = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(p => (Path: Path.GetRelativePath(Solution.RepoRoot, p).Replace('\\', '/'), Text: File.ReadAllText(p)))
            .ToList();
        Assert.NotEmpty(files);

        Verify.None(BlobReadRules.Check(files));
    }

    [Theory]
    [InlineData("var bytes = reader.GetFieldValue<byte[]>(0);")]
    [InlineData("var bytes = r.GetFieldValue< byte[] >(5);")]
    public void A_converting_blob_read_is_reported(string line) =>
        Verify.Exactly(BlobReadRules.Check([("src/X/Stray.cs", line)]), BlobReadRules.Id, "src/X/Stray.cs reads a BLOB");

    [Theory]
    [InlineData("var bytes = SqliteRead.GetBlob(reader, 0);")]
    [InlineData("var text = reader.GetFieldValue<string>(0);")]
    public void The_strict_read_or_another_type_is_not_reported(string line) =>
        Verify.None(BlobReadRules.Check([("src/X/Fine.cs", line)]));
}
