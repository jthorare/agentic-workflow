using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SkipSetupStep;

/// <summary>
/// Skip or un-skip a setup step that is allowed to be skipped (first-run spec §3.1): `sources` (the accounts question, `setup.skipAccounts`) or `first-workflow` (the dismissed hello-sindri row,
/// `setup.skipFirstWorkflow`). A skipped step counts as done. Any caller may do it, since a skip is undone in one click.
/// </summary>
/// <param name="Step">Which step: <see cref="SetupStepId.Sources"/> or <see cref="SetupStepId.FirstWorkflow"/>.</param>
/// <param name="Skipped">True to skip it, false to undo the skip.</param>
internal sealed record SkipSetupStepRequest(SetupStepId Step, bool Skipped) : ICommand<SkipSetupStepResponse>;
