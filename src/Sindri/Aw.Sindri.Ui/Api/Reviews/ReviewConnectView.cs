namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The Reviews tab's one action when GitHub isn't connected or needs a new sign-in (#2499): it opens the Connections page.</summary>
/// <param name="Code">SND-REVIEW-008 (no connection) or SND-REVIEW-009 (sign in again).</param>
/// <param name="Action">"connect" or "sign-in-again".</param>
/// <param name="Text">The button's words ("Connect GitHub", "Sign in to GitHub again").</param>
/// <param name="Connection">The connection to open, such as "github:work"; null when there is none yet.</param>
internal sealed record ReviewConnectView(string Code, string Action, string Text, string? Connection);
