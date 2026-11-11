using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;

namespace Aw.Sindri.Ui.Tests.Desktop;

[Lane(TestLane.Unit)]
public sealed class UiStatePathsTests
{
    [Fact]
    public void They_sit_in_a_ui_directory_beside_control_json()
    {
        var paths = UiStatePaths.From(name => name == "AW_SINDRI_HOME" ? "/s" : null, "/u");

        Assert.Equal(Path.Combine("/s", "ui"), paths.Directory);
        Assert.Equal(Path.Combine("/s", "ui", "preferences.json"), paths.Preferences);
        Assert.Equal(Path.Combine("/s", "ui", "instance.sock"), paths.InstanceSocket);
        Assert.Equal(Path.Combine("/s", "ui", "instance.lock"), paths.InstanceLock);
    }

    [Fact]
    public void They_follow_the_home_default() =>
        Assert.Equal(Path.Combine("/u", ".agentic-workflow", "sindri-app", "ui"), UiStatePaths.From(_ => null, "/u").Directory);

    [Fact]
    public void A_socket_path_too_long_for_sun_path_moves_under_the_temp_directory_by_a_hash_of_it()
    {
        var deep = Path.Combine("/h", new string('d', 120), "ui");

        var paths = UiStatePaths.In(deep, "/t");

        Assert.Equal(Path.Combine(deep, "instance.lock"), paths.InstanceLock);
        Assert.StartsWith(Path.Combine("/t", "sindri-ui-"), paths.InstanceSocket, StringComparison.Ordinal);
        Assert.EndsWith("instance.sock", paths.InstanceSocket, StringComparison.Ordinal);
        Assert.Equal(paths.InstanceSocket, UiStatePaths.In(deep, "/t").InstanceSocket);
        Assert.NotEqual(paths.InstanceSocket, UiStatePaths.In(deep + "2", "/t").InstanceSocket, StringComparer.Ordinal);
    }

    [Fact]
    public void A_socket_path_that_fits_stays_in_the_ui_directory() =>
        Assert.Equal(Path.Combine("/h", "ui", "instance.sock"), UiStatePaths.In(Path.Combine("/h", "ui"), "/t").InstanceSocket);

    // #2804: a temp root of 99 bytes (an agent's scratchpad) makes the fallback ~139 bytes, which the bind refuses.
    [Fact]
    public void A_long_temp_root_never_leaves_the_socket_path_over_sun_path()
    {
        var deep = Path.Combine("/h", new string('d', 120), "ui");
        var longRoot = "/" + new string('t', 98);

        var socket = UiStatePaths.In(deep, longRoot).InstanceSocket;

        Assert.Equal(99, System.Text.Encoding.UTF8.GetByteCount(longRoot));
        var bytes = System.Text.Encoding.UTF8.GetByteCount(socket);
        Assert.True(bytes <= UiStatePaths.MaxSocketPath, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the socket path is {bytes} bytes: {socket}"));
    }

    [Fact]
    public void With_a_short_temp_root_the_fallback_stays_under_it_in_a_hashed_directory()
    {
        var deep = Path.Combine("/h", new string('d', 120), "ui");

        var socket = UiStatePaths.In(deep, "/t").InstanceSocket;

        Assert.Matches("^/t/sindri-ui-[0-9a-f]{16}/instance\\.sock$", socket);
    }

    [Fact]
    public void A_long_temp_root_falls_back_to_the_runtime_directory_when_it_is_set_and_short()
    {
        var deep = Path.Combine("/h", new string('d', 120), "ui");
        var longRoot = "/" + new string('t', 98);

        var socket = UiStatePaths.In(deep, longRoot, "/run/user/1000").InstanceSocket;

        Assert.Matches("^/run/user/1000/sindri-ui-[0-9a-f]{16}/instance\\.sock$", socket);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/run/user/" + "uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu")]
    public void A_long_temp_root_falls_back_to_tmp_when_the_runtime_directory_is_unset_or_too_long(string? runtimeDir)
    {
        var deep = Path.Combine("/h", new string('d', 120), "ui");
        var longRoot = "/" + new string('t', 98);

        var socket = UiStatePaths.In(deep, longRoot, runtimeDir).InstanceSocket;

        Assert.Matches("^/tmp/sindri-ui-[0-9a-f]{16}/instance\\.sock$", socket);
    }
}
