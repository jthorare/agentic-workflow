using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Loops;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The loop a node execution runs in: its shape, the item's position counted from 1 and the item.</summary>
internal sealed record GraphLoopContext(LoopShape Shape, int ItemIndex, JsonNode? Item);
