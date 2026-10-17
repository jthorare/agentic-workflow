using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.OpenItem;

/// <summary>An open was accepted (UI spec E-1 'LaunchRequested(accepted, LaunchId)'): the launcher service answers it with 'launch.started' or 'launch.failed'; 'GET /sindri/launches/{launchId}' shows which.</summary>
/// <param name="Accepted">The command and the ledger sequence of its 'item.open-requested'.</param>
/// <param name="Launch">The request's launch id.</param>
/// <param name="Target">The agent the launch will run.</param>
/// <param name="ChosenBy">'click' when the person picked the agent, 'default' when the remembered one was taken.</param>
/// <param name="Warnings">What the person should know: 'budget-soft' when the agent is past its soft limit.</param>
public sealed record LaunchRequested(CommandAccepted Accepted, LaunchId Launch, AgentId Target, string ChosenBy, IReadOnlyList<string> Warnings);
