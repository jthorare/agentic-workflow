namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>What a T3 tool answered: its structured result, or T3's own failure (a missing thread, a refused mode).</summary>
/// <typeparam name="T">The result's shape.</typeparam>
/// <param name="Value">The result, or null when the tool failed.</param>
/// <param name="Failure">T3's failure, or null when the tool succeeded.</param>
internal sealed record T3Reply<T>(T? Value, T3Wire.Failure? Failure);
