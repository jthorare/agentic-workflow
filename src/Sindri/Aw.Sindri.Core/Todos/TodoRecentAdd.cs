namespace Aw.Sindri.Core.Todos;

/// <summary>One add inside the hour (todo spec §4.4 "The hourly limit").</summary>
/// <param name="At">When it was recorded.</param>
/// <param name="By">Who wrote it.</param>
/// <param name="Via">Its 'via'.</param>
public sealed record TodoRecentAdd(DateTimeOffset At, string By, string? Via);
