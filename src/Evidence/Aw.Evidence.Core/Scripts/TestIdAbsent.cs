using System.Text.Json.Serialization;
using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Scripts;

/// <summary>
/// No visible element has <paramref name="TestId"/>. Checked once, right after the action, without waiting for the element
/// to go: a leftover that is there straight after a drag drop fails the step.
/// </summary>
/// <param name="TestId">The data-testid that must be absent.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TestIdAbsent(TestId TestId) : ExpectedState
{
    /// <inheritdoc/>
    public override string Describe() => $"element {TestId.Value} absent";

    /// <inheritdoc/>
    public override string? Problem() => null;
}
