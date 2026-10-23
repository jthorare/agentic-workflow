using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The sessions the T3 host started, one 0600 file per session name under <see cref="T3Options.MapDirectory"/>, so a stop, an attach or the liveness check
/// finds the thread after Sindri restarts. A name is a checked 'snd-…' session name, never a path.
/// </summary>
/// <param name="options">Where the map lives.</param>
internal sealed class T3SessionMap(T3Options options)
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(T3JsonContext.Default);

    /// <summary>The session called <paramref name="name"/>, or null.</summary>
    /// <param name="name">The session name.</param>
    /// <returns>The entry, or null when there is none (or it doesn't read).</returns>
    public T3SessionEntry? Get(string name)
    {
        if (PathFor(name) is not { } path)
        {
            return null;
        }

        try
        {
            var read = AwJson.Deserialize<T3SessionEntry>(File.ReadAllBytes(path), _json);
            return read.IsSuccess && string.Equals(read.Value.Name, name, StringComparison.Ordinal) ? read.Value : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;   // missing, or gone between the listing and the read
        }
    }

    /// <summary>Records a started session.</summary>
    /// <param name="entry">The session.</param>
    public void Save(T3SessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var path = PathFor(entry.Name) ?? throw new ArgumentException($"'{entry.Name}' isn't a session name", nameof(entry));
        OwnerOnly.EnsureDirectory(options.MapDirectory);
        OwnerOnly.WriteAtomically(path, AwJson.Serialize(entry, _json));
    }

    /// <summary>Whether <paramref name="name"/> is a name the map can hold (letters, digits, '-', '_', '.'; never a path).</summary>
    /// <param name="name">The session name.</param>
    /// <returns>True when it can.</returns>
    public bool Holds(string name) => PathFor(name) is not null;

    /// <summary>Forgets a session. Safe to repeat.</summary>
    /// <param name="name">The session name.</param>
    public void Remove(string name)
    {
        if (PathFor(name) is { } path)
        {
            _ = OwnerOnly.TryDelete(path);
        }
    }

    /// <summary>Every session recorded now.</summary>
    /// <returns>The entries.</returns>
    public IReadOnlyList<T3SessionEntry> All()
    {
        try
        {
            return Directory.Exists(options.MapDirectory)
                ? [.. Directory.EnumerateFiles(options.MapDirectory, "*.json").Select(static f => Path.GetFileNameWithoutExtension(f)).Select(Get).OfType<T3SessionEntry>()]
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private string? PathFor(string name) =>
        name.Length is > 0 and <= 128 && name.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') && name[0] != '.'
            ? Path.Combine(options.MapDirectory, name + ".json")
            : null;
}
