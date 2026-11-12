using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Target box's reader (issue 3240): each form people type, the year a month-day gets, relative words from today, and text it can't read.</summary>
[Lane(TestLane.Unit)]
public sealed class TargetDateReaderTests
{
    private static readonly DateOnly _thursday = new(2026, 11, 12);

    [Theory]
    [InlineData("2026-12-05", "2026-12-05")]
    [InlineData(" 2026-12-05 ", "2026-12-05")]
    [InlineData("Dec 5", "2026-12-05")]
    [InlineData("dec 5", "2026-12-05")]
    [InlineData("5 Dec", "2026-12-05")]
    [InlineData("December 5", "2026-12-05")]
    [InlineData("dec 5 2026", "2026-12-05")]
    [InlineData("Dec 5, 2027", "2027-12-05")]
    [InlineData("jan 3", "2027-01-03")]
    [InlineData("nov 12", "2026-11-12")]
    [InlineData("nov 11", "2027-11-11")]
    [InlineData("today", "2026-11-12")]
    [InlineData("Tomorrow", "2026-11-13")]
    [InlineData("fri", "2026-11-13")]
    [InlineData("next fri", "2026-11-13")]
    [InlineData("Next Friday", "2026-11-13")]
    [InlineData("thu", "2026-11-19")]
    [InlineData("next mon", "2026-11-16")]
    public void A_form_people_type_reads_as_its_date(string text, string expected)
    {
        var reading = TargetDateReader.Read(text, _thursday);

        Assert.Equal((TargetReadKind.Date, DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture)), (reading.Kind, reading.Date));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blank_text_is_no_target(string? text) => Assert.Equal(TargetReadKind.Empty, TargetDateReader.Read(text, _thursday).Kind);

    [Theory]
    [InlineData("soonish")]
    [InlineData("2026-13-01")]
    [InlineData("03/02/2026")]
    [InlineData("dec 32")]
    [InlineData("feb 30")]
    [InlineData("next")]
    [InlineData("next dec")]
    public void Text_that_is_not_a_date_is_unreadable(string text) => Assert.Equal(TargetReadKind.Unreadable, TargetDateReader.Read(text, _thursday).Kind);
}
