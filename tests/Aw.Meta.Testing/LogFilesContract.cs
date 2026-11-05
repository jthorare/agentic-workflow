using Aw.Meta.Core;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The ILogFiles contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class LogFilesContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>The adapter under test.</summary>
    /// <returns>The adapter.</returns>
    protected abstract ILogFiles Create();

    /// <summary>Puts a file in place.</summary>
    /// <param name="logs">The adapter returned by <see cref="Create"/>.</param>
    /// <param name="name">A file name.</param>
    /// <param name="text">The file's text.</param>
    /// <returns>The path to read it by.</returns>
    protected abstract string Seed(ILogFiles logs, string name, string text);

    /// <summary>A short file is returned whole, a long one by its end.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_log_is_read_by_its_end()
    {
        var logs = Create();
        var path = Seed(logs, "build.log", "first line\nlast line\n");

        var whole = await logs.Tail(path, 1000, _none);
        var end = await logs.Tail(path, 10, _none);

        Expect.That(whole.IsSuccess && whole.Value == "first line\nlast line\n", "a short file is whole");
        Expect.That(end.IsSuccess && end.Value == "last line\n", "the last characters are kept");
    }

    /// <summary>A file that isn't there is AW-META-013.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_log_is_AW_META_013()
    {
        var logs = Create();
        var path = Seed(logs, "present.log", "x");

        var missing = await logs.Tail(path + ".absent", 100, _none);

        Expect.That(missing.IsError && missing.Error.Code == MetaErrors.LogUnreadable.Code, "AW-META-013");
    }

    /// <summary>The latest run is the highest all-digit directory; other names and a missing directory count as none.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_latest_run_is_the_highest_numbered_directory()
    {
        var logs = Create();
        _ = Seed(logs, "gates/1/check.log", "a");
        _ = Seed(logs, "gates/9/check.log", "b");
        var path = Seed(logs, "gates/10/check.log", "c");
        _ = Seed(logs, "gates/tree/x.log", "d");
        var directory = Path.GetDirectoryName(Path.GetDirectoryName(path))!;
        var elsewhere = Path.Combine(Path.GetDirectoryName(directory)!, "absent");

        var latest = await logs.LatestRun(directory, _none);
        var missing = await logs.LatestRun(elsewhere, _none);

        Expect.That(latest.IsSuccess && latest.Value == 10, "10 is above 9 numerically, and tree is ignored");
        Expect.That(missing.IsSuccess && missing.Value == 0, "a missing directory is run 0");
    }
}
