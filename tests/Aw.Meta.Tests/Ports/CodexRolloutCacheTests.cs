using Aw.Kernel.Testing;
using Aw.Meta.Core.Ids;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class CodexRolloutCacheTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    private string Put(string day, string session, string text)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_dir.Path, "2026", "11", day));
        var path = Path.Combine(folder.FullName, $"rollout-2026-11-{day}T10-00-00-{session}.jsonl");
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public async Task The_path_found_once_is_reused_until_the_file_is_gone_and_then_looked_for_again()
    {
        var reader = new CodexSessionRollouts(_dir.Path);
        var first = Put("07", "s-1", "one\n");
        Assert.Equal(first, (await reader.Tail(SessionId.From("s-1"), 1000, TestContext.Current.CancellationToken)).Value!.Path);

        // A newer file for the same session appears; the known path still exists, so no scan happens.
        _ = Put("08", "s-1", "two\n");
        Assert.Equal(first, (await reader.Tail(SessionId.From("s-1"), 1000, TestContext.Current.CancellationToken)).Value!.Path);

        File.Delete(first);
        Assert.Contains("2026/11/08", (await reader.Tail(SessionId.From("s-1"), 1000, TestContext.Current.CancellationToken)).Value!.Path.Replace('\\', '/'), StringComparison.Ordinal);
    }

    public void Dispose() => _dir.Dispose();
}
