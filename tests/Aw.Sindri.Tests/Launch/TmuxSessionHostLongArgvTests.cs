using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>#1765: tmux refuses one command over ~16 KB, so a session with a long argv or environment must still start.</summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class TmuxSessionHostLongArgvTests : IDisposable
{
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(4);
    private readonly PrivateTmux _tmux = new();

    public void Dispose() => _tmux.Dispose();

    [Fact]
    public async Task A_session_whose_argv_is_over_the_tmux_command_cap_starts_and_gets_every_argument()
    {
        var marker = Path.Combine(_tmux.Scratch, "argv-length.txt");
        var longArg = "it's \"$HOME\" " + new string('a', 20_000);
        var session = new HostSession(
            "snd-long-argv",
            _tmux.Scratch,
            ["sh", "-c", $"printf %s \"$1\" | wc -c > '{marker}'; sleep 60", "sh", longArg],
            new Dictionary<string, string>(StringComparer.Ordinal));

        var started = await new TmuxSessionHost(_tmux.Options).Start(session, _deadline, CancellationToken.None);

        Expect.True(started.IsSuccess);
        Expect.Equal(Encoding.UTF8.GetByteCount(longArg).ToString(System.Globalization.CultureInfo.InvariantCulture), (await ReadWhenWritten(marker)).Trim());
    }

    [Fact]
    public async Task A_session_whose_env_is_over_the_tmux_command_cap_starts_and_the_process_sees_it()
    {
        var marker = Path.Combine(_tmux.Scratch, "env-length.txt");
        var longValue = "it's a \"quoted\" " + new string('b', 20_000);
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["SND_LONG_VALUE"] = longValue };
        var session = new HostSession(
            "snd-long-env",
            _tmux.Scratch,
            ["sh", "-c", $"printf %s \"$SND_LONG_VALUE\" | wc -c > '{marker}'; sleep 60"],
            env);

        var started = await new TmuxSessionHost(_tmux.Options).Start(session, _deadline, CancellationToken.None);

        Expect.True(started.IsSuccess);
        Expect.Equal(Encoding.UTF8.GetByteCount(longValue).ToString(System.Globalization.CultureInfo.InvariantCulture), (await ReadWhenWritten(marker)).Trim());
    }

    private static async Task<string> ReadWhenWritten(string path)
    {
        for (var i = 0; i < 100; i++)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                return await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"{path} was never written");
    }
}
