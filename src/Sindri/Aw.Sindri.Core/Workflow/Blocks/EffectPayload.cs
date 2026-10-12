using System.Text.Json;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>An effect's outbox command body (spec §2, P6).</summary>
/// <param name="Body">The command's JSON body.</param>
public sealed record EffectPayload(JsonElement Body);
