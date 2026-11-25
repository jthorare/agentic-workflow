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

    /// <summary>Pauses a running toolkit: its process keeps running, its tools leave tools/list and calls are refused with SND-TK-005.</summary>
    /// <param name="name">The toolkit name.</param>
    /// <param name="cancellationToken">Cancels the pause.</param>
    /// <returns>Success (pausing a paused toolkit changes nothing), or SND-TK-001 when it has no process.</returns>
    public ValueTask<Result<Unit>> Pause(string name, CancellationToken cancellationToken);

    /// <summary>Ends a pause: the toolkit's tools are listed and callable again.</summary>
    /// <param name="name">The toolkit name.</param>
    /// <param name="cancellationToken">Cancels the resume.</param>
    /// <returns>Success (resuming a toolkit that isn't paused changes nothing), or SND-TK-001 when it has no process.</returns>
    public ValueTask<Result<Unit>> Resume(string name, CancellationToken cancellationToken);
}
