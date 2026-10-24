using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>The box's limits from the heavy wrapper's cap file or AW_HEAVY_SLOTS, read on every call.</summary>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="stateRoot">The state root (AW_STATE_DIR or ~/.agentic-workflow).</param>
internal sealed class HeavyCapSource(Func<string, string?> environment, string stateRoot) : IBoxLimitsSource
{
    /// <inheritdoc/>
    public BoxLimits Read() => HeavyCap.Read(environment, stateRoot);
}
