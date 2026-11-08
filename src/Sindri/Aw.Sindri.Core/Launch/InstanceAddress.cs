using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Vogen;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// An instance's CLI address (spec §5.1, §6.1): a root is '{item}/{recipe name}/{attempt}' ('T-3/item/1'); a child
/// is '{parent}/{child recipe name}.{parent entry}' ('T-3/item/1/step.4'); a linked instance (LN-WF1) is
/// '{parent}/{recipe name}.{id}.{attempt}' ('T-3/line/1/phase.p2.1'). At most 256 characters.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<InstanceAddress>))]
public readonly partial struct InstanceAddress : ITypedId<InstanceAddress, string>
{
    /// <summary>A root instance's address.</summary>
    /// <param name="item">The item.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="attempt">The attempt, from 1.</param>
    /// <returns>'{item}/{recipe name}/{attempt}'.</returns>
    public static InstanceAddress Root(ItemKey item, RecipeId recipe, int attempt) =>
        From(string.Create(CultureInfo.InvariantCulture, $"{item.Value}/{recipe.Name}/{attempt}"));

    /// <summary>A child's address under this one.</summary>
    /// <param name="recipe">The child's recipe.</param>
    /// <param name="entry">The parent frame's entry that created the child.</param>
    /// <returns>'{this}/{child recipe name}.{entry}'.</returns>
    public InstanceAddress Child(RecipeId recipe, int entry) =>
        From(string.Create(CultureInfo.InvariantCulture, $"{Value}/{recipe.Name}.{entry}"));

    /// <summary>A linked instance's address under this one (line spec LN-WF1): started by an effect, not by a wait, and named by what it is for.</summary>
    /// <param name="recipe">The linked instance's recipe.</param>
    /// <param name="id">What it is for (a phase's id, as the plan names it): its <see cref="LinkedId"/> names the address, so two ids with the same slug share one.</param>
    /// <param name="attempt">The attempt, from 1.</param>
    /// <returns>'{this}/{recipe name}.{slug of id}.{attempt}'.</returns>
    public InstanceAddress Linked(RecipeId recipe, string id, int attempt) =>
        From(string.Create(CultureInfo.InvariantCulture, $"{Value}/{recipe.Name}.{LinkedId(id) ?? id}.{attempt}"));

    /// <summary>The address segment a linked instance's id becomes: lowercase, every run of characters outside letters and digits one hyphen, no hyphen at either end, at most 64 characters.</summary>
    /// <param name="id">The id as its owner names it (a plan's phase id, "P1").</param>
    /// <returns>The slug ("p1"), or null when the id is empty, over 64 characters or has no letter or digit.</returns>
    public static string? LinkedId(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (id.Length is 0 or > 64)
        {
            return null;
        }

        var slug = new StringBuilder(id.Length);
        foreach (var c in id)
        {
            var lower = char.ToLowerInvariant(c);
            if (lower is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                slug.Append(lower);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var text = slug.ToString().TrimEnd('-');
        return text.Length == 0 ? null : text;
    }

    /// <summary>The instance this one is linked to (LN-WF1): the address without its last segment when that segment has the linked form; null for a root or a child.</summary>
    /// <returns>The parent's address, or null.</returns>
    public InstanceAddress? LinkedParent()
    {
        var cut = Value.LastIndexOf('/');
        return cut > 0 && LinkedShape.IsMatch(Value[(cut + 1)..]) ? From(Value[..cut]) : null;
    }

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("An address is '{item}/{recipe}/{attempt}', then '/{recipe}.{entry}' per child generation");

    [GeneratedRegex("^[A-Z][A-Z0-9]*-[A-Za-z0-9]+/[a-z][a-z0-9]*(-[a-z0-9]+)*/[1-9][0-9]*(/[a-z][a-z0-9]*(-[a-z0-9]+)*(\\.[a-z0-9][a-z0-9-]*)?\\.[1-9][0-9]*)*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*\\.[a-z0-9][a-z0-9-]*\\.[1-9][0-9]*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LinkedShape { get; }

    private static string NormalizeInput(string input) => input.Length > 256 ? string.Empty : input;
}
