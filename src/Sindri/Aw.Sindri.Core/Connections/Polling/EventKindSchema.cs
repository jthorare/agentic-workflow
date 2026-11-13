using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>
/// What one event kind hands a workflow (#3201): its payload fields with their types ('string', 'number', 'boolean', 'array', the type names a block's outputs use), and one sample event.
/// Every declared field is present in every event of the kind, so the sample has exactly these fields.
/// </summary>
/// <param name="Kind">The catalog id ('github.pr.checks').</param>
/// <param name="Fields">The payload field names with their types.</param>
/// <param name="Sample">One example payload: the same field names as <paramref name="Fields"/>, values of the declared types.</param>
public sealed record EventKindSchema(string Kind, IReadOnlyDictionary<string, string> Fields, JsonObject Sample);
