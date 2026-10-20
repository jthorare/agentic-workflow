using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Host.Commands.Scoping;

namespace Aw.Sindri.Host.Commands.Line;

/// <summary>
/// The files a line station's check reads, from a directory (line spec §4.3, W11's Step directory): every '.json', '.jsonl' and '.md' file under it, keyed by its
/// path relative to it ('spec.md', 'scope/acceptance.json'). The flags add what the directory does not hold: --scope and --design a packet directory under 'scope/' and
/// 'design/', --style and --lenses a pack's pack.json, and --repo the files a contract edit targets (read from the checkout, never outside it).
/// </summary>
internal static class LineFactDirectory
{
    private const long MaxBytes = 4 * 1024 * 1024;
    private static readonly string[] _extensions = [".json", ".jsonl", ".md"];

    /// <summary>The directory's files and the flags' additions.</summary>
    internal static async Task<Result<Dictionary<string, string>>> Read(string directory, ScopingArguments flags, CancellationToken ct)
    {
        var facts = await Files(directory, prefix: string.Empty, ct).ConfigureAwait(false);
        if (facts.IsError)
        {
            return facts;
        }

        foreach (var (flag, prefix) in new[] { ("--scope", "scope/"), ("--design", "design/") })
        {
            if (flags.Value(flag) is { } other)
            {
                var added = await Files(other, prefix, ct).ConfigureAwait(false);
                if (added.IsError)
                {
                    return added;
                }

                Merge(facts.Value, added.Value);
            }
        }

        await Pack(facts.Value, flags.Value("--style"), "pack/style.json", ct).ConfigureAwait(false);
        await Pack(facts.Value, flags.Value("--lenses"), "pack/lenses.json", ct).ConfigureAwait(false);
        if (flags.Value("--repo") is { } repo)
        {
            await RepoTargets(facts.Value, repo, ct).ConfigureAwait(false);
        }

        return facts;
    }

    private static void Merge(Dictionary<string, string> into, Dictionary<string, string> from)
    {
        foreach (var (key, text) in from)
        {
            into[key] = text;
        }
    }

    private static async Task<Result<Dictionary<string, string>>> Files(string directory, string prefix, CancellationToken ct)
    {
        if (!Directory.Exists(directory))
        {
            return SindriErrors.InvalidInput.With($"{directory} is not a directory (fix: pass the directory of the packet's files, for example --facts \"$AW_SINDRI_RUN_DIR\")");
        }

        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/');
            if (Array.IndexOf(_extensions, Path.GetExtension(file)) >= 0 && !relative.StartsWith('.') && !relative.Contains("/.", StringComparison.Ordinal) && new FileInfo(file).Length <= MaxBytes)
            {
                facts[prefix + relative] = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            }
        }

        return facts;
    }

    private static async Task Pack(Dictionary<string, string> facts, string? file, string key, CancellationToken ct)
    {
        if (file is not null && File.Exists(file) && new FileInfo(file).Length <= MaxBytes)
        {
            facts[key] = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
        }
    }

    // The contract edits name the files they change; each is read from the checkout when it is inside it. A target outside the checkout stays unread, so the rule reports it missing.
    private static async Task RepoTargets(Dictionary<string, string> facts, string repo, CancellationToken ct)
    {
        var root = Path.GetFullPath(repo) + Path.DirectorySeparatorChar;
        foreach (var target in Targets(facts))
        {
            var path = Path.GetFullPath(Path.Combine(root, target));
            if (path.StartsWith(root, StringComparison.Ordinal) && File.Exists(path) && new FileInfo(path).Length <= MaxBytes)
            {
                facts["repo/" + target] = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            }
        }

        _ = facts.TryAdd("repo/", string.Empty);   // the checkout was supplied, so a target it lacks is a finding, not a rule that cannot run
    }

    private static IEnumerable<string> Targets(Dictionary<string, string> facts)
    {
        var contracts = new LineFacts(facts.Where(static p => p.Key == "contracts.json").ToDictionary(static p => p.Key, static p => p.Value, StringComparer.Ordinal));
        return LineJson.Items(contracts.Json("contracts.json"), "edits").Select(static e => LineJson.Str(LineJson.Obj(e, "target"), "file")).OfType<string>().Distinct(StringComparer.Ordinal);
    }
}
