namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The viewer detection chose: <c lang="text">print</c>, <c lang="text">warp</c> or <c lang="text">exec-template</c> (with its template).</summary>
/// <param name="Kind">The viewer kind.</param>
/// <param name="Label">What the app names it ('print', 'Warp', 'kitty').</param>
/// <param name="Template">The terminal's row for an exec-template, else null.</param>
/// <param name="Reason">Why, in a few words.</param>
internal sealed record DetectedTerminal(string Kind, string Label, TerminalTemplate? Template, string Reason);
