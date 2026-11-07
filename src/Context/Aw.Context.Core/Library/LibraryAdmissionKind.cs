namespace Aw.Context.Core.Library;

/// <summary>What the first 4 KiB of a file say about whether the library reads it (review page spec 2026-11-04 §4.1).</summary>
public enum LibraryAdmissionKind
{
    /// <summary>No <c language="text">aw-doc</c> front matter: not a design doc, skipped before any text is stored.</summary>
    Skip,

    /// <summary>Front matter with an <c language="text">aw-doc</c> that is not the number 1 (or sits after a blank first line): counted, not stored.</summary>
    NearMiss,

    /// <summary>A design doc to read: <c language="text">aw-doc: 1</c>, or a whole number other than 1 (stored with SND-LIB-010 and no items).</summary>
    Read,
}
