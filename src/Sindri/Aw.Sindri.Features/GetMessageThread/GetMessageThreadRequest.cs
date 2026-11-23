using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.GetMessageThread;

/// <summary>A thread: its root, replies, linked rows and owner state (GET /sindri/messages/threads/{conversation}/{ts}, 'sindri messages thread'; messages spec §8.2).</summary>
/// <param name="Conversation">The Slack conversation.</param>
/// <param name="Thread">The thread root's ts.</param>
public sealed record GetMessageThreadRequest(string Conversation, MessageTs Thread) : IQuery<GetMessageThreadResponse>;
