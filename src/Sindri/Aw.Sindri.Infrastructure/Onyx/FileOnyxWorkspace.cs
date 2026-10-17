using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>
/// IOnyxWorkspace over the file system under the Sindri home: every write goes to a temporary file in the target's directory and is renamed over it, so a reader sees
/// the old or the new content and a crash leaves no half file. A protected file is created 0600 from the start (no window where it is world-readable); the state is 0600.
/// </summary>
/// <param name="home">The Sindri home the onyx directory sits in.</param>
/// <param name="json">The AwJson options the state is written with.</param>
internal sealed class FileOnyxWorkspace(ISindriHome home, JsonSerializerOptions json) : IOnyxWorkspace
{
    private const UnixFileMode OwnerOnlyFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly OnyxPaths _paths = OnyxPaths.For(home.Path);

    /// <inheritdoc />
    public Result<string?> Read(string path)
    {
        try
        {
            return Results.Success(ReadText(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.OnyxWorkspaceUnusable.With($"Sindri can't read {path}: {exception.Message}; make it readable by you (or remove it if it is a leftover), then run this again");
        }
    }

    private static string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    /// <inheritdoc />
    public bool WriteIfChanged(string path, string content, bool secret)
    {
        if (ReadText(path) is { } have && string.Equals(have, content, StringComparison.Ordinal))
        {
            if (secret)
            {
                NarrowToOwner(path);   // the content is right but someone may have widened the mode
            }

            return false;
        }

        var directory = Path.GetDirectoryName(path)!;   // why: every path Sindri passes has a directory
        if (secret)
        {
            OwnerOnly.EnsureDirectory(directory);
        }
        else
        {
            _ = Directory.CreateDirectory(directory);
        }

        WriteAtomically(path, Encoding.UTF8.GetBytes(content), secret);
        return true;
    }

    /// <inheritdoc />
    public OnyxSetupState? ReadState()
    {
        try
        {
            var path = _paths.State;
            return File.Exists(path) && AwJson.Deserialize<OnyxSetupState>(File.ReadAllBytes(path), json) is { IsSuccess: true } read && read.Value.SchemaVersion == OnyxSetupState.CurrentSchema ? read.Value : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool StateIsUnreadable() => ReadState() is null && File.Exists(_paths.State);

    /// <inheritdoc />
    public void WriteState(OnyxSetupState state)
    {
        OwnerOnly.EnsureDirectory(_paths.Root);
        WriteAtomically(_paths.State, AwJson.Serialize(state, json), secret: true);
    }

    /// <inheritdoc />
    public Result<IDisposable?> TryLock()
    {
        try
        {
            OwnerOnly.EnsureDirectory(_paths.Root);
            return new FileStream(_paths.Root + "/.lock", new FileStreamOptions { Mode = FileMode.OpenOrCreate, Access = FileAccess.ReadWrite, Share = FileShare.None });
        }
        catch (IOException)
        {
            return Results.Success<IDisposable?>(value: null);   // another `onyx` command holds it
        }
        catch (UnauthorizedAccessException exception)
        {
            return SindriErrors.OnyxWorkspaceUnusable.With($"Sindri can't use its Onyx directory {_paths.Root}: {exception.Message}; make it owned by you and writable (or remove it if it is a leftover), then run this again");
        }
    }

    private static void NarrowToOwner(string path)
    {
        if (!OperatingSystem.IsWindows() && File.GetUnixFileMode(path) != OwnerOnlyFile)
        {
            File.SetUnixFileMode(path, OwnerOnlyFile);
        }
    }

    private static void WriteAtomically(string path, byte[] bytes, bool secret)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (secret && !OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerOnlyFile;
        }

        try
        {
            using (var stream = new FileStream(temporary, options))
            {
                stream.Write(bytes);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
