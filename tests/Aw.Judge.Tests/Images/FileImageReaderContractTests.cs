using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Processes;
using Aw.Judge.Infrastructure.Images;
using Aw.Judge.Infrastructure.Processes;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Images;

[Lane(TestLane.Integration)]
public sealed class FileImageReaderContractTests : ImageReaderContract, IDisposable
{
    private readonly ScratchDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    protected override (IImageReader Reader, string Directory) Create(IReadOnlyDictionary<string, byte[]> files, IReadOnlyDictionary<string, string> links)
    {
        foreach (var (name, bytes) in files)
        {
            _ = _directory.Write(name, bytes);
        }

        foreach (var (name, target) in links)
        {
            var link = Path.Combine(_directory.Path, name);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(link) ?? _directory.Path);
            _ = Directory.CreateSymbolicLink(link, Path.Combine(_directory.Path, target));
        }

        return (new FileImageReader(), _directory.Path);
    }

    [Fact]
    public async Task A_fifo_is_refused_without_blocking()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "mkfifo is not available");
        var fifo = Path.Combine(_directory.Path, "after.png");
        var made = await new SystemProcessRunner(Environment.GetEnvironmentVariable).Run(
            new ProcessRequest { FileName = "mkfifo", Arguments = [fifo], Timeout = TimeSpan.FromSeconds(10) }, TestContext.Current.CancellationToken);
        Assert.SkipWhen(made.ExitCode != 0, "mkfifo failed here");

        var read = Task.Run(async () => await new FileImageReader().Read([fifo], _directory.Path, TestContext.Current.CancellationToken));
        var finished = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)) == read;
        if (!finished)
        {
            // Unblock the reader the bug leaves waiting for a writer, so the run does not hang.
            await using var writer = new FileStream(fifo, FileMode.Open, FileAccess.Write);
        }

        Assert.True(finished, "the reader blocked on a FIFO");
        Assert.Contains("regular file", Assert.IsType<string>((await read).Problem), StringComparison.Ordinal);
    }
}
