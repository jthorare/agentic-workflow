namespace Aw.Sindri.Core.Launch.Argv;

/// <summary>The values of the closed placeholder set (launcher spec section 4) for one expansion; a null value is a placeholder the caller has none for.</summary>
/// <param name="Bin">'{bin}'.</param>
/// <param name="Cwd">'{cwd}'; absolute.</param>
/// <param name="Title">'{title}'.</param>
/// <param name="Name">'{name}'.</param>
/// <param name="Seed">'{seed}'.</param>
/// <param name="SessionId">'{sessionId}', or null.</param>
/// <param name="McpConfig">'{mcpConfig}', or null.</param>
/// <param name="Cmd">'{cmd...}': the host's attach argv, spliced as separate arguments, or null.</param>
public sealed record PlaceholderValues(
    string Bin,
    string Cwd,
    string Title,
    string Name,
    string Seed,
    Sessions.SessionId? SessionId = null,
    string? McpConfig = null,
    IReadOnlyList<string>? Cmd = null);
