using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Skills;

/// <summary>
/// The skill catalog on disk (issue 3787, piece 4): one listing per '&lt;skill&gt;/SKILL.md' under the skills folder, named by the folder, with its front matter
/// 'description'. A folder starting with '_' (shared text) or without a SKILL.md is no skill; a SKILL.md with no description is listed with an empty one.
/// </summary>
/// <param name="skillsFolder">The skills folder (<see cref="SkillFolders.Resolve"/>).</param>
internal sealed class FileSkillCatalog(string skillsFolder) : ISkillCatalogPort
{
    private const string SkillFile = "SKILL.md";

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<SkillListing>>> List(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(skillsFolder))
        {
            return ValueTask.FromResult(Results.Success<IReadOnlyList<SkillListing>>([]));
        }

        try
        {
            var listed = new List<SkillListing>();
            foreach (var folder in Directory.EnumerateDirectories(skillsFolder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Path.GetFileName(folder);
                var file = Path.Combine(folder, SkillFile);
                if (!name.StartsWith('_') && File.Exists(file))
                {
                    listed.Add(new SkillListing(name, Description(File.ReadLines(file))));
                }
            }

            return ValueTask.FromResult(Results.Success<IReadOnlyList<SkillListing>>([.. listed.OrderBy(static s => s.Name, StringComparer.Ordinal)]));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)   // boundary: a folder that can't be read
        {
            return ValueTask.FromResult(Results.Failure<IReadOnlyList<SkillListing>>(SindriErrors.GraphBlockFailed.With($"the skills folder {skillsFolder} can't be read: {e.Message}")));
        }
    }

    // why: the front matter is a few YAML lines; a plain, quoted or folded ('>' or '|') one-key value is all a SKILL.md description uses, so no YAML reader is needed
    private static string Description(IEnumerable<string> lines)
    {
        using var line = lines.GetEnumerator();
        if (!line.MoveNext() || !string.Equals(line.Current.TrimEnd(), "---", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        while (line.MoveNext() && !string.Equals(line.Current.TrimEnd(), "---", StringComparison.Ordinal))
        {
            if (line.Current.StartsWith("description:", StringComparison.Ordinal))
            {
                return Value(line.Current["description:".Length..].Trim(), line);
            }
        }

        return string.Empty;
    }

    private static string Value(string first, IEnumerator<string> line)
    {
        var parts = first is ">" or "|" or ">-" or "|-" or ">+" or "|+" ? [] : new List<string> { first };
        while (line.MoveNext() && line.Current.Length > 0 && char.IsWhiteSpace(line.Current[0]))
        {
            parts.Add(line.Current.Trim());
        }

        var text = string.Join(' ', parts.Where(static p => p.Length > 0));
        return text.Length >= 2 && (text[0], text[^1]) is ('"', '"') or ('\'', '\'') ? text[1..^1] : text;
    }
}
