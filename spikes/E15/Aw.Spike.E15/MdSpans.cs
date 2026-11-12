using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Aw.Spike.E15;

internal enum MdKind { Heading, ListItem, Task, TaskDone, Table, CodeBlock, CodeInline, Bold, Italic, Quote, Link }

internal readonly record struct MdSpan(MdKind Kind, int Start, int End, int Level);

/// <summary>Markdig parse to a flat span list (document offsets, End exclusive). The parse is lazy: a change marks it stale, the next reader re-parses.</summary>
internal sealed class MdSpans
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseTaskLists().Build();
    private readonly AvaloniaEdit.Document.TextDocument _document;
    private MdSpan[] _spans = [];
    private bool _stale = true;

    public MdSpans(AvaloniaEdit.Document.TextDocument document)
    {
        _document = document;
        document.Changed += (_, _) => _stale = true;
    }

    public double LastParseMs { get; private set; }

    public MdSpan[] Current
    {
        get
        {
            if (_stale)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                _spans = Parse(_document.Text);
                LastParseMs = sw.Elapsed.TotalMilliseconds;
                _stale = false;
            }
            return _spans;
        }
    }

    private static MdSpan[] Parse(string text)
    {
        var doc = Markdown.Parse(text, Pipeline);
        var list = new List<MdSpan>();
        foreach (var o in doc.Descendants())
        {
            var s = o.Span;
            if (s.IsEmpty)
            {
                continue;
            }
            var end = Math.Min(s.End + 1, text.Length);
            switch (o)
            {
                case HeadingBlock h: list.Add(new(MdKind.Heading, s.Start, end, h.Level)); break;
                case ListItemBlock: list.Add(new(MdKind.ListItem, s.Start, end, 0)); break;
                case Markdig.Extensions.Tables.Table: list.Add(new(MdKind.Table, s.Start, end, 0)); break;
                case QuoteBlock: list.Add(new(MdKind.Quote, s.Start, end, 0)); break;
                case CodeBlock: list.Add(new(MdKind.CodeBlock, s.Start, end, 0)); break;
                case CodeInline: list.Add(new(MdKind.CodeInline, s.Start, end, 0)); break;
                case LinkInline: list.Add(new(MdKind.Link, s.Start, end, 0)); break;
                case TaskList t: list.Add(new(t.Checked ? MdKind.TaskDone : MdKind.Task, s.Start, end, 0)); break;
                case EmphasisInline e:
                    list.Add(new(e.DelimiterCount >= 2 ? MdKind.Bold : MdKind.Italic, s.Start, end, 0));
                    break;
            }
        }
        list.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        return [.. list];
    }
}
