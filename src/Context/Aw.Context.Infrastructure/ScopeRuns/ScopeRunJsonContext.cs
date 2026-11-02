using System.Text.Json.Serialization;
using Aw.Context.Core.ScopeGraph;

namespace Aw.Context.Infrastructure.ScopeRuns;

/// <summary>The scoping graph as the JSON scope_graph.graph holds it. Internal to Infrastructure.</summary>
[JsonSerializable(typeof(ScopeGraphView))]
internal sealed partial class ScopeRunJsonContext : JsonSerializerContext;
