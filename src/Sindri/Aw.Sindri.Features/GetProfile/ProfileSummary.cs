namespace Aw.Sindri.Features.GetProfile;

/// <summary>What approving the live profile would let Sindri do, in plain words (setup-once spec §5.2 "Preview").</summary>
/// <param name="Repos">One per repo the live profile names and the approved one does not (all of them when nothing is approved yet).</param>
/// <param name="Services">One sentence per service the live profile enables or runs more often than the approved one.</param>
/// <param name="Allowance">One sentence per effect the live profile allows and the approved one does not, then one per budget or limit it adds or raises.</param>
/// <param name="Connections">One sentence per outside connection the live profile adds or changes (the forge, judge providers, agents, MCP).</param>
/// <param name="HumanOnly">One sentence per step the live profile keeps for the owner alone that the approved one did not.</param>
public sealed record ProfileSummary(IReadOnlyList<RepoPreview> Repos, IReadOnlyList<string> Services, IReadOnlyList<string> Allowance, IReadOnlyList<string> Connections, IReadOnlyList<string> HumanOnly);
