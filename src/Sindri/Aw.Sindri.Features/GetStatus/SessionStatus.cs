namespace Aw.Sindri.Features.GetStatus;

/// <summary>The session collector's head counts in the status (collector spec CA-8).</summary>
/// <param name="Live">Live sessions in the last pass.</param>
/// <param name="Asking">The ones waiting on the human.</param>
/// <param name="Problems">Sources that failed in the last pass.</param>
/// <param name="Running">The ones working now (state running), as the Agents page counts them.</param>
public sealed record SessionStatus(int Live, int Asking, int Problems, int Running);
