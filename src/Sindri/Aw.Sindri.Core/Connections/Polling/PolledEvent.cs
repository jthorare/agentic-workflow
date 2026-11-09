namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>
/// One event a provider reported. The delivery id is stable: the same provider event gives the same id on every poll and every
/// catch-up, so it is the run's occurrence and a second start with it is refused.
/// </summary>
/// <param name="DeliveryKey">The stable id ('github:1001', 'linear:h-55:label:bug').</param>
/// <param name="Kind">Which catalog event it is.</param>
/// <param name="OccurredAt">When the provider says it happened.</param>
/// <param name="Fields">Payload fields by name ('repo', 'number', 'title', 'url', 'author'). The values are provider text: untrusted, fenced wherever they reach a prompt.</param>
public sealed record PolledEvent(string DeliveryKey, EventKind Kind, DateTimeOffset OccurredAt, IReadOnlyDictionary<string, string> Fields);
