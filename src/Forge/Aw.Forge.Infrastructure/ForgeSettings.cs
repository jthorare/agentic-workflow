namespace Aw.Forge.Infrastructure;

/// <summary>Forge's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Forge keeps its state: $AW_STATE_DIR/forge, or ~/.agentic-workflow/forge.</param>
internal sealed record ForgeSettings(string StateDirectory);
