using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.GetLibrary;

namespace Aw.Sindri.Host.Commands;

/// <summary>The text of 'sindri library list' and 'sindri library show'.</summary>
internal static class LibraryText
{
    /// <summary>The list: a head line, then each doc with errors, each near miss, each folder that can't be read, then the cap line.</summary>
    /// <param name="list">The answer.</param>
    /// <param name="only">Which part to print alone; <see cref="Part.All"/> prints the head line and everything.</param>
    /// <returns>The lines, ending with a newline.</returns>
    internal static string List(GetLibraryResponse list, Part only)
    {
        var text = new StringBuilder();
        if (only == Part.Errors)
        {
            Broken(text, list, alone: true);
        }
        else if (only == Part.NearMisses)
        {
            Misses(text, list, alone: true);
        }
        else
        {
            _ = text.Append(Head(list)).Append('\n');
            Broken(text, list, alone: false);
            Misses(text, list, alone: false);
            foreach (var root in list.UnreadableRoots)
            {
                _ = text.Append(root.Root).Append(": can't be read (").Append(root.Reason).Append(")\n");
            }
        }

        if (list.CappedText is { Length: > 0 } capped && only != Part.NearMisses)
        {
            _ = text.Append(capped).Append('\n');
        }

        return text.ToString();
    }

    private static void Broken(StringBuilder text, GetLibraryResponse list, bool alone)
    {
        if (alone && list.Broken.Count == 0)
        {
            _ = text.Append("No docs with errors.\n");
        }

        foreach (var doc in list.Broken)
        {
            _ = text.Append(doc.Path).Append('\n');
            foreach (var error in doc.Errors)
            {
                _ = text.Append("  ").Append(error.Rule).Append(' ').Append(error.Text).Append('\n');
            }
        }
    }

    private static void Misses(StringBuilder text, GetLibraryResponse list, bool alone)
    {
        if (alone && list.NearMisses.Count == 0)
        {
            _ = text.Append("No near misses.\n");
        }

        foreach (var miss in list.NearMisses)
        {
            _ = text.Append(miss.Path).Append('\n').Append("  ").Append(miss.Rule).Append(' ').Append(miss.Text).Append('\n');
        }
    }

    /// <summary>One doc: its id, kind, status and title, its file, where it sits and what is wrong.</summary>
    /// <param name="doc">The doc.</param>
    /// <returns>The lines, ending with a newline.</returns>
    internal static string Doc(LibraryDocView doc)
    {
        var text = new StringBuilder();
        _ = text.Append(doc.Slug).Append("  ").Append(doc.Kind ?? "?").Append(", ").Append(doc.Status ?? "?").Append("  ").Append(doc.Title).Append('\n');
        _ = text.Append(doc.Path).Append('\n');
        _ = text.Append("root ").Append(doc.Root).Append(" · scope ").Append(doc.Scope).Append('\n');
        if (doc.Project is { } project)
        {
            _ = text.Append("project ").Append(project).Append('\n');
        }

        if (doc.Repos.Count > 0)
        {
            _ = text.Append("repos ").AppendJoin(", ", doc.Repos).Append('\n');
        }

        if (doc.Issues.Count > 0)
        {
            _ = text.Append("issues ").AppendJoin(", ", doc.Issues).Append('\n');
        }

        foreach (var error in doc.Errors)
        {
            _ = text.Append("  ").Append(error.Rule).Append(' ').Append(error.Text).Append('\n');
        }

        return text.ToString();
    }

    private static string Head(GetLibraryResponse list)
    {
        if (list.Docs == 0)
        {
            return "Library: no design docs yet";
        }

        var count = list.Docs.ToString(CultureInfo.InvariantCulture);
        return $"Library: {count} {(list.Docs == 1 ? "doc" : "docs")}, {list.WithErrors.ToString(CultureInfo.InvariantCulture)} with errors";
    }

    /// <summary>Which part of the list a flag asks for.</summary>
    internal enum Part
    {
        /// <summary>Everything.</summary>
        All,

        /// <summary>Only the docs with errors.</summary>
        Errors,

        /// <summary>Only the near misses.</summary>
        NearMisses,
    }
}
