namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>The credentials in a session besides the model login.</summary>
/// <param name="Github">'none' or 'agent-app'.</param>
internal sealed record LaunchCredentialsView(string Github);
