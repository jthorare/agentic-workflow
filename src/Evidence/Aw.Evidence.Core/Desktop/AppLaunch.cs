namespace Aw.Evidence.Core.Desktop;

/// <summary>An app to start.</summary>
/// <param name="Argv">The program, then its arguments; a relative program is the working directory's.</param>
/// <param name="WorkingDirectory">Where it runs (the repo).</param>
/// <param name="Environment">Variables added to the inherited environment.</param>
public sealed record AppLaunch(IReadOnlyList<string> Argv, string WorkingDirectory, IReadOnlyDictionary<string, string> Environment);
