using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Features.ListOutbox;

/// <summary>
/// 'sindri outbox list [--pending | --dispatched] [--json]' and 'GET /outbox?state=' (engine spec §6.3; engine P6 unit
/// (c)): the effects, read from their ledger rows (§5.6, §5.7), not from Kernel's 'outbox_commands'.
/// </summary>
/// <param name="State">Only the effects whose latest row is in this state; null for every effect.</param>
public sealed record ListOutboxRequest(EffectState? State = null) : IQuery<ListOutboxResponse>;
