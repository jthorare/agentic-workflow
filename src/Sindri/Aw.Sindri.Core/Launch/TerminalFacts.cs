namespace Aw.Sindri.Core.Launch;

/// <summary>What the probe found about the configured terminal viewer (launcher L-4): 'launch.probed''s 'terminal'.</summary>
/// <param name="Kind">The viewer kind ('warp', 'exec-template', 'print').</param>
/// <param name="Label">What a person calls it ('Warp').</param>
/// <param name="Present">True when it can open a window here.</param>
/// <param name="Source">'file' when launcher.json chose it, else 'detected'.</param>
/// <param name="ConfigPath">The resolved launcher.json path (also when the file is missing), or null.</param>
/// <param name="Problem">Why it can't open ('not-installed', 'no-gui-session', 'config-invalid'), or null.</param>
/// <param name="Detail">For 'config-invalid': the line and reason, or null.</param>
public sealed record TerminalFacts(string Kind, string Label, bool Present, string Source, string? ConfigPath, string? Problem, string? Detail);
