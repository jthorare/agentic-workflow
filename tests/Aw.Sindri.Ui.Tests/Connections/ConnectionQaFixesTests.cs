using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>qa's review of the Connections page (#2537): a Custom section with a label and a count, a custom row's status as a plain word, and a tile's reason as a sentence with the provider's code in its tooltip.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionQaFixesTests
{
    private readonly ConnectionsFixture _f = new();

    [Fact]
    public async Task The_custom_section_has_a_label_and_a_count_and_hides_with_no_custom_connections()
    {
        var none = await _f.Shown();
        _f.SeedThree();
        var some = await _f.Shown();

        Assert.False(none.ShowsCustom);
        Assert.True(some.ShowsCustom);
        Assert.Equal(("Custom", "2"), (some.CustomLabel, some.CustomCount));
    }

    [Fact]
    public async Task A_custom_rows_status_is_a_plain_word_without_a_glyph()
    {
        _f.SeedThree();

        var page = await _f.Shown();

        Assert.Equal(["Error", "Connected", "Needs sign-in"], page.Rows.Select(r => r.ChipText), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_refused_refresh_reads_as_a_sentence_with_the_service_name_and_the_code_in_the_tooltip()
    {
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work", "needs-sign-in", "oauth", "the provider refused the refresh (invalid_grant)"));
        var page = await _f.Shown();
        var tile = page.Tiles.Single(t => string.Equals(t.Id, "linear", StringComparison.Ordinal));

        Assert.Equal(("Linear refused the sign-in refresh. Reconnect to sign in again.", "invalid_grant"), (tile.ReasonText, tile.ReasonTip));
    }

    [Fact]
    public async Task Any_other_reason_is_capitalised_and_ends_with_a_full_stop()
    {
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("github:work", "needs-sign-in", "oauth", "the token expired"));
        var page = await _f.Shown();
        var tile = page.Tiles.Single(t => string.Equals(t.Id, "github", StringComparison.Ordinal));

        Assert.Equal(("The token expired.", string.Empty), (tile.ReasonText, tile.ReasonTip));
    }
}
