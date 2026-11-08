using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The profile routes of the fake (the summary and the approval): each answers a golden, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<ApproveProfileRequest> _profileApprovals = [];
    private int _profileCalls;

    /// <summary>POST /sindri/profile/approve's answer.</summary>
    public RouteAnswer ApproveProfileAnswer { get; set; } = RouteAnswer.Api("profile-approve", "approved");

    /// <summary>GET /sindri/profile's answer: an approved profile (no summary) by default.</summary>
    public RouteAnswer ProfileAnswer { get; set; } = RouteAnswer.Api("profile", "approved");

    /// <summary>How many times the profile was read.</summary>
    public int ProfileCalls => Volatile.Read(ref _profileCalls);

    /// <summary>Held open until set: the profile approval waits for it (the app is sending meanwhile).</summary>
    public TaskCompletionSource? ApproveProfileGate { get; set; }

    /// <summary>Every profile approval body received, in order.</summary>
    public IReadOnlyList<ApproveProfileRequest> ProfileApprovals { get { lock (_gate) { return [.. _profileApprovals]; } } }

    public async Task<Result<ProfileApproved>> ApproveProfile(Uri api, ApproveProfileRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<ProfileApproved>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _profileApprovals.Add(request);
        }

        await WaitFor(ApproveProfileGate, cancellationToken).ConfigureAwait(false);
        return Answer<ProfileApproved>(api, "/sindri/profile/approve", ApproveProfileAnswer);
    }

    public Task<Result<ProfileRead>> ProfileSummary(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _profileCalls);
        return Task.FromResult(Answer<ProfileRead>(api, "/sindri/profile", ProfileAnswer));
    }
}
