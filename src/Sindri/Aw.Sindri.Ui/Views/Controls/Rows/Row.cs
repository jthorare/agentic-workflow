using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls.Primitives;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// One row of a <see cref="RowList"/> (#2978): glyph, id and title, one cell per list column, age and who it waits on. Its data context is an
/// IRowItem. Classes: cursor (keyboard focus), sel (the row being peeked or open). Stub: it draws nothing yet.
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "Stub of the red commit (#2978); the control commit uses it.")]
internal sealed class Row : TemplatedControl;
