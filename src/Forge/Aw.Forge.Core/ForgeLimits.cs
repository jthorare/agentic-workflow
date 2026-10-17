namespace Aw.Forge.Core;

/// <summary>Sizes the port promises: what an adapter caps before it returns text.</summary>
public static class ForgeLimits
{
    /// <summary>The most characters of a check log excerpt (CheckLog); an adapter cuts the head, keeping the tail where failures sit.</summary>
    public const int LogExcerptCharacters = 8000;

    /// <summary>The longest idempotency marker.</summary>
    public const int MarkerCharacters = 128;
}
