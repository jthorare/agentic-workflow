using System.Text;

namespace Aw.Context.Testing.Fixtures;

/// <summary>
/// A small git repo for the app's context goldens (S-4): the tree src/tabs/{TabManager,TabGroups,Pinned}.ts,
/// src/layout/Grid.ts, README.md and a private .env, in one commit with a fixed author, committer, date and
/// message, so its sha is the same on every machine. The tabs map (<see cref="MapJson"/>) maps it: TabManager.ts is
/// confirmed, TabGroups.ts is a model suggestion, and the other files are unmapped. The aw-split-panes fixture repo has
/// no TabGroups.ts, so it can't stand in. Dispose deletes it.
/// </summary>
public sealed class MapTabsRepo : IDisposable
{
    /// <summary>The repo's name in the tabs map.</summary>
    public const string Repo = "aw-split-panes";

    private static readonly FixtureAuthor _author = new("Fixture Author", "fixture@example.invalid");
    private static readonly DateTimeOffset _at = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly (string Path, string Text)[] _files =
    [
        ("src/tabs/TabManager.ts", "export class TabManager {\n  restoreLayout(): void {}\n}\n"),
        ("src/tabs/TabGroups.ts", "export class TabGroups {\n  groupSelected(): void {}\n}\n"),
        ("src/tabs/Pinned.ts", "export const pinned: string[] = [];\n"),
        ("src/layout/Grid.ts", "export class Grid {}\n"),
        ("README.md", "# Tabs\n"),
        (".env", "PRIVATE=1\n"),
    ];

    private readonly string _root;

    private MapTabsRepo(string root, string path, string head)
    {
        _root = root;
        Path = path;
        Head = head;
    }

    /// <summary>The repo's working tree (branch 'main' checked out, clean).</summary>
    public string Path { get; }

    /// <summary>The sha of 'main'.</summary>
    public string Head { get; }

    /// <summary>The tabs map, as the JSON file the import reads.</summary>
    public static string MapJson { get; } = ReadMap();

    /// <summary>Builds the repo in a new temp directory.</summary>
    /// <returns>The built repo.</returns>
    public static MapTabsRepo Build()
    {
        var root = Directory.CreateTempSubdirectory("aw-map-tabs-").FullName;
        try
        {
            var template = RepoTemplate.Copy("map-tabs", "map-tabs-", root, BuildTemplate);   // one git build per process; the history is fixed, so a copy has the same sha
            return new MapTabsRepo(root, System.IO.Path.Combine(root, "tabs"), template.Head);
        }
        catch
        {
            Directory.Delete(root, recursive: true);
            throw;
        }
    }

    private static MapTabsRepo BuildTemplate(string root)
    {
        var path = Directory.CreateDirectory(System.IO.Path.Combine(root, "tabs")).FullName;
        var emptyConfig = System.IO.Path.Combine(root, "gitconfig");
        File.WriteAllText(emptyConfig, string.Empty);
        foreach (var (relative, text) in _files)
        {
            var target = System.IO.Path.Combine(path, relative);
            _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);   // why: a file under the repo has a directory
            File.WriteAllText(target, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        var git = new GitRunner(path, emptyConfig, _author);
        _ = git.Run(_at, "init", "-q", "-b", "main");
        _ = git.Run(_at, "config", "core.hooksPath", System.IO.Path.Combine(root, "no-hooks"));
        _ = git.Run(_at, "add", "-A");
        _ = git.Run(_at, "commit", "-q", "--no-gpg-sign", "-m", "Tabs fixture");
        return new MapTabsRepo(root, path, git.Run(_at, "rev-parse", "main"));
    }

    /// <summary>Writes the tabs map to <paramref name="directory"/>/tabs-map.json.</summary>
    /// <param name="directory">An existing directory outside every repo.</param>
    /// <returns>The file's path.</returns>
    public static string WriteMap(string directory)
    {
        var path = System.IO.Path.Combine(directory, "tabs-map.json");
        File.WriteAllText(path, MapJson);
        return path;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string ReadMap()
    {
        var assembly = typeof(MapTabsRepo).Assembly;
        using var stream = assembly.GetManifestResourceStream("map-tabs.json") ?? throw new InvalidOperationException("The tabs map isn't embedded in Aw.Context.Testing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
