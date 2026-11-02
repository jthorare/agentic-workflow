namespace Aw.Judge.Core.Batching;

/// <summary>The Jev model a batched call asks for, so a caller that caches Jev's answers can key them by it (a new model or version is a deliberate cache miss).</summary>
public static class JevModel
{
    /// <summary>The model id and its version, as sent in every batched request.</summary>
    public const string Id = "jev-1.13.0";
}
