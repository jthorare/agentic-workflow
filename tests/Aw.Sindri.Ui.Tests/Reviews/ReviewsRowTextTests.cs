using Aw.Kernel.Testing;
using static Aw.Sindri.Ui.Tests.Reviews.ReviewsFixture;

namespace Aw.Sindri.Ui.Tests.Reviews;

/// <summary>Reviews list words (issue #2791): a row with no required-check count says so, and a failed read names no port or code in the line.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewsRowTextTests
{
    private readonly ReviewsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Row_with_no_required_text_shows_no_required_checks()
    {
        var list = Golden("some");
        var row = list["groups"]![0]!["sections"]![0]!["rows"]![0]!;
        row["requiredOpen"] = null;
        row["requiredText"] = null;
        _f.Serve(list);

        var page = await _f.Shown(Token);

        Assert.Equal("No required checks", page.Groups[0].Sections[0].Rows[0].Required);
    }

    [Fact]
    public async Task ReviewsFailed_names_no_port_or_code()
    {
        var page = await _f.Shown(Token);
        _f.Server.GoDown();

        await page.Refresh(Url, Token);

        Assert.DoesNotContain("127.0.0.1", page.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("SND-UI-002", page.Error, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1", page.ErrorDetail, StringComparison.Ordinal);
        Assert.Contains("SND-UI-002", page.ErrorDetail, StringComparison.Ordinal);
    }
}
