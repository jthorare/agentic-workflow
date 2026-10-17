namespace Aw.Sindri.Tests.Launch.Argv;

/// <summary>One argv golden: the entry, the mode and the argument list exactly as the builder returns it.</summary>
internal sealed record ArgvGolden(string Agent, string Mode, IReadOnlyList<string> Argv);
