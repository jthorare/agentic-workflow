using Aw.Kernel;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>Opens the limits file '--limits' names and the scope control it describes (sandbox spec §3.3, SB7).</summary>
internal interface IWrapLimitsSource
{
    /// <summary>Reads the file and makes the scope control for its unit.</summary>
    /// <param name="path">The limits file.</param>
    /// <returns>The limits and the scope, or why the file could not be used (wrap then refuses to start the agent unwatched).</returns>
    public Result<WrapLimitsSetup> Open(string path);
}
