using Aw.Kernel;

namespace Aw.Evidence.Core.Desktop;

/// <summary>Serves a folder of design files over loopback HTTP, for the browser to render.</summary>
public interface IStaticSite
{
    /// <summary>Starts serving <paramref name="directory"/> (GET and HEAD only, nothing outside it) on a free loopback port.</summary>
    /// <param name="directory">The folder (absolute).</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The running site, or AW-EVIDENCE-015.</returns>
    public ValueTask<Result<IServedSite>> ServeAsync(string directory, CancellationToken cancellationToken);
}
