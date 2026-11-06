namespace Aw.Forge.Core;

/// <summary>Sizes the port promises: what an adapter caps before it returns text.</summary>
public static class ForgeLimits
{
    /// <summary>The most characters of a check log excerpt (CheckLog); an adapter cuts the head, keeping the tail where failures sit.</summary>
    public const int LogExcerptCharacters = 8000;

    /// <summary>The longest idempotency marker.</summary>
    public const int MarkerCharacters = 128;

    /// <summary>The most bytes of a file's text FileText returns; an adapter cuts the rest.</summary>
    public const int FileTextBytes = 1024 * 1024;

    /// <summary>The most files Files lists: GitHub's own cap on a pull request's file list.</summary>
    public const int PullFileCap = 3000;
}
