using Aw.Kernel;

namespace Aw.Sindri.Core.Toolkits;

/// <summary>Starts and stops toolkit processes (the supervisor).</summary>
public interface IToolkitRuntime
{
    /// <summary>Starts the toolkit's process in its sandbox and connects its MCP client.</summary>
    /// <param name="toolkit">A toolkit that is built for its current source.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>Success, or SND-TK-008 / SND-TK-004.</returns>
    public ValueTask<Result<Unit>> Start(Toolkit toolkit, CancellationToken cancellationToken);

    /// <summary>Stops the toolkit's process.</summary>
    /// <param name="name">The toolkit name.</param>
    /// <param name="cancellationToken">Cancels the stop.</param>
    /// <returns>Success; stopping a toolkit that isn't running is success.</returns>
    public ValueTask<Result<Unit>> Stop(string name, CancellationToken cancellationToken);
}
