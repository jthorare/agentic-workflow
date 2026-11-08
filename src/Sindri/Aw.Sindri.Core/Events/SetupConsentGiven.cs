using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'setup.consent.given' payload (setup-once spec §7.5): the owner agreed, by approving a profile or pressing "Turn on for your repos", that Sindri may do these steps on its own. Key 'setup.consent.given:&lt;profileHash&gt;'.</summary>
/// <param name="CommandId">The command that carried the consent.</param>
/// <param name="Steps">The steps it covers ('mcp', 'map').</param>
/// <param name="ProfileHash">The approved profile it was given for.</param>
public sealed record SetupConsentGiven(CommandId CommandId, IReadOnlyList<string> Steps, ProfileHash ProfileHash);
