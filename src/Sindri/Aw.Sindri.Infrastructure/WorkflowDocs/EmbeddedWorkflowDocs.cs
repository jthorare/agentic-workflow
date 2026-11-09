using Aw.Sindri.Core.Graph.Docs;

namespace Aw.Sindri.Infrastructure.WorkflowDocs;

/// <summary>The workflow doc pages embedded from docs/workflows/*.md (resource names 'workflow-docs/&lt;slug&gt;.md').</summary>
public static class EmbeddedWorkflowDocs
{
    private const string Prefix = "workflow-docs/";
    private const string Extension = ".md";

    /// <summary>Reads every embedded page.</summary>
    /// <returns>The pages, ordered by slug.</returns>
    public static IReadOnlyList<WorkflowDocPage> Load()
    {
        var assembly = typeof(EmbeddedWorkflowDocs).Assembly;
        var pages = new List<WorkflowDocPage>();
        foreach (var name in assembly.GetManifestResourceNames().Where(static n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(Extension, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("workflow doc " + name + " is not embedded");
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            var slug = name[Prefix.Length..^Extension.Length];
            pages.Add(new WorkflowDocPage(slug, Title(text) ?? slug, text));
        }

        pages.Sort(static (a, b) => string.CompareOrdinal(a.Slug, b.Slug));
        return pages;
    }

    private static string? Title(string text) =>
        text.Split('\n').Select(static l => l.TrimEnd('\r')).FirstOrDefault(static l => l.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim();
}
