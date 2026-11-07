namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>201 of POST /sindri/linear/issues.</summary>
/// <param name="Outcome">"applied".</param>
/// <param name="ClientId">The app's client id, echoed.</param>
/// <param name="Issue">The new issue's row.</param>
internal sealed record LinearCreated(string Outcome, string ClientId, LinearIssueRow Issue) : LinearWriteOutcome;
