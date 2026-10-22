namespace Aw.Forge.Infrastructure;

/// <summary>Forge's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Forge keeps its state: $AW_STATE_DIR/forge, or ~/.agentic-workflow/forge.</param>
/// <param name="Login">The login the file-backed forge acts as (it posts replies as this account and answers Self with it): AW_FORGE_LOGIN, default "sindri".</param>
internal sealed record ForgeSettings(string StateDirectory, string Login);
