using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'sandbox.allowed' payload (sandbox spec §3.6): a caller allowed a blocked host or home path in the live profile, under the blocked run's repo. Key 'sandbox.allowed:&lt;command id&gt;'; the envelope's actor is the caller.</summary>
/// <param name="CommandId">The 'AllowDenial' command's id.</param>
/// <param name="Launch">The blocked run.</param>
/// <param name="Repo">The repo the entry was allowed for.</param>
/// <param name="Kind">'network' or 'path', as the denial's kind is named.</param>
/// <param name="Entry">The entry as the profile holds it.</param>
public sealed record SandboxAllowed(CommandId CommandId, LaunchId Launch, RepoName Repo, string Kind, string Entry);
