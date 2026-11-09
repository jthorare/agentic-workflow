using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The profile routes of the fake (the summary): each answers a golden, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private int _profileCalls;

    /// <summary>GET /sindri/profile's answer: an approved profile (no summary) by default.</summary>
    public RouteAnswer ProfileAnswer { get; set; } = RouteAnswer.Api("profile", "approved");

    /// <summary>How many times the profile was read.</summary>
    public int ProfileCalls => Volatile.Read(ref _profileCalls);

    public Task<Result<ProfileRead>> ProfileSummary(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _profileCalls);
        return Task.FromResult(Answer<ProfileRead>(api, "/sindri/profile", ProfileAnswer));
    }
}
