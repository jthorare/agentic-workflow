using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Host;

/// <summary>What a request's caller headers say (API spec §6), before the token and prover checks.</summary>
/// <param name="Class">Human or session (always session with a launch).</param>
/// <param name="Terminal">The X-Sindri-Terminal claim, kept only with human.</param>
/// <param name="Client">The X-Sindri-Client label (None for a missing or unknown one).</param>
/// <param name="Repo">The X-Sindri-Repo scope, or null.</param>
/// <param name="UnknownClient">An X-Sindri-Client value Sindri doesn't know (counted as no label, logged), or null.</param>
/// <param name="Launch">The X-Sindri-Launch id, present whatever the caller header says (it makes the caller a session), or null.</param>
internal sealed record ParsedCaller(CallerClass Class, bool Terminal, ClientLabel Client, RepoName? Repo, string? UnknownClient, LaunchId? Launch);
