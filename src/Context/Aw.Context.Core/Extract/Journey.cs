namespace Aw.Context.Core.Extract;

internal sealed record Journey(string Title, IReadOnlyList<JourneyStep> Steps);
