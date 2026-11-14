using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;
using Aw.Sindri.Ui.Views.Icons;

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>
/// The field error box (#3234): a failed field draws its input's border red and an error line under it. The error text holds 4.5:1 and the
/// border 3:1 (WCAG 1.4.3, 1.4.11) against the panel and the input; the line carries a ✕ and its words, never colour alone; the screen reader
/// reads the line as text and the input by its caption.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class FieldErrorTests
{
    private const string Sentence = "Short ids use lowercase letters, numbers and dashes.";

    private static readonly List<Window> _open = [];

    // Each test closes its windows and drains the dispatcher before the session tears the app down: a render queued by the last UpdateLayout otherwise runs during that teardown, when the
    // Inter collection is already gone, and an empty text line (which has no fallback font) throws "Could not create glyphTypeface".
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(FieldErrorTests).Assembly).Dispatch(
            () =>
            {
                try
                {
                    return body();
                }
                finally
                {
                    foreach (var window in _open)
                    {
                        window.Close();
                    }

                    _open.Clear();
                    Dispatcher.UIThread.RunJobs();
                }
            },
            TestContext.Current.CancellationToken);

    private static Window Track(Window window)
    {
        _open.Add(window);
        return window;
    }

    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }

    private static double Contrast(Color a, Color b)
    {
        var (x, y) = (Luminance(a), Luminance(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private static Color Solid(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : throw new InvalidOperationException("a solid brush was expected");

    private static Color Token(StyledElement from, string key) => Solid(from.FindResource(key) as IBrush);

    // A field on the sheet's panel (Surface1), shown in a headless window and laid out.
    private static (Window Window, LabelledField Field, TextBox Input, Border Panel) Show(string? error)
    {
        var input = new TextBox { Text = "Evening Check" };
        var field = new LabelledField { Caption = "Short id", Content = input, ErrorText = error };
        var panel = new Border { Padding = new Thickness(16), Width = 420, Child = field };
        var window = Track(new Window { Content = panel, Width = 480, Height = 200 });
        panel.Background = new SolidColorBrush(Token(window, "Brush.Surface1"));
        window.Show();
        window.UpdateLayout();
        return (window, field, input, panel);
    }

    private static Border Border(TextBox input) =>
        input.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement");

    [Fact]
    public async Task A_failed_field_draws_its_input_border_in_Error_at_3_to_1_against_the_panel_and_the_input()
    {
        var (border, error, panel, fill) = await OnUi(() =>
        {
            var (window, _, input, panelBorder) = Show(Sentence);
            return (Solid(Border(input).BorderBrush), Token(window, "Brush.Error"), Solid(panelBorder.Background), Solid(input.Background));
        });

        Assert.Equal(error, border);
        Assert.True(Contrast(border, panel) >= 3.0, string.Create(CultureInfo.InvariantCulture, $"border on the panel is {Contrast(border, panel):F2}:1"));
        Assert.True(Contrast(border, fill) >= 3.0, string.Create(CultureInfo.InvariantCulture, $"border on the input is {Contrast(border, fill):F2}:1"));
    }

    [Fact]
    public async Task The_border_stays_red_while_the_input_has_focus()
    {
        var (border, error) = await OnUi(() =>
        {
            var (window, _, input, _) = Show(Sentence);
            _ = input.Focus(NavigationMethod.Tab);
            window.UpdateLayout();
            return (Solid(Border(input).BorderBrush), Token(window, "Brush.Error"));
        });

        Assert.Equal(error, border);
    }

    [Fact]
    public async Task The_error_line_reads_in_Error_at_4_5_to_1_after_a_cross_and_its_words()
    {
        var (text, colour, panel, glyph, shown) = await OnUi(() =>
        {
            var (window, field, _, panelBorder) = Show(Sentence);
            var line = field.GetVisualDescendants().OfType<FieldError>().Single();
            var words = line.GetVisualDescendants().OfType<TextBlock>().Single();
            return (words.Text, Solid(words.Foreground), Solid(panelBorder.Background), line.GetVisualDescendants().OfType<StateIcon>().Any(), line.IsEffectivelyVisible);
        });

        Assert.True(shown);
        Assert.Equal(Sentence, text);
        Assert.True(glyph, "the line has a cross glyph, so colour is never the only signal");
        Assert.True(Contrast(colour, panel) >= 4.5, string.Create(CultureInfo.InvariantCulture, $"the error text on the panel is {Contrast(colour, panel):F2}:1"));
    }

    [Fact]
    public async Task A_field_with_no_error_draws_no_line_and_no_red_border()
    {
        var (shown, height, border, error) = await OnUi(() =>
        {
            var (window, field, input, _) = Show(error: null);
            var line = field.GetVisualDescendants().OfType<FieldError>().Single();
            return (line.IsEffectivelyVisible, line.Bounds.Height, Solid(Border(input).BorderBrush), Token(window, "Brush.Error"));
        });

        Assert.False(shown);
        Assert.Equal(0, height);
        Assert.NotEqual(error, border);
    }

    [Fact]
    public async Task The_error_clears_when_the_sentence_does()
    {
        var (shown, border, error) = await OnUi(() =>
        {
            var (window, field, input, _) = Show(Sentence);
            field.ErrorText = null;
            window.UpdateLayout();
            return (field.GetVisualDescendants().OfType<FieldError>().Single().IsEffectivelyVisible, Solid(Border(input).BorderBrush), Token(window, "Brush.Error"));
        });

        Assert.False(shown);
        Assert.NotEqual(error, border);
    }

    [Fact]
    public async Task The_screen_reader_reads_the_line_as_text_and_the_input_by_its_caption()
    {
        var (lineName, lineType, inputName, captionHidden) = await OnUi(() =>
        {
            var (_, field, input, _) = Show(Sentence);
            var line = field.GetVisualDescendants().OfType<FieldError>().Single();
            var caption = field.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Short id");
            return (AutomationProperties.GetName(line), AutomationProperties.GetControlTypeOverride(line), AutomationProperties.GetName(input),
                AutomationProperties.GetAccessibilityView(caption) == AccessibilityView.Raw);
        });

        Assert.Equal(Sentence, lineName);
        Assert.Equal(Avalonia.Automation.Peers.AutomationControlType.Text, lineType);
        Assert.Equal("Short id", inputName);
        Assert.True(captionHidden, "the caption is the input's name, so it is not read twice");
    }

    [Fact]
    public async Task A_lone_error_line_shows_its_sentence_and_hides_when_empty()
    {
        var (full, empty) = await OnUi(() =>
        {
            var line = new FieldError { Text = "Couldn't save: the workflow changed." };
            var window = Track(new Window { Content = line, Width = 400, Height = 100 });
            window.Show();
            window.UpdateLayout();
            var shown = line.IsEffectivelyVisible && line.Bounds.Height > 0;
            line.Text = null;
            window.UpdateLayout();
            return (shown, line.IsEffectivelyVisible);
        });

        Assert.True(full);
        Assert.False(empty);
    }

    [Fact]
    public async Task A_plain_TextBox_with_the_field_invalid_class_draws_the_same_red_border()
    {
        var (border, error) = await OnUi(() =>
        {
            var input = new TextBox();
            input.Classes.Add("field-invalid");
            var window = Track(new Window { Content = input, Width = 300, Height = 100 });
            window.Show();
            window.UpdateLayout();
            return (Solid(Border(input).BorderBrush), Token(window, "Brush.Error"));
        });

        Assert.Equal(error, border);
    }
}
