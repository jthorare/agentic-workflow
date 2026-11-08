using Aw.Kernel.Testing;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class CodexSessionRolloutsContractTests : CodexRolloutsContract, IDisposable
{
    private readonly TempDirectory _dir = new();

    protected override (ICodexRollouts Reader, Action<string, string> Put) Create() =>
        (new CodexSessionRollouts(_dir.Path), (session, content) =>
        {
            var day = Directory.CreateDirectory(Path.Combine(_dir.Path, "2026", "11", "07"));
            File.WriteAllText(Path.Combine(day.FullName, $"rollout-2026-11-07T10-00-00-{session}.jsonl"), content);
        }
    );

    [Fact]
    public async Task A_session_id_that_is_a_path_or_a_pattern_is_never_looked_up()
    {
        var reader = new CodexSessionRollouts(_dir.Path);

        Assert.Null((await reader.Tail(SessionId.From("../x"), 100, TestContext.Current.CancellationToken)).Value);
        Assert.Null((await reader.Tail(SessionId.From("*"), 100, TestContext.Current.CancellationToken)).Value);
    }

    public void Dispose() => _dir.Dispose();
}
