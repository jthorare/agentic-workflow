namespace Aw.Sindri.Ui.Desktop;

/// <summary>A program to start detached, with where its output goes.</summary>
/// <param name="Executable">The program's path.</param>
/// <param name="Arguments">Its arguments.</param>
/// <param name="LogPath">The file its stdout and stderr are appended to.</param>
internal sealed record ProcessLaunch(string Executable, IReadOnlyList<string> Arguments, string LogPath);
