using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.PlanContext;

/// <summary>The design's surfaces and stop 2's answers into the context map, after the owner's approve (line spec LN-CTX1).</summary>
/// <param name="Facts">The design packet's files under 'design/' (coverage.json, escapes.json, decisions.json, and the scope map under 'scope/' when it is held) and the plan's answer log as 'answers.jsonl'.</param>
/// <param name="Run">The plan instance's address: the run the proposals are filed under.</param>
/// <param name="Repo">The repo the surfaces were found in.</param>
/// <param name="Packet">The hash of the frozen plan packet the answers are keyed by.</param>
/// <param name="Approved">Whether the owner approved at stop 2: nothing is proposed from an unapproved draft.</param>
public sealed record ProposePlanContextRequest(IReadOnlyDictionary<string, string> Facts, InstanceAddress Run, RepoName Repo, string Packet, bool Approved);
