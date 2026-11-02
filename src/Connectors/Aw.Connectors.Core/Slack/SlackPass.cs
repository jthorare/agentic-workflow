namespace Aw.Connectors.Core.Slack;

/// <summary>
/// Where a Slack read stands inside one channel: the pass that walks the channel's history newest to oldest in bounded chunks. The channel's watermark moves to <paramref name="Top"/> only when the pass ends.
/// </summary>
/// <param name="Top">The newest root ts the pass saw (its first page); it becomes the watermark when the pass ends.</param>
/// <param name="Below">The exclusive history bound of the current chunk (a day start): the chunk is read from here downwards. Null is the top of the range.</param>
/// <param name="ThreadsBelow">When set, the chunk's day records are already emitted and only thread roots older than this ts are left to read. Null means the chunk has not been emitted yet.</param>
public sealed record SlackPass(string Top, string? Below, string? ThreadsBelow);
