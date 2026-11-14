using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'setup.first_workflow.created' payload (first-run spec §2 decision 19, E2): the setup service made and activated hello-sindri. Key 'setup.first_workflow.created', one per box, so a deleted or dismissed hello-sindri is never made again.</summary>
/// <param name="CommandId">The id of the service run that recorded it.</param>
/// <param name="Slug">The workflow's slug, 'hello-sindri'.</param>
/// <param name="Repo">The repo its item is made in: the first repo of the profile.</param>
public sealed record SetupFirstWorkflowCreated(CommandId CommandId, WorkflowSlug Slug, RepoName Repo);
