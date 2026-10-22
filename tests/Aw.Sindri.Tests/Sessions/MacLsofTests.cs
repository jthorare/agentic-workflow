using Aw.Sindri.Infrastructure.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>lsof's field output gives a working directory, the open descriptors and the sockets with a path (collector spec §4.1 steps 5 and 6, macOS).</summary>
public sealed class MacLsofTests
{
    private static string Recording(string name) => File.ReadAllText(Path.Combine(SessionFixtures.Macos, name));

    [Fact, Trait("Lane", "unit")]
    public void Cwd_is_the_n_field_of_the_cwd_descriptor() =>
        Assert.Equal("/Users/dev/work/repo", MacLsof.Cwd(Recording("lsof-cwd.txt")));

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("p1\nfcwd\n")]
    [InlineData("p1\nfcwd\nnrelative/path\n")]
    public void Cwd_is_null_when_lsof_gave_no_absolute_path(string output) => Assert.Null(MacLsof.Cwd(output));

    [Fact, Trait("Lane", "unit")]
    public void Open_files_are_paths_and_a_socket_key_in_descriptor_order_without_peers_and_pipes()
    {
        var files = MacLsof.OpenFiles(Recording("lsof-files.txt"), limit: 64);
        var key = MacLsof.SocketKey("0x826cb2cf7ecb93da")!.Value;

        Assert.Equal("/dev/null", files[0]);
        Assert.Contains(MacLsof.SocketLink(key), files, StringComparer.Ordinal);
        Assert.DoesNotContain(files, static f => f.Contains("->", StringComparison.Ordinal));
        Assert.Equal(16, files.Count);   // 18 descriptors, two of them pipes
    }

    [Fact, Trait("Lane", "unit")]
    public void Open_files_stop_at_the_limit() =>
        Assert.Equal(5, MacLsof.OpenFiles(Recording("lsof-files.txt"), limit: 5).Count);

    [Fact, Trait("Lane", "unit")]
    public void Sockets_map_each_keyed_socket_with_a_path_and_skip_connected_ones()
    {
        var sockets = MacLsof.Sockets(Recording("lsof-sockets.txt"));

        Assert.Equal("/private/tmp/tmux-501/triage", sockets[MacLsof.SocketKey("0x826cb2cf7ecb93da")!.Value]);
        Assert.Equal(3, sockets.Count);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("0x826cb2cf7ecb93da", true)]
    [InlineData("0xF", true)]
    [InlineData("826cb2cf7ecb93da", false)]
    [InlineData("0x", false)]
    [InlineData("0xZZ", false)]
    [InlineData("", false)]
    public void A_socket_key_is_a_hexadecimal_address_and_the_same_address_gives_the_same_key(string device, bool valid)
    {
        Assert.Equal(valid, MacLsof.SocketKey(device) is not null);
        Assert.Equal(MacLsof.SocketKey(device), MacLsof.SocketKey(device));
    }
}
