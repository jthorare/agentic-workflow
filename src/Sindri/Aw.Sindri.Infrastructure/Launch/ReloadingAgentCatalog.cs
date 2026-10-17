using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The catalog a launch reads (API P10a): launcher.json is looked at on every access and read again only when its size or modification time changed, so an
/// edit takes effect on the next open without restarting Sindri. Precedence is the environment's file, then the state directory's, then the built-ins.
/// </summary>
/// <param name="path">The file to watch (the resolved AW_LAUNCHER_CONFIG or the state directory's launcher.json); it may not exist.</param>
/// <param name="builtins">The text of the embedded built-ins.</param>
internal sealed class ReloadingAgentCatalog(string path, byte[] builtins) : IAgentCatalog
{
    private readonly Lock _gate = new();
    private (FileStamp Stamp, LauncherConfig Config)? _read;

    /// <summary>The file this catalog watches.</summary>
    public string Path { get; } = path;

    /// <summary>The config as of the file's latest change; one access reads it at most once.</summary>
    public LauncherConfig Current
    {
        get
        {
            lock (_gate)
            {
                var stamp = FileStamp.Of(Path);
                if (_read is { } cached && cached.Stamp == stamp)
                {
                    return cached.Config;
                }

                var (bytes, unreadable) = LauncherStartup.ReadFile(Path);
                var config = IsolatedLauncherRead.Read(builtins, bytes);
                config = unreadable is null ? config : config with { Problem = unreadable, Unusable = true };
                _read = (stamp, config);
                return config;
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<AgentEntry> All => Current.Agents;

    /// <inheritdoc/>
    public AgentEntry? Resolve(AgentId id) => Current.Agents.FirstOrDefault(e => e.Id.Equals(id));

    private readonly record struct FileStamp(bool Exists, long Ticks, long Length)
    {
        internal static FileStamp Of(string path)
        {
            var info = new FileInfo(path);
            return info.Exists ? new FileStamp(Exists: true, info.LastWriteTimeUtc.Ticks, info.Length) : default;
        }
    }
}
