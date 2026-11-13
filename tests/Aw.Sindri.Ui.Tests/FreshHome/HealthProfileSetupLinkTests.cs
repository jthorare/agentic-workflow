using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.FreshHome;

/// <summary>The Health page's Profile card offers "Run Setup" only when the profile file is missing, not when it exists and is broken (issue 3113, part of 3019).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class HealthProfileSetupLinkTests : FrameHost
{
    private async Task<(Window Window, ShellViewModel Shell)> OnHealth(ProfileView profile)
    {
        Server.Current = A.Status(profile: profile);
        var (window, shell) = await Connected(width: 1600, height: 1000);
        shell.GoTo(PageId.System);
        await Until(window, () => Visible(window, "ProfileEyebrow"), "the Health page never drew the Profile card");
        return (window, shell);
    }

    [Fact]
    public Task A_missing_profile_shows_the_Run_Setup_button_with_the_no_profile_line() => OnUi(async () =>
    {
        var (window, _) = await OnHealth(new ProfileView(Live: null, Problem: "No profile found", Source: null, ProblemKind: "missing"));

        var link = Named<Button>(window, "ProfileSetupLink");

        Assert.Equal((true, Labels.ProfileNotSetUpDetail), (link.IsEffectivelyVisible, link.Content as string));
    });

    [Fact]
    public Task A_broken_profile_file_does_not_show_the_Run_Setup_button() => OnUi(async () =>
    {
        var (window, _) = await OnHealth(new ProfileView(Live: null, Problem: "line 3: unknown key", Source: null, ProblemKind: "invalid"));

        Assert.False(Visible(window, "ProfileSetupLink"));
    });
}
