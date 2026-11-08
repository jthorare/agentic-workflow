namespace Aw.Sindri.Core.Memory;

/// <summary>How a recall ranked the facts.</summary>
public enum RecallMode
{
    /// <summary>By cosine similarity of embeddings ('semantic').</summary>
    Semantic = 1,

    /// <summary>By token overlap, because no model was available ('keyword').</summary>
    Keyword = 2,
}
