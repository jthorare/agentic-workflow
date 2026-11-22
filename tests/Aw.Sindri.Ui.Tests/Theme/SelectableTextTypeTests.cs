using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Linear;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>
/// A type class on a SelectableTextBlock renders like the same class on a TextBlock (#3785). An Avalonia type selector matches the
/// exact style key, so a `TextBlock.caption` style never reaches a SelectableTextBlock, and the copyable failed-open lines rendered at
/// body size.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SelectableTextTypeTests
{
    [Theory]
    [InlineData("caption")]
    [InlineData("mono")]
    public async Task A_selectable_text_block_with_a_type_class_renders_like_a_text_block_with_it(string name)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SelectableTextTypeTests).Assembly);

        var (selectable, plain) = await session.Dispatch(
            () =>
            {
                var copyable = new SelectableTextBlock { Text = "This file isn't on this machine." };
                copyable.Classes.Add(name);
                return (Rendered(copyable), Rendered(Classed(name)));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(plain, selectable);
    }

    // The Linear issue sheet's description and comment lines are LinearLineView, a SelectableTextBlock that adds "body" itself.
    [Fact]
    public async Task A_linear_line_renders_in_the_body_type()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SelectableTextTypeTests).Assembly);

        var (line, plain) = await session.Dispatch(
            () => (Rendered(new LinearLineView { Text = "Steps to reproduce" }), Rendered(Classed("body"))),
            TestContext.Current.CancellationToken);

        Assert.Equal(plain, line);
    }

    // Guard: every class a view puts on a SelectableTextBlock that has a `TextBlock.<class>` style also has a
    // `SelectableTextBlock.<class>` one (a selector list counts).
    [Fact]
    public void Every_class_on_a_selectable_text_block_with_a_text_block_style_has_a_selectable_one()
    {
        var docs = UiSource.Axaml.Select(f => (File: f, Doc: XDocument.Load(f))).ToList();
        var selectors = docs
            .SelectMany(d => d.Doc.Descendants().Where(e => string.Equals(e.Name.LocalName, "Style", StringComparison.Ordinal)))
            .Select(s => s.Attribute("Selector")?.Value ?? string.Empty)
            .SelectMany(s => s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.Ordinal);
        var uses = docs
            .SelectMany(d => d.Doc.Descendants()
                .Where(e => string.Equals(e.Name.LocalName, "SelectableTextBlock", StringComparison.Ordinal))
                .SelectMany(e => (e.Attribute("Classes")?.Value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Select(c => (Path.GetFileName(d.File), Class: c)));

        var missing = uses
            .Where(u => selectors.Contains("TextBlock." + u.Class) && !selectors.Contains("SelectableTextBlock." + u.Class))
            .Distinct()
            .Select(u => $"{u.Item1}: {u.Class}")
            .ToList();

        Assert.Empty(missing);
    }

    private static TextBlock Classed(string name)
    {
        var text = new TextBlock { Text = "This file isn't on this machine." };
        text.Classes.Add(name);
        return text;
    }

    private static TypeLook Rendered(TextBlock text)
    {
        var window = new Window { Content = text };
        window.Show();
        var look = new TypeLook(text.FontFamily.ToString(), text.FontSize, text.LineHeight, text.FontWeight, text.LetterSpacing);
        window.Close();
        return look;
    }

    private sealed record TypeLook(string Family, double Size, double LineHeight, FontWeight Weight, double LetterSpacing);
}
