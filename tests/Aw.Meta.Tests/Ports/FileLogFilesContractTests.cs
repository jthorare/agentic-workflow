using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class FileLogFilesContractTests : LogFilesContract, IDisposable
{
    private readonly TempDirectory _dir = new();

    protected override ILogFiles Create() => new FileLogFiles();

    protected override string Seed(ILogFiles logs, string name, string text)
    {
        var path = Path.Combine(_dir.Path, name);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public async Task Concurrent_claims_in_one_gate_directory_never_collide()
    {
        var logs = Create();
        var directory = Path.Combine(_dir.Path, "gates", "190");

        var claims = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(async () => await logs.ClaimRun(directory, 0, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)));

        Assert.All(claims, claim => Assert.True(claim.IsSuccess, claim.IsError ? claim.Error.Message : string.Empty));
        Assert.Equal(16, claims.Select(claim => claim.Value).Distinct().Count());
    }

    public void Dispose() => _dir.Dispose();
}
