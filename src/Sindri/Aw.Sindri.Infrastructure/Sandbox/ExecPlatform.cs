using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>What 'sandbox exec' needs from the platform around its command (#1936).</summary>
/// <param name="Scope">Wraps the command in the platform's scope (<see cref="ExecScope"/>).</param>
/// <param name="Ended">Cleans up what the scope left behind once the command ends (macOS: its group file, and the terminal back to sindri); none on Linux.</param>
internal sealed record ExecPlatform(Func<LaunchId, ScopeLimits, IReadOnlyList<string>, IReadOnlyList<string>> Scope, Action<LaunchId>? Ended = null);
