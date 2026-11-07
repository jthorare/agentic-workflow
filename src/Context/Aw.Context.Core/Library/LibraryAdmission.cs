namespace Aw.Context.Core.Library;

/// <summary>Decides from the head of a file whether the library feed reads it (spec §4.1), with the same front-matter rule the design-doc parser applies.</summary>
public static class LibraryAdmission
{
    /// <summary>The bytes of a file that decide.</summary>
    public const int HeadBytes = 4096;

    /// <summary>Classifies a file by its first bytes.</summary>
    /// <param name="path">The file's path, for messages.</param>
    /// <param name="head">The first <see cref="HeadBytes"/> bytes of the file, as text.</param>
    /// <returns>Skip, near miss or read.</returns>
    public static LibraryAdmissionKind Classify(string path, string head) => throw new NotSupportedException();
}
