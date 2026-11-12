using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Target box's reader (issue 3240): blank is no target, a date word reads, anything else is unreadable. The forms themselves are tested with DueWords.</summary>
[Lane(TestLane.Unit)]
public sealed class TargetDateReaderTests
{
    private static readonly DateOnly _thursday = new(2026, 11, 12);

    [Theory]
    [InlineData("Dec 5", 2026, 12, 5)]
    [InlineData("next fri", 2026, 11, 13)]
    [InlineData("2026-12-05", 2026, 12, 5)]
    public void A_readable_form_is_a_date(string text, int year, int month, int day)
    {
        var reading = TargetDateReader.Read(text, _thursday);

        Assert.Equal((TargetReadKind.Date, new DateOnly(year, month, day)), (reading.Kind, reading.Date));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blank_text_is_no_target(string? text) => Assert.Equal(TargetReadKind.Empty, TargetDateReader.Read(text, _thursday).Kind);

    [Theory]
    [InlineData("soonish")]
    [InlineData("feb 30")]
    public void Text_that_is_not_a_date_is_unreadable(string text) => Assert.Equal(TargetReadKind.Unreadable, TargetDateReader.Read(text, _thursday).Kind);
}
