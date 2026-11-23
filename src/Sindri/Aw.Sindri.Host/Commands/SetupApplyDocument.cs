using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Host.Commands;

/// <summary>What 'sindri setup --apply --json' answers (first-run spec §5.1): the repos it added, found already there or couldn't add, whether it started serve, the setup pass and the steps after it.</summary>
/// <param name="Added">The paths of the repos it added.</param>
/// <param name="Already">The paths of the repos the profile already named.</param>
/// <param name="Failed">The repos it couldn't add, each with the code and message of why.</param>
/// <param name="ServeStarted">True when Sindri wasn't running and this call started it.</param>
/// <param name="SetupPass">How the setup pass ended.</param>
/// <param name="Steps">The setup steps read after the pass (GET /sindri/setup); empty when that read failed.</param>
internal sealed record SetupApplyDocument(IReadOnlyList<string> Added, IReadOnlyList<string> Already, IReadOnlyList<SetupApplyFailure> Failed, bool ServeStarted, SetupPassOutcome SetupPass, IReadOnlyList<SetupStep> Steps);
