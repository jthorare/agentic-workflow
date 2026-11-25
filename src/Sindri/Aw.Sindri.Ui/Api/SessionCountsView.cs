using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.Api;

/// <summary>The session collector's counts in the status (collector spec CA-8).</summary>
/// <param name="Live">Live agent sessions in the last pass.</param>
/// <param name="Asking">The ones waiting on the human.</param>
/// <param name="Problems">Sources that failed in the last pass.</param>
/// <param name="Running">The ones working now; absent from an older answer, which the System page then reads as the live count.</param>
/// <param name="ProblemRows">The problems the count counts, each with its code and sentence (#4012); absent from an older answer.</param>
/// <param name="Watching">"off" while the owner turned session watching off, "on" otherwise (#4316); absent from an older answer, read as on.</param>
internal sealed record SessionCountsView(int Live, int Asking, int Problems, int? Running = null, IReadOnlyList<SessionProblemView>? ProblemRows = null, string? Watching = null);
