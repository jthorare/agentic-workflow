using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Traits;

namespace Aw.Judge.Infrastructure.Traits;

/// <summary>
/// ITraitStore over the files: '&lt;repo&gt;/.agents/traits/*.yaml', then the private overlay
/// '&lt;state&gt;/traits/&lt;repo-name&gt;/*.yaml', whose trait replaces a repo trait of the same name so that a rule can be tuned
/// locally before its PR.
/// </summary>
/// <param name="overlayRoot">'&lt;state&gt;/traits': the directory with one subdirectory per repo name.</param>
internal sealed class FileTraitStore(string overlayRoot) : ITraitStore
{
    /// <inheritdoc />
    public TraitSet Load(string repoRoot, string repoName)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);
        ArgumentNullException.ThrowIfNull(repoName);
        Dictionary<string, Trait> byName = new(StringComparer.Ordinal);
        List<TraitProblem> problems = [];
        foreach (var directory in new[] { Path.Combine(repoRoot, ".agents", "traits"), Path.Combine(overlayRoot, repoName) })
        {
            foreach (var file in Yaml(directory))
            {
                var (trait, error) = TraitFileParser.Parse(Read(file), file);
                if (trait is not null)
                {
                    byName[trait.Name] = trait;
                }
                else
                {
                    problems.Add(new TraitProblem(file, error ?? "invalid trait"));
                }
            }
        }

        return new TraitSet([.. byName.Values], problems);
    }

    private static string[] Yaml(string directory)
    {
        try
        {
            return [.. Directory.EnumerateFiles(directory, "*" + TraitFileParser.Extension).Order(StringComparer.Ordinal)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string Read(string file)
    {
        try
        {
            return File.ReadAllText(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }
}
