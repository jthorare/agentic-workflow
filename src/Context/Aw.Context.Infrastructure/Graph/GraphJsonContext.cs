using System.Text.Json.Serialization;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The proposal payload stored in map_override.payload and the surface lists in proposal_run.surfaces. Internal to Infrastructure, used only through <see cref="GraphPayload"/>.</summary>
[JsonSerializable(typeof(ProposalPayload))]
[JsonSerializable(typeof(ProposedSurface[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class GraphJsonContext : JsonSerializerContext;
