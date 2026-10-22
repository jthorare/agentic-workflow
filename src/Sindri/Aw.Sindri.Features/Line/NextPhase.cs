using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

/// <summary>The phase to start, as <c language="text">fact:nextPhase</c> carries it (line spec §8.2).</summary>
/// <param name="Row">The phase's row.</param>
/// <param name="Attempt">Its attempt.</param>
/// <param name="Stack">The phase it is stacked on, or <see cref="PhaseBoard.Default"/> for the default branch.</param>
internal sealed record NextPhase(PhaseRow Row, int Attempt, PhaseRow Stack)
{
    /// <summary><c language="text">fact:nextPhase</c>: <c language="text">{ phase, attempt, packet, base: { rule, ref }, stackParent }</c>.</summary>
    /// <returns>The JSON.</returns>
    internal JsonObject ToJson()
    {
        var stacked = !ReferenceEquals(Stack, PhaseBoard.Default);
        var packet = (JsonObject)Row.Entry.DeepClone();
        if (Row.Packet is not null)
        {
            packet["hash"] = Row.Packet;   // a retry starts from the newest packet frozen under the phase's id, amendments included
        }

        return new JsonObject
        {
            ["phase"] = Row.Phase,
            ["attempt"] = Attempt,
            ["packet"] = packet,
            ["base"] = new JsonObject { ["rule"] = stacked ? "dependency-head" : "default-branch", ["ref"] = stacked ? Stack.HeadBranch : string.Empty },
            ["stackParent"] = stacked ? Stack.Phase : null,
        };
    }
}
