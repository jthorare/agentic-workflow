using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>The session collector's head counts in the status (collector spec CA-8).</summary>
/// <param name="Live">Live sessions in the last pass.</param>
/// <param name="Asking">The ones waiting on the human.</param>
/// <param name="Problems">Sources that failed in the last pass; a test override (SND-SESSION-003) is not one (#4012).</param>
/// <param name="Running">The ones working now (state running), as the Agents page counts them.</param>
public sealed record SessionStatus(int Live, int Asking, int Problems, int Running)
{
    /// <summary>The problems the count counts, each with its code and sentence, for the System page's Agents card (#4012).</summary>
    public IReadOnlyList<SessionProblem> ProblemRows { get; init; } = [];

    /// <summary>"off" while the owner turned session watching off in launcher.json, "on" otherwise (#4316); a turned-off collector is no problem.</summary>
    public string Watching { get; init; } = "on";
}
