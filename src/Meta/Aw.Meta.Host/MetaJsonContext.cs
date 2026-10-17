using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;
using Aw.Meta.Features.CheckMerge;
using Aw.Meta.Features.GateRerun;
using Aw.Meta.Features.GateRun;
using Aw.Meta.Features.Health;
using Aw.Meta.Features.Inbox;
using Aw.Meta.Features.InboxDefer;
using Aw.Meta.Features.InboxDismiss;
using Aw.Meta.Features.LaneInbox;
using Aw.Meta.Features.LaneReply;
using Aw.Meta.Features.Lanes;
using Aw.Meta.Features.LaneSend;
using Aw.Meta.Features.LaneStopCheck;
using Aw.Meta.Features.LogAppend;
using Aw.Meta.Features.MergeRun;
using Aw.Meta.Features.OutboxRecord;
using Aw.Meta.Features.Owner;
using Aw.Meta.Features.OwnerAnswer;
using Aw.Meta.Features.OwnerAsk;
using Aw.Meta.Features.QueueDrop;
using Aw.Meta.Features.Roles;
using Aw.Meta.Features.Status;
using Aw.Meta.Features.StopCheck;
using Aw.Meta.Features.SyncInbox;
using Aw.Meta.Features.Team;
using Aw.Meta.Features.Watch;
using Aw.Meta.Features.WorktreeList;
using Aw.Meta.Features.WorktreeSweep;

namespace Aw.Meta.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(TeamConfig))]
[JsonSerializable(typeof(MetaRole))]
[JsonSerializable(typeof(BuildInfo))]
[JsonSerializable(typeof(TeamListResponse))]
[JsonSerializable(typeof(TeamMigration))]
[JsonSerializable(typeof(ShowRoleResponse))]
[JsonSerializable(typeof(ListInboxResponse))]
[JsonSerializable(typeof(DismissInboxResponse))]
[JsonSerializable(typeof(DeferInboxResponse))]
[JsonSerializable(typeof(LanesResponse))]
[JsonSerializable(typeof(OutboxRecordResponse))]
[JsonSerializable(typeof(ListOwnerResponse))]
[JsonSerializable(typeof(AnswerOwnerResponse))]
[JsonSerializable(typeof(AskOwnerResponse))]
[JsonSerializable(typeof(AppendLogResponse))]
[JsonSerializable(typeof(QueueDropResponse))]
[JsonSerializable(typeof(StatusResponse))]
[JsonSerializable(typeof(StopCheckResponse))]
[JsonSerializable(typeof(GateRunResponse))]
[JsonSerializable(typeof(GateRerunResponse))]
[JsonSerializable(typeof(CheckMergeResponse))]
[JsonSerializable(typeof(MergeRunResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(WatchResponse))]
[JsonSerializable(typeof(ListWorktreesResponse))]
[JsonSerializable(typeof(SweepWorktreesResponse))]
[JsonSerializable(typeof(LaneSendResponse))]
[JsonSerializable(typeof(LaneInboxResponse))]
[JsonSerializable(typeof(LaneStopCheckResponse))]
[JsonSerializable(typeof(LaneReplyResponse))]
[JsonSerializable(typeof(SyncInboxRequest))]
[JsonSerializable(typeof(SyncInboxResponse))]
internal sealed partial class MetaJsonContext : JsonSerializerContext;
