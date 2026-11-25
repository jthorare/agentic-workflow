using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>
/// The graph tile and pill pens (#3234): every key is a token that exists, rings and borders read at 3:1 against the canvas, text at 4.5:1 on its fill, the focus ring is a separate ring outside the
/// selected one, and disabled and error look like themselves. Computed from Theme/Tokens.axaml, the way ContrastTests does.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GraphTilePensTests
{
    private const double RingMin = 3.0;
    private const double TextMin = 4.5;

    private static readonly TokenFile _tokens = TokenFile.Load();

    public static TheoryData<bool, int> Cases()
    {
        var data = new TheoryData<bool, int>();
        foreach (var pill in new[] { false, true })
        {
            foreach (var states in new[] { GraphTileStates.None, GraphTileStates.Hover, GraphTileStates.Focus, GraphTileStates.Selected, GraphTileStates.Disabled, GraphTileStates.Error, GraphTileStates.Selected | GraphTileStates.Focus | GraphTileStates.Hover, GraphTileStates.Error | GraphTileStates.Hover })
            {
                data.Add(pill, (int)states);
            }
        }

        return data;
    }

    private static GraphTilePaint Paint(bool pill, GraphTileStates states) => pill ? GraphPillPens.Paint(states) : GraphTilePens.Paint(states);

    private static IReadOnlyList<GraphRing> Rings(bool pill, GraphTileStates states) => pill ? GraphPillPens.Rings(states) : GraphTilePens.Rings(states);

    private static string Token(string brushKey) => brushKey["Brush.".Length..];

    private static string Label(bool pill, GraphTileStates states) => string.Concat(pill ? "pill" : "tile", " ", states.ToString());

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_key_a_state_names_is_a_brush_token_that_exists(bool pill, int stateBits)
    {
        var states = (GraphTileStates)stateBits;
        var paint = Paint(pill, states);
        var keys = new List<string> { paint.Fill, paint.Border, paint.Title, paint.Kind };
        keys.AddRange(Rings(pill, states).Select(r => r.Key));

        Assert.All(keys, key =>
        {
            Assert.StartsWith("Brush.", key, StringComparison.Ordinal);
            Assert.True(_tokens.Resources.ContainsKey(key), $"{Label(pill, states)} names {key}, which Tokens.axaml does not declare");
        });
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Rings_and_every_border_but_a_disabled_one_read_at_3_to_1_on_the_canvas(bool pill, int stateBits)
    {
        var states = (GraphTileStates)stateBits;
        var paint = Paint(pill, states);
        var keys = Rings(pill, states).Select(r => r.Key).ToList();
        if (!states.HasFlag(GraphTileStates.Disabled) && (states.HasFlag(GraphTileStates.Hover) || states.HasFlag(GraphTileStates.Error)))
        {
            keys.Add(paint.Border);
        }

        Assert.All(keys, key => Assert.True(_tokens.Contrast(Token(key), "Canvas") >= RingMin, string.Create(CultureInfo.InvariantCulture, $"{Label(pill, states)}: {key} is {_tokens.Contrast(Token(key), "Canvas"):F2} on Canvas, below {RingMin}")));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Text_on_the_fill_reads_at_4_5_to_1_unless_the_state_is_disabled(bool pill, int stateBits)
    {
        var states = (GraphTileStates)stateBits;
        var paint = Paint(pill, states);
        if (states.HasFlag(GraphTileStates.Disabled))
        {
            Assert.Equal("Brush.InkTertiary", paint.Title);
            return;
        }

        Assert.True(_tokens.Contrast(Token(paint.Title), Token(paint.Fill)) >= TextMin, $"{Label(pill, states)}: title {paint.Title} on {paint.Fill}");
        Assert.True(_tokens.Contrast(Token(paint.Kind), Token(paint.Fill)) >= TextMin, $"{Label(pill, states)}: secondary {paint.Kind} on {paint.Fill}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_focus_ring_is_a_separate_ring_outside_the_selected_one_and_both_show_together(bool pill)
    {
        var selected = Rings(pill, GraphTileStates.Selected).Single();
        var focus = Rings(pill, GraphTileStates.Focus).Single();
        var both = Rings(pill, GraphTileStates.Selected | GraphTileStates.Focus);

        Assert.NotEqual(selected.Key, focus.Key, StringComparer.Ordinal);
        Assert.True(focus.Gap > selected.Gap, "the focus ring sits outside the selected ring");
        Assert.Equal<GraphRing>([selected, focus], both);
        Assert.Empty(Rings(pill, GraphTileStates.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Disabled_is_dashed_and_error_is_the_error_fill_with_the_error_border(bool pill)
    {
        var disabled = Paint(pill, GraphTileStates.Disabled);
        var error = Paint(pill, GraphTileStates.Error);
        var rest = Paint(pill, GraphTileStates.None);

        Assert.True(disabled.Dashed);
        Assert.False(rest.Dashed);
        Assert.Equal("Brush.ErrorSoft", error.Fill);
        Assert.Equal("Brush.Error", error.Border);
        Assert.True(error.BorderThickness > rest.BorderThickness, "an error border is heavier than the rest border");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hover_lifts_the_border_and_a_disabled_tile_does_not_hover(bool pill)
    {
        var rest = Paint(pill, GraphTileStates.None);
        var hover = Paint(pill, GraphTileStates.Hover);
        var disabledHover = Paint(pill, GraphTileStates.Disabled | GraphTileStates.Hover);

        Assert.NotEqual(rest.Border, hover.Border, StringComparer.Ordinal);
        Assert.Equal(Paint(pill, GraphTileStates.Disabled), disabledHover);
    }
}
