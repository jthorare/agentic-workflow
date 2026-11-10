namespace Aw.Sindri.Core.Reviews;

/// <summary>The one connect state of the Reviews list: no GitHub connection (SND-REVIEW-008) or one that needs sign-in (SND-REVIEW-009).</summary>
/// <param name="Code">"SND-REVIEW-008" or "SND-REVIEW-009".</param>
/// <param name="Action">"connect" or "sign-in-again".</param>
/// <param name="Text">"Connect GitHub" or "Sign in to GitHub again".</param>
/// <param name="Connection">The connection to sign in again ('github:work'); null when there is none.</param>
public sealed record ReviewConnect(string Code, string Action, string Text, string? Connection);
