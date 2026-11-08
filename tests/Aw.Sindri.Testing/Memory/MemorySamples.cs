using Aw.Kernel.Testing;
using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Testing.Memory;

/// <summary>Memories and facts for tests. Names are built without <see cref="MemoryName.Parse"/>, so a test of the store does not depend on the parser.</summary>
public static class MemorySamples
{
    /// <summary>A name.</summary>
    /// <param name="value">The name text.</param>
    /// <returns>The name.</returns>
    public static MemoryName Name(string value = "team-notes") => new() { Value = value };

    /// <summary>A memory.</summary>
    /// <param name="name">The name; 'team-notes' when null.</param>
    /// <returns>The memory.</returns>
    public static MemoryInfo Info(MemoryName? name = null) => new(name ?? Name(), "Shared notes", "human:cli", KnownInstants.Epoch);

    /// <summary>A fact.</summary>
    /// <param name="key">The fact's key.</param>
    /// <param name="text">The text.</param>
    /// <param name="tags">The tags; none when null.</param>
    /// <param name="expiresAt">When it expires, or null.</param>
    /// <param name="vector">Its vector; a unit vector on the first axis when null.</param>
    /// <param name="memory">The memory; 'team-notes' when null.</param>
    /// <returns>The fact.</returns>
    public static MemoryFact Fact(string key = "fact-1", string text = "The deploy runs from main", IReadOnlyList<string>? tags = null, DateTimeOffset? expiresAt = null, float[]? vector = null, MemoryName? memory = null)
        => new(key, memory ?? Name(), text, tags ?? [], new MemoryProvenance("wf-1", "run-1", "item-1", "session-1"), expiresAt, KnownInstants.Epoch, KnownInstants.Epoch, vector ?? [1f, 0f, 0f, 0f]);
}
