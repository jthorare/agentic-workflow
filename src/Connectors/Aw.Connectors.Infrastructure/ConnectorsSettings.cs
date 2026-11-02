namespace Aw.Connectors.Infrastructure;

/// <summary>Connectors's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Connectors keeps its state: $AW_STATE_DIR/connectors, or ~/.agentic-workflow/connectors.</param>
internal sealed record ConnectorsSettings(string StateDirectory);
