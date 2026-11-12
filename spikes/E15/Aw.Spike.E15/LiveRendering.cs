using AvaloniaEdit;

namespace Aw.Spike.E15;

/// <summary>Installs or removes the three live-rendering parts (colorizer, background, chip generator) on an editor.</summary>
internal sealed class LiveRendering
{
    private readonly TextEditor _editor;
    private readonly MdSpans _spans;
    private readonly MarkdownColorizer _colorizer;
    private readonly MarkdownBackground _background;
    private readonly ChipGenerator _chips;
    private bool _on;

    public LiveRendering(TextEditor editor, double baseSize)
    {
        _editor = editor;
        _spans = new MdSpans(editor.Document);
        _colorizer = new MarkdownColorizer(_spans, baseSize);
        _background = new MarkdownBackground(_spans);
        _chips = new ChipGenerator(editor);
    }

    public MdSpans Spans => _spans;

    public void Set(bool on)
    {
        if (on == _on)
        {
            return;
        }
        var tv = _editor.TextArea.TextView;
        if (on)
        {
            tv.LineTransformers.Add(_colorizer);
            tv.BackgroundRenderers.Add(_background);
            tv.ElementGenerators.Add(_chips);
        }
        else
        {
            tv.LineTransformers.Remove(_colorizer);
            tv.BackgroundRenderers.Remove(_background);
            tv.ElementGenerators.Remove(_chips);
        }
        _on = on;
        tv.Redraw();
    }
}
