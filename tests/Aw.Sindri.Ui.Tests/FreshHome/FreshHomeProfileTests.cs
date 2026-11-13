using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.FreshHome;

/// <summary>A brand-new home has no profile: the health strip and the top pill say "not set up", not unreadable or Healthy (issue 3113, part of 3019).</summary>
[Lane(TestLane.Unit)]
public sealed class FreshHomeProfileTests
{
    private static ProfileView Missing => new(Live: null, Problem: "No profile found", Source: null, ProblemKind: "missing");

    [Fact]
    public void A_missing_profile_reads_not_set_up_not_unreadable()
    {
        Assert.Equal("not set up", StatusText.ProfileState(Missing));
        Assert.Equal("profile not set up", StatusText.HealthLine(A.Status(profile: Missing)).Split(" · ")[^1]);
    }

    [Fact]
    public void A_missing_profile_has_a_neutral_glyph_not_the_red_cross() =>
        Assert.NotEqual(StateGlyph.Down, StatusText.ProfileGlyph(Missing));

    [Fact]
    public void A_missing_profile_shows_not_set_up_in_the_home_strip()
    {
        var cell = StatusText.Strip(A.Status(profile: Missing)).Single(c => string.Equals(c.Label, Labels.CellProfile, StringComparison.Ordinal));

        Assert.Equal(("not set up", false), (cell.Value, cell.Glyph == StateGlyph.Down));
    }

    [Fact]
    public void A_profile_the_server_calls_invalid_still_reads_unreadable() =>
        Assert.Equal("unreadable", StatusText.ProfileState(new ProfileView(Live: null, Problem: "SND-PROFILE-002 bad key", Source: null, ProblemKind: "invalid")));

    [Fact]
    public void A_problem_with_no_kind_reads_unreadable_so_a_broken_file_never_hides_behind_setup() =>
        Assert.Equal("unreadable", StatusText.ProfileState(new ProfileView(Live: null, Problem: "No profile found", Source: null)));

    [Fact]
    public void A_missing_problem_kind_from_the_server_reads_not_set_up() =>
        Assert.Equal("not set up", StatusText.ProfileState(new ProfileView(Live: null, Problem: "No profile found", Source: "x", ProblemKind: "missing")));
}
