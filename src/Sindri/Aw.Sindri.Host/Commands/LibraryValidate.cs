using System.Globalization;
using System.Text;
using Aw.Context.Core.Library;
using Aw.Sindri.Core;
using Aw.Sindri.Host.Commands.Library;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri library validate &lt;path|dir&gt; [--root &lt;name&gt;] [--json]' (PR review page spec §5.5): the design-doc rules over a file or folder, locally; nothing is stored and Sindri need not run.</summary>
/// <param name="output">Where the verb writes.</param>
internal sealed class LibraryValidate(Output output)
{
    internal const string Usage = "library validate <path|dir> [--root <name>] [--json]";

    private static readonly string[] _later = ["--pr-body", "--repo", "--docs"];

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.FirstOrDefault(static a => _later.Contains(a, StringComparer.Ordinal)) is { } later)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"not yet: '{later}' checks against the stored library, which arrives with the PR page's second release ({Usage})")).ConfigureAwait(false);
        }

        var (target, root, bad) = Parse(args);
        if (target is null || bad is not null)
        {
            return await Usage1(bad).ConfigureAwait(false);
        }

        var json = args.Contains("--json", StringComparer.Ordinal);
        var files = Files(target);
        if (files is null)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'{target}' is not a file or folder ({Usage})")).ConfigureAwait(false);
        }

        var (docs, findings) = await Check(files, root ?? "local", ct).ConfigureAwait(false);
        var report = new LibraryValidateReport(findings, findings.Count == 0);
        return json
            ? await output.Json(report, findings.Count == 0 ? 0 : 1).ConfigureAwait(false)
            : await output.Text(Render(docs, findings), findings.Count == 0 ? 0 : 1).ConfigureAwait(false);
    }

    private static (string? Target, string? Root, string? Bad) Parse(string[] args)
    {
        string? root = null;
        string? target = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--json")
            {
                continue;
            }

            if (args[i] == "--root" && i + 1 < args.Length && root is null)
            {
                root = args[++i];
            }
            else if (!args[i].StartsWith('-') && target is null)
            {
                target = args[i];
            }
            else
            {
                return (target, root, args[i]);
            }
        }

        return (target, root, Bad: null);
    }

    private Task<int> Usage1(string? bad) =>
        output.Error(SindriErrors.InvalidInput.With((bad is null ? "which file or folder? " : $"'{bad}' isn't an argument of this command (or it needs a value): ") + Usage));

    // why: a folder is read the way the feed reads a root: no symlinks, no dot folders (.git, .obsidian, .trash), only *.md
    private static List<string>? Files(string target)
    {
        if (File.Exists(target))
        {
            return [Path.GetFullPath(target)];
        }

        if (!Directory.Exists(target))
        {
            return null;
        }

        var found = new List<string>();
        var pending = new Stack<string>([Path.GetFullPath(target)]);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            foreach (var entry in new DirectoryInfo(dir).EnumerateFileSystemInfos())
            {
                if (entry.LinkTarget is not null || entry.Name.StartsWith('.'))
                {
                    continue;
                }

                if (entry is DirectoryInfo)
                {
                    pending.Push(entry.FullName);
                }
                else if (entry.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(entry.FullName);
                }
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    private static async Task<(int Docs, List<LibraryFindingRow> Findings)> Check(List<string> files, string scope, CancellationToken ct)
    {
        var scoped = new List<ScopedDesignDoc>();
        var findings = new List<LibraryFindingRow>();
        foreach (var file in files)
        {
            var text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            var parsed = DesignDocParser.Parse(file, text);
            if (GeneratedSource.IsGenerated(parsed.FrontMatter) && await SourceSha(file, ct).ConfigureAwait(false) is { } sourceSha)
            {
                parsed = DesignDocParser.Parse(file, text, sourceSha);
            }

            if (!DesignDocParser.IsDesignDoc(parsed))
            {
                continue;
            }

            scoped.Add(new ScopedDesignDoc(file, scope, parsed));
            findings.AddRange(parsed.Errors.Select(e => Row(file, e)));
        }

        findings.AddRange(DesignDocScope.Check(scoped).Select(f => Row(f.Path, f.Error)));
        return (scoped.Count, [.. findings.OrderBy(static f => f.File, StringComparer.Ordinal).ThenBy(static f => f.Line ?? 0).ThenBy(static f => f.Rule, StringComparer.Ordinal)]);
    }

    // The sha256 of the packet.json beside a generated doc; null when there is none or a symlink (nothing to compare, no finding).
    private static async Task<string?> SourceSha(string docPath, CancellationToken ct)
    {
        var packet = GeneratedSource.PathBeside(docPath);
        var info = new FileInfo(packet);
        return info.Exists && info.LinkTarget is null && !info.Attributes.HasFlag(FileAttributes.ReparsePoint)   // a symlink is never a source, as in the library walker
            ? GeneratedSource.Sha256(await File.ReadAllBytesAsync(packet, ct).ConfigureAwait(false))
            : null;
    }

    private static LibraryFindingRow Row(string file, DesignDocError error) => new(error.Code, file, error.Line, error.Message, error.Fix);

    private static string Render(int docs, List<LibraryFindingRow> findings)
    {
        var text = new StringBuilder();
        foreach (var finding in findings)
        {
            _ = text.Append(finding.Rule).Append(' ').Append(finding.Message).Append(' ').Append(finding.Fix).Append('\n');
        }

        _ = text.Append(string.Create(CultureInfo.InvariantCulture, $"Checked {docs} design {(docs == 1 ? "doc" : "docs")}: ")).Append(findings.Count == 0 ? "no findings" : string.Create(CultureInfo.InvariantCulture, $"{findings.Count} {(findings.Count == 1 ? "finding" : "findings")}")).Append('\n');
        return text.ToString();
    }
}
