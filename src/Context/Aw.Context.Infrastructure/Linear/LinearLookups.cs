using Aw.Context.Core.Linear;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The <see cref="ILinearLookups"/> over the Linear source factory: one reader (and so one key resolution) per fetch.</summary>
/// <param name="linear">The factory of Linear's parts.</param>
internal sealed class LinearLookups(LinearSources linear) : ILinearLookups
{
    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<LinearTeamLookups>>> Fetch(LinearSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return linear.CreateLookupsReader(settings).Read(cancellationToken);
    }
}
