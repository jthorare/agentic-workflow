namespace Aw.Sindri.Features.Scrub;

/// <summary>One 'sindri scrub' run (cutover spec §6.2): redact a text, scan the staged diff, or install the pre-commit hook.</summary>
/// <param name="Mode">The form.</param>
/// <param name="Text">The standard input; read for <see cref="ScrubMode.Stdin"/> only.</param>
/// <param name="Repo">The repo the staged scan or the install acts on; null is the current directory.</param>
/// <param name="Bin">The path baked into the installed hook; null is the running binary's own. <see cref="ScrubMode.InstallPreCommit"/> only.</param>
public sealed record ScrubRequest(ScrubMode Mode, string Text = "", string? Repo = null, string? Bin = null);
