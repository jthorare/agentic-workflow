namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>One progress line of a job.</summary>
/// <param name="Round">The round it belongs to.</param>
/// <param name="Line">The line, already scrubbed by Sindri.</param>
/// <param name="At">When it was written.</param>
internal sealed record JobProgressView(int Round, string Line, DateTimeOffset At);
