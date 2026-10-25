using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Commands;

/// <summary>What an HTTP request's caller headers say, after parsing (API spec §6): the input of <see cref="CallerContext.Http"/>.</summary>
/// <param name="Class">Human or session (service never arrives over HTTP; it is read as a session).</param>
/// <param name="Terminal">The X-Sindri-Terminal claim; honoured only for a human.</param>
/// <param name="Client">The X-Sindri-Client label.</param>
/// <param name="Repo">The X-Sindri-Repo scope, or null.</param>
/// <param name="Launch">The X-Sindri-Launch id; a human never carries one.</param>
public sealed record HttpCaller(CallerClass Class, bool Terminal, ClientLabel Client, RepoName? Repo, LaunchId? Launch = null);
