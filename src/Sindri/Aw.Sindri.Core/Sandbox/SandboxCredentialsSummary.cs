namespace Aw.Sindri.Core.Sandbox;

/// <summary>The credentials in a session besides the model login.</summary>
/// <param name="Github">None or the agent App token.</param>
public sealed record SandboxCredentialsSummary(GithubCredentials Github);
