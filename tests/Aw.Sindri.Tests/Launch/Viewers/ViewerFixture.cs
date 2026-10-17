using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch.Viewers;

/// <summary>A scratch directory, stub scripts (no display is ever needed), a fake host holding one session, and a fake clock.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal sealed class ViewerFixture : IDisposable
{
    public string Dir { get; } = Directory.CreateTempSubdirectory("aw-viewer-").FullName;

    public FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    public FakeSessionHost Host { get; } = new();

    public HostRef Session { get; private set; } = new(HostKind.Tmux, "snd-one");

    public async Task StartSession()
    {
        var started = await Host.Start(new HostSession(Session.Name, Dir, ["sleep", "1"], new Dictionary<string, string>(StringComparer.Ordinal)), TimeSpan.FromSeconds(4), CancellationToken.None);
        Session = started.Value;
    }

    public ViewerRequest Request(string title = "sindri ITM-1") =>
        new(Session, Host.AttachArgv(Session), title, TimeSpan.FromSeconds(3)) { Cwd = Dir };

    /// <summary>Writes an executable shell script and returns its path.</summary>
    public string Script(string name, string body)
    {
        var path = Path.Combine(Dir, name);
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    /// <summary>Awaits the task while moving the fake clock 250 ms at a time (the observation window's poll), with real pauses so processes can run.</summary>
    public async Task<T> Drive<T>(ValueTask<T> pending, Action<int>? onTick = null)
    {
        var task = pending.AsTask();
        for (var tick = 0; !task.IsCompleted && tick < 400; tick++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            onTick?.Invoke(tick);
            Clock.Advance(TimeSpan.FromMilliseconds(250));
        }

        return await task.WaitAsync(TimeSpan.FromSeconds(30), TimeProvider.System, TestContext.Current.CancellationToken);   // a hang fails the test, never holds the slot
    }

    public void Dispose() => Directory.Delete(Dir, recursive: true);
}
