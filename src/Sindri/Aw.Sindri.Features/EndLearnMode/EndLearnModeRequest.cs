using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.EndLearnMode;

/// <summary>End network learn mode early (POST /sindri/sandbox/learn-off, 'sindri sandbox learn off'; sandbox spec §3.6): desk class, appends 'sandbox.learn-ended'. A tightening, so it needs no approval.</summary>
/// <param name="Repo">The repo to end it for; null ends it for every repo.</param>
public sealed record EndLearnModeRequest(RepoName? Repo = null) : ICommand<CommandAccepted>;
