using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Testing;

/// <summary>The box's limits, changed by the test the way an operator lowers the heavy-slot cap.</summary>
public sealed class FakeBoxLimits : IBoxLimitsSource
{
    /// <summary>The heavy-slot cap now.</summary>
    public int HeavySlots { get; set; } = 3;

    /// <inheritdoc/>
    public BoxLimits Read() => new(HeavySlots);
}
