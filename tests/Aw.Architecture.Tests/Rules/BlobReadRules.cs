using System.Text.RegularExpressions;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// A BLOB column is read through Aw.Kernel.Data's SqliteRead.GetBlob (#2253): GetFieldValue&lt;byte[]&gt; converts, so a TEXT value or a damaged column reads back as bytes and then as garbage.
/// Read from the source text: any file that calls GetFieldValue&lt;byte[]&gt;.
/// </summary>
public static partial class BlobReadRules
{
    /// <summary>A source file reads a BLOB with GetFieldValue&lt;byte[]&gt;.</summary>
    public const string Id = "ARCH-BLOB-001";

    [GeneratedRegex(@"\bGetFieldValue\s*<\s*byte\s*\[\s*\]\s*>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ConvertingRead { get; }

    /// <summary>Checks the source files.</summary>
    /// <param name="files">Each file's repo-relative path and its text.</param>
    /// <returns>One violation per offending file.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<(string Path, string Text)> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return files
            .Where(f => ConvertingRead.IsMatch(f.Text))
            .Select(f => new Violation(Id, $"{f.Path} reads a BLOB with GetFieldValue<byte[]>",
                "call SqliteRead.GetBlob (Aw.Kernel.Data): it throws InvalidDataException naming the column when the value is not a BLOB, instead of converting it"));
    }
}
