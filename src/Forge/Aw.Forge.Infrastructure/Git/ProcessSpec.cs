namespace Aw.Forge.Infrastructure.Git;

/// <summary>One program to run: the exact arguments, the variables added to a scratch environment, where, and for how long.</summary>
/// <param name="File">The program.</param>
/// <param name="Arguments">Its arguments, one element each (no shell).</param>
/// <param name="Environment">Variables added to the minimal inherited environment.</param>
/// <param name="WorkingDirectory">Where to run it, or null for the current directory.</param>
/// <param name="Timeout">How long it may run before it is killed.</param>
internal sealed record ProcessSpec(
    string File,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    string? WorkingDirectory,
    TimeSpan Timeout);
