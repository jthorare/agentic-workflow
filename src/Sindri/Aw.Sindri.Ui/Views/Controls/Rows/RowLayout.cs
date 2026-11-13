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
    int LayoutsBuilt);
