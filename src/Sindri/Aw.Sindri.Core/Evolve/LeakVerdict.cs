namespace Aw.Sindri.Core.Evolve;

/// <summary>
/// What the leak gate decided. It holds no text from the proposal or its sources: only whether it holds and which layer held it,
/// so a reason or a log line made from it can't carry the matched text.
/// </summary>
internal sealed record LeakVerdict
{
    private LeakVerdict(LeakLayer? layer)
    {
        Layer = layer;
    }

    /// <summary>Gets the verdict for a proposal that passed every layer.</summary>
    public static LeakVerdict Clean { get; } = new((LeakLayer?)null);

    /// <summary>Gets the layer that held it, or null when it passed.</summary>
    public LeakLayer? Layer { get; }

    /// <summary>Gets a value indicating whether a layer held it.</summary>
    public bool Held => Layer is not null;

    /// <summary>Gets the held reason: the layer's fixed words, never matched text; empty when it passed.</summary>
    public string Reason => Layer is { } layer ? $"held by the {layer.ToString().ToLowerInvariant()} layer" : string.Empty;

    /// <summary>The verdict for a hold by one layer.</summary>
    /// <param name="layer">The layer.</param>
    /// <returns>The verdict.</returns>
    public static LeakVerdict HeldBy(LeakLayer layer) => new(layer);
}
