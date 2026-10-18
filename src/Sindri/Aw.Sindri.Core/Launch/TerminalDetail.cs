namespace Aw.Sindri.Core.Launch;

/// <summary>Why a terminal probe has a problem (launcher spec L-4, UI §6.6): <c lang="text">terminal.detail {line, reason}</c>.</summary>
/// <param name="Line">The launcher.json line the reader pointed at, or null when the problem has none.</param>
/// <param name="Reason">What is wrong, in a sentence.</param>
public sealed record TerminalDetail(int? Line, string Reason);
