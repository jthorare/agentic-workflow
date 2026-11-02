using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Core.Projects;

/// <summary>A project write was accepted (projects page spec §5.3): the command and its ledger sequence (the read-your-writes token), what it was about, and the version it moved to.</summary>
/// <param name="Accepted">The command id and the sequence its event was written at.</param>
/// <param name="Project">The project's 'prj_…' id, or the Linear key for a hide.</param>
/// <param name="Version">The version the project is at now (the hide version for a hide).</param>
/// <param name="Name">What to call it in a sentence: the typed name, else the Linear key, else the id.</param>
public sealed record ProjectAccepted(CommandAccepted Accepted, string Project, long Version, string Name);
