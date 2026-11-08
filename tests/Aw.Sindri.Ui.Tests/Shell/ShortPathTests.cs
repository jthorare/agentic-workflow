using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>I5, I7: a path as the app shows it: ~ for home, and a middle ellipsis past 48 characters that keeps the head and the file name.</summary>
[Lane(TestLane.Unit)]
public sealed class ShortPathTests
{
    [Fact]
    public void A_short_path_stays_whole() => Assert.Equal("/tmp/sindri/control.json", StatusText.ShortPath("/tmp/sindri/control.json"));

    [Fact]
    public void Home_is_written_as_a_tilde()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal("~/.sindri/control.json", StatusText.ShortPath(Path.Combine(home, ".sindri", "control.json")));
    }

    [Fact]
    public void A_long_path_keeps_its_head_and_as_much_of_its_tail_as_fits()
    {
        var shown = StatusText.ShortPath("/var/folders/07/k3x9q2m1n8b7v6c5x4z3/T/aw-sindri-home-1234567890/sindri/ui/control.json");

        Assert.Equal("/var/…/sindri/ui/control.json", shown);
        Assert.True(shown.Length <= StatusText.ShortPathLength);
    }

    [Fact]
    public void A_file_name_longer_than_the_limit_keeps_its_end()
    {
        var shown = StatusText.ShortPath("/var/" + new string('x', 80) + ".json");

        Assert.EndsWith("x.json", shown, StringComparison.Ordinal);
        Assert.StartsWith("/var/…", shown, StringComparison.Ordinal);
        Assert.Equal(StatusText.ShortPathLength, shown.Length);
    }

    // S3: the profile's file is a path, shown in mono as every path is; the hash beside it stays as it is.
    [Fact]
    public void The_profile_file_is_the_one_path_cell() =>
        Assert.Equal([false, true], StatusText.Profile(A.Live()).Select(c => c.IsPath));
}
