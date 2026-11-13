using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>Draws the text of a flat <see cref="Row"/> (#3189) from the layouts Prepare built: the title line, the facts, the age and who the row waits on.</summary>
internal sealed partial class Row
{
    private void DrawContent(DrawingContext context, IRowItem item, double height)
    {
        DrawTitleLine(context, height);
        DrawFacts(context, item, height);
        Put(context, _text[Age], _slots.AgeX, height);
        Put(context, _text[Wait], _slots.WaitX, height);
    }

    // The id and the sub-line keep their width, the title takes what is left and ends with an ellipsis, so all three stay inside the title column without a clip. No clip is pushed anywhere
    // in this Render: at a render scaling of 2 a clip pushed and popped left every later draw of the Render empty.
    private void DrawTitleLine(DrawingContext context, double height)
    {
        var x = _slots.TitleX;
        var id = Built(Id);
        Put(context, id, x, height);
        x += id.Width + TitleGap;
        var title = Built(Title);
        Put(context, title, x, height);
        x += title.Width + TitleGap;
        Put(context, _text[Sub], x, height);
    }
}
