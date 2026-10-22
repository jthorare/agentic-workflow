using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Engine;

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
    /// <param name="id">What it is for (a phase's id): lowercase letters, digits and hyphens.</param>
    /// <param name="attempt">The attempt, from 1.</param>
    /// <returns>'{this}/{recipe name}.{id}.{attempt}'.</returns>
    public InstanceAddress Linked(RecipeId recipe, string id, int attempt) =>
        From(string.Create(CultureInfo.InvariantCulture, $"{Value}/{recipe.Name}.{id}.{attempt}"));

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
