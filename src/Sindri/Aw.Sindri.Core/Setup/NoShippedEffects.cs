namespace Aw.Sindri.Core.Setup;

/// <summary>The build that embeds no recipes: it ships no effect kinds.</summary>
public sealed class NoShippedEffects : IShippedEffects
{
    /// <inheritdoc/>
    public IReadOnlyList<string> Kinds() => [];
}
