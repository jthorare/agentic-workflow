using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Records;

/// <summary>Builds the <see cref="ISourceViewer"/> of the Linear source for the profile's settings.</summary>
public interface ISourceViewers
{
    /// <summary>The Linear viewer for <paramref name="settings"/>.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <returns>The viewer, or null when the profile configures no Linear token.</returns>
    public ISourceViewer? ForLinear(ContextSettings settings);
}
