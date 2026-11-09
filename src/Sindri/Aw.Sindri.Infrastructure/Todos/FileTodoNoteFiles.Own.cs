using System.Text;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Infrastructure.Todos;

#pragma warning disable MA0182 // until the todo sync registers it

/// <summary>Sindri's own side of the note files: the intent file ('writing') and the base files ('note-base-&lt;hash&gt;.md'), in the todo folder.</summary>
internal sealed partial class FileTodoNoteFiles
{
    private const string BasePrefix = "note-base-";
    private const string BaseSuffix = ".md";

    private static readonly Regex _hex = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    private string IntentPath => Path.Combine(_folder, "writing");

    public void WriteIntent(string hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        Replace(IntentPath, Encoding.UTF8.GetBytes(hash));
    }

    public string? ReadIntent()
    {
        if (!File.Exists(IntentPath))
        {
            return null;
        }

        var hash = File.ReadAllText(IntentPath, Encoding.UTF8).Trim();
        return hash.Length == 0 ? null : hash;
    }

    public void ClearIntent() => File.Delete(IntentPath);

    public string SaveBase(NoteBase noteBase)
    {
        ArgumentNullException.ThrowIfNull(noteBase);
        var bytes = Encoding.UTF8.GetBytes(noteBase.Lines.Count == 0 ? string.Empty : string.Join('\n', noteBase.Lines) + "\n");
        var hash = Hash(bytes);
        Replace(BasePath(hash), bytes);
        return hash;
    }

    public NoteBase? LoadBase(string hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        if (!_hex.IsMatch(hash) || !File.Exists(BasePath(hash)))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(BasePath(hash));
        if (!string.Equals(Hash(bytes), hash, StringComparison.Ordinal))
        {
            return null;
        }

        var text = _utf8.GetString(bytes);
        return new NoteBase(text.Length == 0 ? [] : text[..^1].Split('\n'));
    }

    public void DeleteBases(string? keep)
    {
        if (!Directory.Exists(_folder))
        {
            return;
        }

        var kept = keep is null ? null : BasePrefix + keep + BaseSuffix;
        foreach (var file in Directory.EnumerateFiles(_folder, BasePrefix + "*" + BaseSuffix))
        {
            if (!string.Equals(Path.GetFileName(file), kept, StringComparison.Ordinal))
            {
                File.Delete(file);
            }
        }
    }

    private string BasePath(string hash) => Path.Combine(_folder, BasePrefix + hash + BaseSuffix);

    // tmp file (0600) in the same folder, then a rename over the target: a reader sees the old file or the new one, never half of it
    private void Replace(string path, byte[] bytes)
    {
        MakeFolder();
        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = CreateNew(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite))
            {
                stream.Write(bytes);
            }

            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            File.Delete(tmp);
            throw;
        }
    }

    private void MakeFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            _ = Directory.CreateDirectory(_folder);
            return;
        }

        _ = Directory.CreateDirectory(_folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    // exclusive create (an existing file, a symlink included, refuses it) with the given mode on a unix box
    private static FileStream CreateNew(string path, UnixFileMode mode)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = mode;
        }

        return new FileStream(path, options);
    }
}

#pragma warning restore MA0182
