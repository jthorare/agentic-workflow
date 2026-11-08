using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.OnboardRepo;

/// <summary>
/// Onboard a repo of the approved profile by hand (setup-once spec §5.1 "After approval", POST /setup/repos/onboard; a desk action like AddRepo, so a session is refused): the same onboarding the setup service runs when a profile is approved.
/// A job keyed by 'onboard-' and the first 16 hex of the sha256 of the repo's name. It never approves the profile: a repo the approved profile does not name is SND-SETUP-004 before any job exists.
/// </summary>
/// <param name="Path">The absolute path of a folder inside the repo's checkout.</param>
public sealed record OnboardRepoRequest(string Path) : IJob<OnboardedRepo>;
