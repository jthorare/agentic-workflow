using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One command the instance accepts now (UI spec E-13): the catalog action the app calls, its CLI line, and for a recipe verb the verb.</summary>
/// <param name="ActionId">The catalog action ("ApplyCommand:command" for a recipe verb).</param>
/// <param name="Cli">The 'sindri workflow …' line.</param>
/// <param name="Verb">The recipe verb of an "ApplyCommand:command" entry ("merge", "check"); null for every other command.</param>
/// <param name="Destructive">True when the verb is destructive; read as optional, a missing flag is not destructive.</param>
internal sealed record AllowedCommandView(
    string ActionId,
    string Cli,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Verb = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Destructive = null);
