using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.GetInstanceLine;

/// <summary>One row of <c language="text">fact:phaseBoard</c> as the line board reads it (<c language="text">{ phases: [{ phase, state, attempt, parkReason? }] }</c>, in plan order).</summary>
/// <param name="Phase">The phase's id.</param>
/// <param name="State">The state's name on the board; 'waiting' for one the board does not name.</param>
/// <param name="Attempt">The attempt that started last; 0 before the first.</param>
/// <param name="ParkReason">Why the phase is parked.</param>
internal sealed record BoardRow(LinePhaseId Phase, string State, int Attempt, string? ParkReason)
{
    private static readonly string[] _states = ["waiting", "running", "shipped", "merged", "failed", "skipped"];

    /// <summary>The rows the fact holds; none when it is not a board. A row with no phase id is left out.</summary>
    /// <param name="board">The fact.</param>
    /// <returns>The rows, in plan order.</returns>
    internal static IReadOnlyList<BoardRow> Read(JsonNode? board)
    {
        var rows = new List<BoardRow>();
        foreach (var p in LineJson.Items(board, "phases"))
        {
            if (LineJson.Str(p, "phase") is { Length: > 0 } phase)
            {
                var state = LineJson.Str(p, "state") is { } named && _states.Contains(named, StringComparer.Ordinal) ? named : "waiting";
                rows.Add(new BoardRow(LinePhaseId.From(phase), state, LineJson.Number(p, "attempt") ?? 0, LineJson.Str(p, "parkReason")));
            }
        }

        return rows;
    }
}
