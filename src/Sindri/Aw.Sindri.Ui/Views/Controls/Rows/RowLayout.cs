using Avalonia;
using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>What the flat Row has drawn (#3189): the texts, their ink, the chips, and how many text layouts it built. A test hook, read by the Frames tests; not a UI surface.</summary>
/// <param name="Id">The id text.</param>
/// <param name="Title">The title text.</param>
/// <param name="Sub">The sub-line text.</param>
/// <param name="Age">The age text.</param>
/// <param name="Wait">The waits-on text.</param>
/// <param name="Facts">The fact texts of the visible columns, in column order.</param>
/// <param name="Chips">The chips the row drew.</param>
/// <param name="TitleInk">The title's brush.</param>
/// <param name="IdInk">The id's brush.</param>
/// <param name="CaptionSize">The font size the caption texts were laid out at.</param>
/// <param name="LayoutsBuilt">How many text layouts this row built since it was made.</param>
/// <param name="SubInk">The sub-line's brush.</param>
/// <param name="Cells">The fact cells of the visible columns.</param>
/// <param name="AgeRect">Where the age is drawn, in the row's coordinates.</param>
/// <param name="TitleRect">Where the title is drawn: its x and its laid-out width.</param>
/// <param name="TitleTrimmed">True when the title is cut with an ellipsis.</param>
/// <param name="TitleDesiredWidth">How wide the whole title would be.</param>
/// <param name="SelBar">The selection bar's rectangle, or null when the row is not selected.</param>
/// <param name="SelBarInk">The selection bar's brush.</param>
/// <param name="Ring">True when the cursor ring is drawn.</param>
/// <param name="RingRect">The cursor ring's outer rectangle (its stroke is inside it), or null when the row has no cursor.</param>
/// <param name="SubRect">Where the sub-line is drawn (its x and laid-out width), or null when the row has none.</param>
/// <param name="WaitRect">The waits-on slot, in the row's coordinates.</param>
/// <param name="TitleSlot">The title column: from where the id starts to where the title and sub-line must end.</param>
internal sealed record RowLayout(
    string Id,
    string Title,
    string Sub,
    string Age,
    string Wait,
    IReadOnlyList<string> Facts,
    IReadOnlyList<RowChip> Chips,
    IBrush? TitleInk,
    IBrush? IdInk,
    double CaptionSize,
    int LayoutsBuilt,
    IBrush? SubInk,
    IReadOnlyList<RowCell> Cells,
    Rect AgeRect,
    Rect TitleRect,
    bool TitleTrimmed,
    double TitleDesiredWidth,
    Rect? SelBar,
    IBrush? SelBarInk,
    bool Ring,
    Rect? RingRect,
    Rect? SubRect,
    Rect WaitRect,
    Rect TitleSlot);
