using Aw.Kernel;
using Aw.Sindri.Ui.Api.CodeIndex;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The code index's read of the fake (CI-10b): GET /sindri/code-index/status answers an API golden, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private int _codeIndexCalls;

    /// <summary>GET /sindri/code-index/status's answer: the one-repo golden by default.</summary>
    public RouteAnswer CodeIndexStatusAnswer { get; set; } = RouteAnswer.Api("code-index-status", "one-repo");

    /// <summary>How many times the status was read.</summary>
    public int CodeIndexCalls => Volatile.Read(ref _codeIndexCalls);

    /// <summary>Holds the next status read (once) until it is completed; the read takes its answer when it starts, so a later change of <see cref="CodeIndexStatusAnswer"/> is not seen by it.</summary>
    public TaskCompletionSource? CodeIndexHold { get; set; }

    /// <summary>The token of the newest status read.</summary>
    public CancellationToken CodeIndexToken { get; private set; }

    public async Task<Result<CodeIndexStatusView>> CodeIndexStatus(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _codeIndexCalls);
        CodeIndexToken = cancellationToken;
        var (answer, hold) = (CodeIndexStatusAnswer, CodeIndexHold);
        CodeIndexHold = null;
        await WaitFor(hold, cancellationToken).ConfigureAwait(false);
        return Answer<CodeIndexStatusView>(api, "/sindri/code-index/status", answer);
    }
}
