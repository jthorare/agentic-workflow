using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sandbox;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The sandbox's reads and write of the fake (SB-6): each answers a golden (spec-shaped until the server's are recorded), the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<AllowRequest> _allowed = [];
    private readonly List<string> _denialItems = [];
    private int _sandboxStatusCalls;
    private int _sandboxCheckCalls;

    /// <summary>GET /sindri/sandbox/status' answer: ready by default.</summary>
    public RouteAnswer SandboxStatusAnswer { get; set; } = RouteAnswer.SpecShaped("sandbox-status", "ready");

    /// <summary>GET /sindri/sandbox/check's answer: still failing by default.</summary>
    public RouteAnswer SandboxCheckAnswer { get; set; } = RouteAnswer.SpecShaped("sandbox-check", "still-failing");

    /// <summary>GET /sindri/sandbox/denials' answer: a few denials by default.</summary>
    public RouteAnswer SandboxDenialsAnswer { get; set; } = RouteAnswer.SpecShaped("sandbox-denials", "some");

    /// <summary>POST /sindri/sandbox/allow's answer: needs your approval by default.</summary>
    public RouteAnswer SandboxAllowAnswer { get; set; } = RouteAnswer.SpecShaped("sandbox-allow", "needs-approval");

    /// <summary>When set, Allow waits on it before answering (the row stays in Sending meanwhile).</summary>
    public TaskCompletionSource? SandboxAllowGate { get; set; }

    /// <summary>How many times the status was read.</summary>
    public int SandboxStatusCalls => Volatile.Read(ref _sandboxStatusCalls);

    /// <summary>How many times Check again was read.</summary>
    public int SandboxCheckCalls => Volatile.Read(ref _sandboxCheckCalls);

    /// <summary>The items the denials were read for, in order.</summary>
    public IReadOnlyList<string> SandboxDenialItems { get { lock (_gate) { return [.. _denialItems]; } } }

    /// <summary>The Allow requests sent, in order.</summary>
    public IReadOnlyList<AllowRequest> Allowed { get { lock (_gate) { return [.. _allowed]; } } }

    public Task<Result<SandboxStatusView>> SandboxStatus(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _sandboxStatusCalls);
        return Task.FromResult(Answer<SandboxStatusView>(api, "/sindri/sandbox/status", SandboxStatusAnswer));
    }

    public Task<Result<SandboxCheckView>> SandboxCheck(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _sandboxCheckCalls);
        return Task.FromResult(Answer<SandboxCheckView>(api, "/sindri/sandbox/check", SandboxCheckAnswer));
    }

    public Task<Result<DenialsView>> SandboxDenials(Uri api, string item, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _denialItems.Add(item);
        }

        return Task.FromResult(Answer<DenialsView>(api, "/sindri/sandbox/denials", SandboxDenialsAnswer));
    }

    public async Task<Result<AllowView>> SandboxAllow(Uri api, AllowRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<AllowView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _allowed.Add(request);
        }

        await WaitFor(SandboxAllowGate, cancellationToken).ConfigureAwait(false);
        return Answer<AllowView>(api, "/sindri/sandbox/allow", SandboxAllowAnswer);
    }
}
