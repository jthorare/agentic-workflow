using Aw.Kernel;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The app's start port over <see cref="SindriStarter"/>; it also reads the start's last error line from serve.log.</summary>
/// <param name="starter">The starter.</param>
internal sealed class SindriStarterPort(SindriStarter starter) : ISindriStarter
{
    private long _logStart;

    public event EventHandler<SindriExitedEventArgs>? Exited
    {
        add => starter.Starter.Exited += value;
        remove => starter.Starter.Exited -= value;
    }

    public int Launch => starter.Launch;

    public string LogPath { get; } = Path.Combine(starter.Home, "logs", "serve.log");

    public Result<SindriStartOutcome> Start()
    {
        var before = File.Exists(LogPath) ? new FileInfo(LogPath).Length : 0;
        var result = starter.Start();
        if (result.IsError)
        {
            return result.Error;
        }

        if (result.Value == StartOutcome.Started)
        {
            _logStart = before;
        }

        return result.Value switch
        {
            StartOutcome.Started => SindriStartOutcome.Started,
            StartOutcome.AlreadyStarted => SindriStartOutcome.AlreadyStarted,
            StartOutcome.NotInstalled => SindriStartOutcome.NotInstalled,
            _ => throw new InvalidOperationException("Unknown start outcome " + result.Value),
        };
    }

    public void Release() => starter.Release();

    /// <summary>The last 'SND-' line appended since the start (the whole log when it was cut shorter than it was), or null.</summary>
    /// <returns>The line, or null.</returns>
    public string? LastError()
    {
        try
        {
            if (!File.Exists(LogPath))
            {
                return null;
            }

            using var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _ = stream.Seek(stream.Length >= _logStart ? _logStart : 0, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            string? last = null;
            while (reader.ReadLine() is { } line)
            {
                if (line.StartsWith("SND-", StringComparison.Ordinal))
                {
                    last = line;
                }
            }

            return last;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
