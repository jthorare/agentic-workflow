namespace Aw.Sindri.Ui.Terminal;

/// <summary>One server message, read.</summary>
/// <param name="Kind">Which message.</param>
/// <param name="Rows">A frame's ANSI lines; empty for the others.</param>
/// <param name="Status">A frame's cursor, size and pane state; unknown for the others.</param>
/// <param name="Seq">A frame's sequence number.</param>
internal sealed record TerminalServerMessage(TerminalServerKind Kind, IReadOnlyList<string> Rows, TerminalStatus Status, int Seq);
