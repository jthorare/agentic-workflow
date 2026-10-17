using Aw.Kernel.Json;

namespace Aw.Forge.Infrastructure.World;

/// <summary>
/// The world in one JSON file under a directory. Each call takes an exclusive lock file (so the CLI and a server can share the
/// directory), loads the document, runs the callback and, for a write, saves it to a temporary file and renames it over the original.
/// </summary>
internal sealed class FileWorldStore : IWorldStore
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(ForgeFileJsonContext.Default, indented: true);
    private static readonly TimeSpan _lockWait = TimeSpan.FromSeconds(10);

    private readonly string _file;
    private readonly string _lock;

    internal FileWorldStore(string directory)
    {
        _ = Directory.CreateDirectory(directory);
        _file = Path.Combine(directory, "forge.json");
        _lock = Path.Combine(directory, "forge.lock");
    }

    public TResult Read<TResult>(Func<WorldState, TResult> read)
    {
        using var held = Acquire();
        return read(Load());
    }

    public TResult Write<TResult>(Func<WorldState, TResult> write)
    {
        using var held = Acquire();
        var state = Load();
        var result = write(state);
        var temporary = _file + ".tmp";
        File.WriteAllBytes(temporary, AwJson.Serialize(state, _json));
        File.Move(temporary, _file, overwrite: true);
        return result;
    }

    private FileStream Acquire()
    {
        var deadline = Environment.TickCount64 + (long)_lockWait.TotalMilliseconds;
        var spin = new SpinWait();
        while (true)
        {
            try
            {
                return new FileStream(_lock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException e) when (IsContention(e) && Environment.TickCount64 < deadline)
            {
                spin.SpinOnce();   // backs off to a short sleep inside SpinWait; Thread.Sleep is banned
            }
        }
    }

    // A held lock is a plain IOException (a sharing violation, or EWOULDBLOCK on Unix). A missing directory, a bad path or a
    // full disk is also an IOException, and waiting ten seconds will not fix it: those surface at once.
    private static bool IsContention(IOException e) =>
        e is not (DirectoryNotFoundException or FileNotFoundException or PathTooLongException or DriveNotFoundException);

    private WorldState Load()
    {
        if (!File.Exists(_file))
        {
            return new WorldState();
        }

        var parsed = AwJson.Deserialize<WorldState>(File.ReadAllBytes(_file), _json);
        return parsed.IsSuccess
            ? parsed.Value
            : throw new InvalidDataException($"The forge state file is not valid: {parsed.Error.Message} ({_file})");
    }
}
