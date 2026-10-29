namespace Aw.Context.Core.Extract;

internal sealed record JourneyRead(IReadOnlyList<Journey> Journeys, int Unsupported);
