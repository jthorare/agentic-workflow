using System.Reflection;
using System.Text;
using Aw.Kernel.Json;

namespace Aw.Context.Testing.Fixtures;

/// <summary>
/// The split-panes fixture (spec §10): its embedded files (paths relative to 'Fixtures/split-panes/', '/'-separated),
/// its history and its probes. See that folder's README for what each record is for.
/// </summary>
public static class SplitPanesFixture
{
    /// <summary>The Linear team key every fixture issue carries.</summary>
    public const string TeamKey = "SPL";

    private const string Prefix = "split-panes/";
    private static readonly Assembly _assembly = typeof(SplitPanesFixture).Assembly;
    private static readonly Lazy<Dictionary<string, string>> _resources = new(Index);
    private static readonly Lazy<FixtureHistory> _history = new(() => Read<FixtureHistory>("history.json"));
    private static readonly Lazy<ProbeFile> _probes = new(() => Read<ProbeFile>("probes.json"));

    /// <summary>Every embedded file, ordinal order.</summary>
    public static IReadOnlyList<string> ResourcePaths => [.. _resources.Value.Keys.Order(StringComparer.Ordinal)];

    /// <summary>The repo history from 'history.json'.</summary>
    public static FixtureHistory History => _history.Value;

    /// <summary>The probes from 'probes.json'.</summary>
    public static IReadOnlyList<Probe> Probes => _probes.Value.Probes;

    /// <summary>Reads an embedded file as UTF-8 text.</summary>
    /// <param name="path">A path from <see cref="ResourcePaths"/>.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentException">No such file in the fixture.</exception>
    public static string ReadText(string path) => Encoding.UTF8.GetString(ReadBytes(path));

    /// <summary>Reads an embedded file's bytes.</summary>
    /// <param name="path">A path from <see cref="ResourcePaths"/>.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="ArgumentException">No such file in the fixture.</exception>
    public static byte[] ReadBytes(string path)
    {
        if (!_resources.Value.TryGetValue(path, out var name))
        {
            throw new ArgumentException($"The split-panes fixture has no file '{path}'.", nameof(path));
        }

        using var stream = _assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' is listed but can't be opened.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>The files under a folder of the fixture, keyed by their path relative to it.</summary>
    /// <param name="folder">A folder such as 'app/03-spl-103'.</param>
    /// <returns>Relative path to resource path, ordinal order.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> FilesUnder(string folder)
    {
        var root = folder.TrimEnd('/') + "/";
        return [.. ResourcePaths.Where(p => p.StartsWith(root, StringComparison.Ordinal)).Select(p => KeyValuePair.Create(p[root.Length..], p))];
    }

    private static Dictionary<string, string> Index() =>
        _assembly.GetManifestResourceNames()
            .Where(n => n.Replace('\\', '/').StartsWith(Prefix, StringComparison.Ordinal))
            .ToDictionary(n => n.Replace('\\', '/')[Prefix.Length..], n => n, StringComparer.Ordinal);

    private static T Read<T>(string path)
    {
        var options = AwJson.CreateOptions(FixtureJsonContext.Default);
        var result = AwJson.Deserialize<T>(ReadBytes(path), options);
        return result.IsSuccess ? result.Value : throw new InvalidOperationException($"Fixture file '{path}' is invalid: {result.Error}");
    }
}
