using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>One row of the trace table.</summary>
/// <param name="Item">The acceptance item.</param>
/// <param name="Phase">The phase that carries it.</param>
/// <param name="Signals">The proof signals for it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopTraceRow(
    string Item,
    string Phase,
    IReadOnlyList<StopSignal> Signals);
