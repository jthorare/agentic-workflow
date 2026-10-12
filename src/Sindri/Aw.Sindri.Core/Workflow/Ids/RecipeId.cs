using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Ids;

/// <summary>A recipe's id (spec §3.2): 'workflow.' and a kebab-case name, for example 'workflow.step', at most 64 characters.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RecipeId>))]
public readonly partial struct RecipeId : ITypedId<RecipeId, string>
{
    /// <summary>The name after 'workflow.' ('step' in 'workflow.step'); the CLI's addresses use it.</summary>
    public string Name => Value["workflow.".Length..];

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A recipe id is 'workflow.<name>' with a kebab-case name, at most 64 characters");

    [GeneratedRegex("^workflow\\.[a-z][a-z0-9]*(-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 64 ? string.Empty : input;
}
