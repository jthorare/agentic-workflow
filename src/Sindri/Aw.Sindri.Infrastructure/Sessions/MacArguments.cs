namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>What a process's argument block gives the collector: argv[0] and the allowlisted environment. Nothing else of the block is kept.</summary>
/// <param name="Argv0">The program name argv[0] gives, or null.</param>
/// <param name="Environment">The allowlisted names and values.</param>
internal sealed record MacArguments(string? Argv0, IReadOnlyDictionary<string, string> Environment);
