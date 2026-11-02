using Aw.Context.Infrastructure.Extract;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class GraphifyProcessDieWithParentTests
{
    private static readonly Dictionary<string, string> _env = new(StringComparer.Ordinal) { ["PATH"] = "/usr/bin:/bin" };

    private static readonly string[] _bwrapPaths = ["/usr/bin/bwrap", "/bin/bwrap"];

    [Fact]
    public async Task A_sandboxed_run_outlives_the_thread_that_started_it()
    {
        var bwrap = _bwrapPaths.FirstOrDefault(File.Exists);
        Assert.SkipWhen(bwrap is null, "no root-owned bwrap at /usr/bin/bwrap or /bin/bwrap");
        string[] argv = [bwrap!, "--unshare-all", "--new-session", "--die-with-parent", "--ro-bind", "/", "/", "--dev", "/dev", "--proc", "/proc", "/bin/sh", "-c", "sleep 5; echo done"];
        Task<GraphifyProcess.Ran?>? started = null;
        var ct = TestContext.Current.CancellationToken;

        // --die-with-parent ties the child's life to the thread that forked bwrap; a pool thread retires the same way.
        var caller = new Thread(() => started = GraphifyProcess.Spawn(argv, _env, Path.GetTempPath(), TimeSpan.FromSeconds(60), ct));
        caller.Start();
        caller.Join();
        var ran = await started!;

        Assert.NotNull(ran);
        Assert.Equal(0, ran.Exit);
        Assert.False(ran.TimedOut);
        Assert.Contains("done", ran.Stdout, StringComparison.Ordinal);
    }
}
