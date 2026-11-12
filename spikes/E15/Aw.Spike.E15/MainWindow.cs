using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;

namespace Aw.Spike.E15;

internal sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "E15 editor spike";
        Width = 900;
        Height = 700;
        Editor = new TextEditor
        {
            FontFamily = new FontFamily("Cascadia Code,Menlo,DejaVu Sans Mono,monospace"),
            FontSize = 14,
            ShowLineNumbers = false,
            WordWrap = true,
        };
        Editor.Document = new AvaloniaEdit.Document.TextDocument(Fixtures.Demo);
        Live = new LiveRendering(Editor, 14);
        Live.Set(true);
        Content = Editor;
    }

    public TextEditor Editor { get; }

    public LiveRendering Live { get; }
}
