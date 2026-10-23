using System.Text.Json.Serialization;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The answer a status layout offers (E-23).</summary>
/// <param name="Kind">The kind.</param>
/// <param name="ActionId">The catalog action id of this answer kind ("ApplyCommand:answer-grant", "-grant-tokens", "-accept" are protected; "-text", "-resume", "-reject" are desk).</param>
/// <param name="Class">The catalog row's action class: "desk" or "protected".</param>
/// <param name="Cli">The exact command printed.</param>
/// <param name="AtTerminal">Whether it needs a terminal.</param>
/// <param name="Via">"child" when this line is a pointer to a child that decides the answer, not an answer form to run (a needs-you row for a nested park carries the child's own answer in its cli); null (omitted) otherwise.</param>
/// <param name="Child">The child's address, on a "child" pointer when it is known; null (omitted) otherwise.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusAnswer(
    AnswerKind Kind,
    ActionId ActionId,
    ActionClass Class,
    string Cli,
    bool AtTerminal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Child = null);
