using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Instance names and generated inputs (engine spec section 3.11): renders a recipe's 'name' template once, at start, and an
/// input's 'generate' template when the start supplies no value. Pure: the workflow reads the facts' content and passes it in.
/// </summary>
public static class InstanceNames
{
    /// <summary>The longest rendered fact value in a name.</summary>
    public const int ValueLength = 40;

    /// <summary>The longest rendered name.</summary>
    public const int NameLength = 80;

    /// <summary>The facts the recipe's name reads, in order of first use; empty when it has no name or the template is malformed.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>The fact keys.</returns>
    public static IReadOnlyList<FactKey> FactsRead(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return recipe.Name is { } name ? NameTemplate.Parse(name).Facts : [];
    }

    /// <summary>The value of a 'generate' template: its literals and the UTC start time. A template that reads facts, or is malformed, renders null (the validator refuses both).</summary>
    /// <param name="template">The template.</param>
    /// <param name="start">The start time.</param>
    /// <returns>The text, or null.</returns>
    public static string? Generate(string template, DateTimeOffset start)
    {
        ArgumentNullException.ThrowIfNull(template);
        var parsed = NameTemplate.Parse(template);
        return parsed.Problem is not null || parsed.Facts.Count > 0
            ? null
            : Fill(parsed, start, static _ => string.Empty);
    }

    /// <summary>
    /// The rendered name of an instance of <paramref name="recipe"/>, or null when the recipe has none. Each fact placeholder is its
    /// input's value by kind, with control characters replaced by a space and cut to <see cref="ValueLength"/>; a value that can't be
    /// read is the input's bare key. The whole name is cut to <see cref="NameLength"/> and trimmed; it may come out empty.
    /// </summary>
    /// <param name="recipe">The recipe and its input facts.</param>
    /// <param name="item">The item the instance is on.</param>
    /// <param name="contents">The stored content of each fact the name reads (<see cref="FactsRead"/>); a missing or null entry was not read.</param>
    /// <param name="start">The start time.</param>
    /// <returns>The name, or null when the recipe declares none.</returns>
    public static string? Render(InterpretedRecipe recipe, ItemKey item, IReadOnlyDictionary<FactKey, string?> contents, DateTimeOffset start)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(contents);
        if (recipe.Recipe.Name is not { } template)
        {
            return null;
        }

        var parsed = NameTemplate.Parse(template);
        if (parsed.Problem is not null)
        {
            return null;
        }

        var filled = Fill(parsed, start, key => ValueOf(recipe, item, key, contents.GetValueOrDefault(key)));
        return SubjectRefs.Cut(filled, NameLength).Trim();
    }

    private static string Fill(NameTemplate template, DateTimeOffset start, Func<FactKey, string> value)
    {
        var utc = start.ToUniversalTime();
        var text = new StringBuilder();
        foreach (var part in template.Parts)
        {
            _ = text.Append(part switch
            {
                NameTemplate.LiteralPart literal => literal.Text,
                NameTemplate.StartPart { IsDate: true } => utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                NameTemplate.StartPart => utc.ToString("HH:mm", CultureInfo.InvariantCulture),
                NameTemplate.FactPart fact => value(fact.Key),
                _ => string.Empty,
            });
        }

        return text.ToString();
    }

    private static string ValueOf(InterpretedRecipe recipe, ItemKey item, FactKey key, string? content)
    {
        var bare = SubjectRefs.Bare(key.Value);
        var input = recipe.Recipe.Inputs.FirstOrDefault(i => i.Key == key);
        var read = input is null ? null : Read(recipe, input, item, content);
        return SubjectRefs.Cut(Flatten(string.IsNullOrWhiteSpace(read) ? bare : read), ValueLength);
    }

    private static string? Read(InterpretedRecipe recipe, RecipeInput input, ItemKey item, string? content) => input.Kind switch
    {
        "item" => item.Value,
        "file" => recipe.Inputs.TryGetValue(input.Key, out var file) ? file.Name : null,
        "text" => content is null ? null : SubjectRefs.FirstLine(content),
        "link" => content is null ? null : LinkText(content),
        "linear-project" or "webhook" or "payload" or "packet" => content is null ? null : SubjectRefs.PayloadId(content),
        _ => null,
    };

    private static string? LinkText(string content)
    {
        var (url, title) = SubjectRefs.LinkOf(content);
        return title ?? SubjectRefs.Host(url);
    }

    private static string Flatten(string value)
    {
        var text = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            _ = text.Append(char.IsControl(c) ? ' ' : c);
        }

        return text.ToString();
    }
}
