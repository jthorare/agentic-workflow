using Aw.Kernel.Testing;
using static Aw.Sindri.Ui.Tests.Setup.SetupFixture;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The profile row shows what the profile allows in the API's own plain words, once it is saved.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupProfilePreviewTests
{
    private readonly SetupFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_profile_step_lists_the_served_preview_lines_in_order()
    {
        _f.Serve(With("profile-preview", "profile", "needs-you"));   // a step that is not finished draws the lines it was served

        var step = (await _f.Read(Token)).Steps.Single(s => s.Id == "profile");

        Assert.Equal(
            [("repo", "Works in demo (/work/demo)."), ("service", "Runs the observe service every 1h."), ("allows", "Workflows may: pr.open, pr.comment."), ("limit", "Warns you when an agent has used 80% of its daily budget.")],
            step.Preview.Select(l => (l.Kind, l.Text)));
        Assert.True(step.HasPreview);
    }

    [Fact]
    public async Task A_step_without_a_preview_and_a_saved_profile_show_none()
    {
        _f.Serve("profile-preview");
        var setup = await _f.Read(Token);

        _f.Serve(With("profile-preview", "profile", "done"));
        await setup.Refresh(Url, Token);

        Assert.All(setup.Steps.Where(s => s.Id != "profile"), s => Assert.False(s.HasPreview));
        Assert.False(setup.Steps.Single(s => s.Id == "profile").HasPreview);
    }
}
