using System.Text;

namespace Aw.Context.Testing.Fixtures;

/// <summary>
/// A small git repo for the code lens's goldens (S-CL): the sharing feature's source files, an edge test, a test that imports a file, a test that only shares a name, a migration that names a table and a private .env,
/// in one commit with a fixed author, committer, date and message, so its sha is the same on every machine. It sits beside <see cref="MapTabsRepo"/> and leaves it alone (the file-map goldens read that one). Dispose deletes it.
/// </summary>
public sealed class CodeLensRepo : IDisposable
{
    /// <summary>The repo's name in the code map.</summary>
    public const string Repo = "aw-split-panes";

    private static readonly FixtureAuthor _author = new("Fixture Author", "fixture@example.invalid");
    private static readonly DateTimeOffset _at = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly (string Path, string Text)[] _files =
    [
        ("src/sharing/links.ts", "export function createLink(): string {\n  return 'insert into shared_link';\n}\n"),
        ("src/sharing/routes.ts", "export const routes: string[] = [];\n"),
        ("src/sharing/token.ts", "export const token = 'x';\n"),
        ("tests/edge.test.ts", "import { createLink } from '../src/sharing/links';\n"),
        ("tests/links.test.ts", "import { createLink } from '../src/sharing/links';\n"),
        ("tests/routes.test.ts", "describe('routes', () => {});\n"),
        ("migrations/002_sharing.sql", "create table shared_link (id integer primary key);\n"),
        ("migrations/003_other.sql", "create table unrelated (id integer primary key);\n"),
        ("README.md", "# Sharing\n"),
        (".env", "PRIVATE=1\n"),
    ];

    private readonly string _root;

    private CodeLensRepo(string root, string path, string head)
    {
        _root = root;
        Path = path;
        Head = head;
    }

    /// <summary>The repo's working tree (branch 'main' checked out, clean).</summary>
    public string Path { get; }

    /// <summary>The sha of 'main'.</summary>
    public string Head { get; }

    /// <summary>The code map as YAML: the sharing feature, its action and things, two implementing files, an edge test, with the repo evidence the code lens reads.</summary>
    /// <param name="commit">The commit the map names, or null for none.</param>
    /// <returns>The YAML.</returns>
    public static string MapYaml(string? commit = null) => string.Join('\n', new[]
    {
        commit is null ? null : $"commit: {commit}",
        "schemaVersion: 1", "kind: authored", $"repo: {Repo}", "nodes:",
        "  - {key: \"feature:sharing\", label: \"Sharing\"}",
                "  - {key: \"surface:screen/share\", label: \"Share\"}",
        "  - {key: \"feature:audit\", label: \"Audit\"}",
        "  - {key: \"action:sharing.create-link\", label: \"Create a link\"}",
        "  - {key: \"action:sharing.resolve-link\", label: \"Resolve a link\"}",
        "  - {key: \"thing:shared-link\", label: \"Shared link\"}",
        "  - {key: \"thing:note\", label: \"Note\"}",
        "  - {key: \"code:src/sharing/links.ts\", label: \"links.ts\", provenance: deterministic}",
        "  - {key: \"code:src/sharing/routes.ts\", label: \"routes.ts\", provenance: deterministic}",
        "  - {key: \"code:tests/edge.test.ts\", label: \"edge.test.ts\", provenance: deterministic}",
        "edges:",
        "  - {from: \"surface:screen/share\", to: \"feature:sharing\", type: part-of}",
        "  - {from: \"action:sharing.create-link\", to: \"surface:screen/share\", type: on}",
        "  - {from: \"action:sharing.resolve-link\", to: \"surface:screen/share\", type: on}",
        "  - {from: \"action:sharing.create-link\", to: \"thing:shared-link\", type: writes}",
        "  - {from: \"action:sharing.resolve-link\", to: \"thing:shared-link\", type: reads}",
        "  - {from: \"action:sharing.resolve-link\", to: \"thing:note\", type: reads}",
        "  - {from: \"code:src/sharing/links.ts\", to: \"feature:sharing\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/sharing/links.ts\"}]}",
        "  - {from: \"code:src/sharing/routes.ts\", to: \"feature:sharing\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/sharing/routes.ts\"}]}",
        "  - {from: \"code:tests/edge.test.ts\", to: \"feature:sharing\", type: tests, provenance: deterministic}",
        string.Empty,
    }.Where(l => l is not null));

    /// <summary>Builds the repo in a new temp directory.</summary>
    /// <returns>The built repo.</returns>
    /// <param name="extraTests">How many more test files to commit under tests/gen, each importing src/sharing/links (to pass the lexical candidate cap).</param>
    public static CodeLensRepo Build(int extraTests = 0)
    {
        var root = Directory.CreateTempSubdirectory("aw-code-lens-").FullName;
        try
        {
            var template = RepoTemplate.Copy("code-lens-" + extraTests.ToString(System.Globalization.CultureInfo.InvariantCulture), "code-lens-", root, templateRoot => BuildTemplate(templateRoot, extraTests));   // one git build per process and size; a copy has the same sha
            return new CodeLensRepo(root, System.IO.Path.Combine(root, "sharing"), template.Head);
        }
        catch
        {
            Directory.Delete(root, recursive: true);
            throw;
        }
    }

    private static CodeLensRepo BuildTemplate(string root, int extraTests)
    {
        var path = Directory.CreateDirectory(System.IO.Path.Combine(root, "sharing")).FullName;
        var emptyConfig = System.IO.Path.Combine(root, "gitconfig");
        File.WriteAllText(emptyConfig, string.Empty);
        foreach (var (relative, text) in _files.Concat(Enumerable.Range(0, extraTests).Select(i => ($"tests/gen/g{i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)}.test.ts", "import { createLink } from '../../src/sharing/links';\n"))))
        {
            var target = System.IO.Path.Combine(path, relative);
            _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);   // why: a file under the repo has a directory
            File.WriteAllText(target, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        var git = new GitRunner(path, emptyConfig, _author);
        _ = git.Run(_at, "init", "-q", "-b", "main");
        _ = git.Run(_at, "config", "core.hooksPath", System.IO.Path.Combine(root, "no-hooks"));
        _ = git.Run(_at, "add", "-A");
        _ = git.Run(_at, "commit", "-q", "--no-gpg-sign", "-m", "Code lens fixture");
        return new CodeLensRepo(root, path, git.Run(_at, "rev-parse", "main"));
    }

    /// <summary>Writes <paramref name="yaml"/> to <paramref name="directory"/>/code-map.yaml.</summary>
    /// <param name="directory">An existing directory outside every repo.</param>
    /// <param name="yaml">The map.</param>
    /// <returns>The file's path.</returns>
    public static string WriteMap(string directory, string yaml)
    {
        var path = System.IO.Path.Combine(directory, "code-map.yaml");
        File.WriteAllText(path, yaml);
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
}
