using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>
/// A 'with:' argument or a child input (spec §3.3): exactly one of a literal (a string, number or bool, or a list
/// of these), '{ fact }' or '{ profile }'. A capability parameter takes only a literal or '{ profile }'
/// (RCP-CAP-007). The short form is the bare literal.
/// </summary>
/// <param name="Literal">The literal value.</param>
/// <param name="Fact">A fact the engine reads when the block runs.</param>
/// <param name="Profile">A profile key path the engine reads when the block runs.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WithValue(JsonElement? Literal, FactKey? Fact, string? Profile);
