namespace Aw.Evidence.Core.Scripts;

/// <summary>An element by its exact text.</summary>
/// <param name="Text">The exact text.</param>
/// <param name="Nth">Which of several matches (1-based), or null for exactly one.</param>
public sealed record TextTarget(string Text, int? Nth) : StepTarget
{
    /// <inheritdoc/>
    public override string Label => Text;
}
