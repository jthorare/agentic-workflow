using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the pick is given (model-pick spec 5): the run, where it runs, the launch request built without a model (its id, item and brief), and the files the question shows.</summary>
/// <param name="Run">The run, with its floor, recipe and any earlier failed run.</param>
/// <param name="Target">Where the Step runs.</param>
/// <param name="Request">The launch request built with no model.</param>
/// <param name="Files">The pack's files and the written facts.</param>
/// <param name="Drafter">For a challenger Step, the drafter's launch (#1008); null for any other Step.</param>
internal sealed record ModelPickWork(AgentLaunchRun Run, AgentStepTarget Target, LaunchRequest Request, IReadOnlyList<PickFileText> Files, DrafterModel? Drafter = null);
