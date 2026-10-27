namespace Aw.Sindri.Ui.Api;

/// <summary>The latest run of a service.</summary>
/// <param name="Slot">The slot it ran for.</param>
/// <param name="Outcome">'ok' or 'failed'.</param>
/// <param name="Code">The SND code of a failed run, or null.</param>
/// <param name="RanAt">When it ran.</param>
/// <param name="Detail">The failure's message, already scrubbed by the server, or null.</param>
internal sealed record ServiceRunView(DateTimeOffset Slot, string Outcome, string? Code, DateTimeOffset RanAt, string? Detail = null);
