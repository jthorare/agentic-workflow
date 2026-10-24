using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>POST /sindri/context/map/promote's answer: the promoted map (the server no longer issues a challenge, owner q93; the old field reads as absent). The app never sends it.</summary>
/// <param name="Draft">Draft, as served.</param>
/// <param name="Promoted">Promoted, as served.</param>
/// <param name="Sequence">Sequence, as served.</param>
/// <param name="Challenge">Challenge, as served.</param>
internal sealed record PromotePreview(
    string Draft,
    JsonElement? Promoted,
    long? Sequence,
    ReviewChallenge? Challenge = null);
