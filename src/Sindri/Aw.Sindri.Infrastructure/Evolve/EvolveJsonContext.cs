using System.Text.Json.Serialization;
using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Infrastructure.Evolve;

/// <summary>The JSON the proposal store writes into the ledger: a proposal's body and a session's turns (used through AwJson.CreateOptions).</summary>
[JsonSerializable(typeof(Proposal))]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class EvolveJsonContext : JsonSerializerContext;
