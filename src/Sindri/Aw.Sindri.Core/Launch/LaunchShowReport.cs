namespace Aw.Sindri.Core.Launch;

/// <summary>What <c lang="bash">sindri launch show</c> prints.</summary>
/// <param name="Terminal">The viewer in use ('Terminal: kitty (exec-template)').</param>
/// <param name="TerminalSource">'file' or 'detected'.</param>
/// <param name="ConfigPath">The launcher.json path (also when the file is missing).</param>
/// <param name="Entries">The catalog.</param>
/// <param name="Problems">Config problems, dropped entries and permission warnings.</param>
public sealed record LaunchShowReport(string Terminal, string TerminalSource, string ConfigPath, IReadOnlyList<LaunchShowEntry> Entries, IReadOnlyList<string> Problems);
