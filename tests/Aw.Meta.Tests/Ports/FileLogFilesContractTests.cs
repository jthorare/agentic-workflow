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

    public void Dispose() => _dir.Dispose();
}
