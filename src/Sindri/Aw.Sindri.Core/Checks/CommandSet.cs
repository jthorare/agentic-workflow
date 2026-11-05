using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Checks;

/// <summary>The repo's commands for a build or ship Step, and how long each may run (line spec LN-WF9, decision A).</summary>
/// <param name="Commands">The commands, in the profile's order; none when none are configured.</param>
/// <param name="Each">The longest one command may run: the profile's 'workflow.steps.longMax' (LN-WF7).</param>
/// <param name="OutputLimit">The most characters of each command's output to keep (the end of it); <see cref="CheckRun.TailLimit"/> for the repo's commands.</param>
public sealed record CommandSet(IReadOnlyList<BuildCommand> Commands, TimeSpan Each, int OutputLimit = CheckRun.TailLimit);
