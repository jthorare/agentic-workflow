using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>
/// Mono text reads as typed (#4087). JetBrains Mono joins "=>", "&lt;=" and "!=" into one glyph through its 'calt' and 'liga' font features, so a
/// code step's body on the run page showed arrows where the code had two characters. Every mono style (the run page's Code, Input, Output and
/// "What the run produced" blocks are SelectableTextBlock.mono) turns both features off.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class MonoLigatureTests
{
    [Theory]
    [InlineData("TextBlock", "mono")]
    [InlineData("SelectableTextBlock", "mono")]
    [InlineData("TextBlock", "kbd")]
    [InlineData("TextBox", "typed-field")]
    public async Task Mono_text_turns_ligatures_and_contextual_alternates_off(string control, string name)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(MonoLigatureTests).Assembly);

        var off = await session.Dispatch(() => FeaturesOff(Make(control), name), TestContext.Current.CancellationToken);

        Assert.Equal(["calt", "liga"], off, StringComparer.Ordinal);
    }

    private static Control Make(string control) => control switch
    {
        "SelectableTextBlock" => new SelectableTextBlock { Text = "x => x <= 3" },
        "TextBox" => new TextBox { Text = "x => x <= 3" },
        _ => new TextBlock { Text = "x => x <= 3" },
    };

    private static string[] FeaturesOff(Control text, string name)
    {
        text.Classes.Add(name);
        var window = new Window { Content = text };
        window.Show();
        var features = text switch
        {
            TextBlock block => block.FontFeatures,
            TemplatedControl box => box.FontFeatures,
            _ => null,
        };
        window.Close();
        return features?.Where(static f => f.Value == 0).Select(static f => f.Tag).Order(StringComparer.Ordinal).ToArray() ?? [];
    }
}
