using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Scripts;

/// <summary>A bare test id.</summary>
/// <param name="TestId">The data-testid.</param>
public sealed record TestIdTarget(TestId TestId) : StepTarget
{
    /// <inheritdoc/>
    public override string Label => TestId.Value;
}
