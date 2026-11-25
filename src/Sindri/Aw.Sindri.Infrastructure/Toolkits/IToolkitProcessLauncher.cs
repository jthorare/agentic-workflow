using Aw.Kernel;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>Starts a toolkit's process in its sandbox and connects its MCP client; the supervisor's only door to a real process.</summary>
internal interface IToolkitProcessLauncher
{
    /// <summary>Starts one process.</summary>
    /// <param name="toolkit">The toolkit.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The running process, SND-TK-008 when this box has no process sandbox, or SND-TK-004 when it ended before it answered.</returns>
    public ValueTask<Result<IToolkitProcess>> Launch(Toolkit toolkit, CancellationToken cancellationToken);
}
