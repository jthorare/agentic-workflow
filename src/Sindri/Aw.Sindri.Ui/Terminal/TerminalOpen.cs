namespace Aw.Sindri.Ui.Terminal;

/// <summary>Which session's terminal socket to open, and the size the view fits (one value, so Connect and Run stay inside AW1006's five parameters).</summary>
/// <param name="Api">The base URL.</param>
/// <param name="Provider">The session's provider.</param>
/// <param name="Id">The session's id.</param>
/// <param name="Cols">The columns the view fits.</param>
/// <param name="Rows">The rows the view fits.</param>
internal sealed record TerminalOpen(Uri Api, string Provider, string Id, int Cols, int Rows);
