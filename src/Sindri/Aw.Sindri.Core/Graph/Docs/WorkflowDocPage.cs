using System.Text;

namespace Aw.Sindri.Core.Graph.Docs;

/// <summary>One page of the workflow docs: a concept ('references') or one block type ('block-schedule').</summary>
public sealed class WorkflowDocPage
{
    /// <summary>The prefix of a block page's id; the rest is the block's type name.</summary>
    public const string BlockPrefix = "block-";

    /// <summary>Makes a page.</summary>
    /// <param name="slug">The page's slug, its file name without '.md': 'spec', or 'block-' and a block type.</param>
    /// <param name="title">The page's title, from its first heading.</param>
    /// <param name="text">The page's Markdown.</param>
    public WorkflowDocPage(string slug, string title, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(text);
        Slug = slug;
        Title = title;
        Text = text;
    }

    /// <summary>The page's slug.</summary>
    public string Slug { get; }

    /// <summary>The page's title.</summary>
    public string Title { get; }

    /// <summary>The page's Markdown.</summary>
    public string Text { get; }

    /// <summary>The block type a block page describes, such as 'schedule'; null for a concept page.</summary>
    public string? BlockType => Slug.StartsWith(BlockPrefix, StringComparison.Ordinal) ? Slug[BlockPrefix.Length..] : null;

    /// <summary>The bodies of the page's fenced json blocks, in order. Each one is a whole workflow spec.</summary>
    /// <returns>The JSON texts; empty when the page has none.</returns>
    public IReadOnlyList<string> JsonExamples()
    {
        var found = new List<string>();
        StringBuilder? open = null;
        foreach (var raw in Text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (open is null)
            {
                open = string.Equals(line.Trim(), "```json", StringComparison.Ordinal) ? new StringBuilder() : null;
            }
            else if (string.Equals(line.Trim(), "```", StringComparison.Ordinal))
            {
                found.Add(open.ToString().TrimEnd('\n'));
                open = null;
            }
            else
            {
                open.Append(line).Append('\n');
            }
        }

        return found;
    }
}
