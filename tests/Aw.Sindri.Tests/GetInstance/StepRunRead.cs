using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>Reads an instance's workflow events, as the source under test does.</summary>
/// <param name="address">The instance.</param>
/// <param name="cancellationToken">Cancels the read.</param>
/// <returns>The events, or the read error.</returns>
public delegate ValueTask<Result<IReadOnlyList<RecordedWorkflowEvent>>> StepRunRead(InstanceAddress address, CancellationToken cancellationToken);
