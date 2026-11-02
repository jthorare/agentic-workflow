using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The <see cref="ISourceViewers"/> over the Linear source factory: a viewer when the profile names a Linear token, none otherwise.</summary>
/// <param name="linear">The factory of Linear's parts.</param>
internal sealed class LinearViewers(LinearSources linear) : ISourceViewers
{
    /// <inheritdoc />
    public ISourceViewer? ForLinear(ContextSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Linear is { } linearSettings ? linear.CreateViewer(linearSettings) : null;
    }
}
