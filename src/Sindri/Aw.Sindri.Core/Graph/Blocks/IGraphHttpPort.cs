using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where an 'http' node's request is sent: the host allowlist, the loopback refusal, the redirect rule and the 1 MB cap live behind it.</summary>
public interface IGraphHttpPort
{
    /// <summary>Sends the request and reads the answer.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answer, SND-GRAPH-122 when the request was refused before it was sent, or SND-GRAPH-123 when it failed.</returns>
    public ValueTask<Result<GraphHttpResponse>> Send(GraphHttpRequest request, CancellationToken cancellationToken);
}
