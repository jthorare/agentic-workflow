using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Tests.CheckTraits;

/// <summary>An ITraitCheckLog that keeps its entries.</summary>
public sealed class RecordingTraitLog : ITraitCheckLog
{
    private readonly List<JsonObject> _entries = [];

    public IReadOnlyList<JsonObject> Entries => _entries;

    public void Append(JsonObject entry) => _entries.Add((JsonObject)entry.DeepClone());
}
