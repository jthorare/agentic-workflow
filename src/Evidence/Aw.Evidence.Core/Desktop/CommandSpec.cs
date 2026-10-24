namespace Aw.Evidence.Core.Desktop;

/// <summary>A program to run for a desktop run.</summary>
/// <param name="Argv">The program, then its arguments; a relative program is the working directory's.</param>
/// <param name="WorkingDirectory">Where it runs (the repo).</param>
/// <param name="Environment">Variables added to the inherited environment.</param>
/// <param name="Timeout">How long it may run.</param>
public sealed record CommandSpec(IReadOnlyList<string> Argv, string WorkingDirectory, IReadOnlyDictionary<string, string> Environment, TimeSpan Timeout);
