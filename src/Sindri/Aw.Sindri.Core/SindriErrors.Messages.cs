using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-MSG-011: the conversation is not in the store (messages spec §10); `sindri messages status`, or open it once to load.</summary>
    public static readonly ErrorDefinition MessagesConversationUnknown = new("SND-MSG-011", ErrorKind.NotFound, "The conversation is not in the store");

    /// <summary>SND-MSG-025: messages.db is newer than this build, or damaged (messages spec §10); `sindri messages doctor`, and `purge` rebuilds the cache.</summary>
    public static readonly ErrorDefinition MessagesStoreUnusable = new("SND-MSG-025", ErrorKind.Unavailable, "messages.db is newer than this build, or damaged");
}
