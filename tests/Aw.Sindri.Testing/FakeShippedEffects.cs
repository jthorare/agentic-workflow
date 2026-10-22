using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Testing;

/// <summary>The effect kinds the shipped recipes declare, set by the test.</summary>
/// <param name="kinds">The effect ids.</param>
public sealed class FakeShippedEffects(params string[] kinds) : IShippedEffects
{
    /// <inheritdoc/>
    public IReadOnlyList<string> Kinds() => kinds;
}
