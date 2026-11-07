namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>200 of POST /sindri/linear/issues/{key}/comments.</summary>
/// <param name="Outcome">"applied".</param>
/// <param name="ClientId">The app's client id, echoed.</param>
/// <param name="Comment">The posted comment.</param>
/// <param name="Slow">True when it landed only on the re-read ("Posted (Linear was slow to answer)").</param>
internal sealed record LinearCommentPosted(string Outcome, string ClientId, LinearComment Comment, bool Slow) : LinearWriteOutcome;
