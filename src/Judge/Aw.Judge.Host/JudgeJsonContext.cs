using System.Text.Json.Serialization;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.PromptSort;
using Aw.Judge.Core.Questions;
using Aw.Judge.Features.ApproveDecision;
using Aw.Judge.Features.CheckTraits;
using Aw.Judge.Features.ExplainDecision;
using Aw.Judge.Features.GetBrief;
using Aw.Judge.Features.GetConfig;
using Aw.Judge.Features.GetHealth;
using Aw.Judge.Features.MapBriefByDispatch;
using Aw.Judge.Features.MapBriefByName;
using Aw.Judge.Features.SaveBrief;
using Aw.Judge.Features.SetBriefAgentId;
using Aw.Judge.Features.SetConfig;
using Aw.Judge.Features.SetPromptSort;
using Aw.Judge.Features.SortPrompt;
using Aw.Judge.Features.UndoDecision;
using Aw.Judge.Host.Commands;
using Aw.Kernel;

namespace Aw.Judge.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(HealthOutput))]
[JsonSerializable(typeof(BriefOutput))]
[JsonSerializable(typeof(DecisionOutput))]
[JsonSerializable(typeof(JudgeConfig))]
[JsonSerializable(typeof(IReadOnlyList<SkippedProvider>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, double>))]
[JsonSerializable(typeof(WakeGateInput))]
[JsonSerializable(typeof(BriefScopeInput))]
[JsonSerializable(typeof(AskCheckInput))]
[JsonSerializable(typeof(RuleCheckInput))]
[JsonSerializable(typeof(ResolutionCheckInput))]
[JsonSerializable(typeof(TurnProgressInput))]
[JsonSerializable(typeof(UiElementRepairInput))]
[JsonSerializable(typeof(VisualCritiqueInput))]
[JsonSerializable(typeof(ChooseInput))]
[JsonSerializable(typeof(DecidedEnvelope))]
[JsonSerializable(typeof(EscalatedEnvelope))]
[JsonSerializable(typeof(GetHealthRequest))]
[JsonSerializable(typeof(GetHealthResponse))]
[JsonSerializable(typeof(BriefInput))]
[JsonSerializable(typeof(SaveBriefRequest))]
[JsonSerializable(typeof(SaveBriefResponse))]
[JsonSerializable(typeof(GetBriefRequest))]
[JsonSerializable(typeof(GetBriefResponse))]
[JsonSerializable(typeof(MapBriefByNameRequest))]
[JsonSerializable(typeof(MapBriefByNameResponse))]
[JsonSerializable(typeof(MapBriefByDispatchRequest))]
[JsonSerializable(typeof(MapBriefByDispatchResponse))]
[JsonSerializable(typeof(SetBriefAgentIdRequest))]
[JsonSerializable(typeof(SetBriefAgentIdResponse))]
[JsonSerializable(typeof(ExplainDecisionRequest))]
[JsonSerializable(typeof(ExplainDecisionResponse))]
[JsonSerializable(typeof(UndoDecisionRequest))]
[JsonSerializable(typeof(UndoDecisionResponse))]
[JsonSerializable(typeof(ApproveDecisionRequest))]
[JsonSerializable(typeof(ApproveDecisionResponse))]
[JsonSerializable(typeof(GetConfigRequest))]
[JsonSerializable(typeof(GetConfigResponse))]
[JsonSerializable(typeof(SetConfigRequest))]
[JsonSerializable(typeof(SetConfigResponse))]
// The cutover verbs (6.0 declares, 6.1 and 6.2 fill): the sorter's hook record and files, the scaffold switch, trait-check.
[JsonSerializable(typeof(SortPromptInput))]
[JsonSerializable(typeof(SortPromptOutput))]
[JsonSerializable(typeof(SortPromptRequest))]
[JsonSerializable(typeof(SortPromptResponse))]
[JsonSerializable(typeof(SetPromptSortRequest))]
[JsonSerializable(typeof(SetPromptSortResponse))]
[JsonSerializable(typeof(ScaffoldSwitch))]
[JsonSerializable(typeof(SortSession))]
[JsonSerializable(typeof(PromptSortRecord))]
[JsonSerializable(typeof(CheckTraitsRequest))]
[JsonSerializable(typeof(CheckTraitsResponse))]
[JsonSerializable(typeof(TraitFinding))]
internal sealed partial class JudgeJsonContext : JsonSerializerContext;
