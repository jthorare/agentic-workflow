using Aw.Context.Core.Audience;
using Aw.Context.Core.Records;
using Aw.Kernel;

namespace Aw.Context.Core.Search;

/// <summary>The lexical leg over the record store (spec §6.1): full-text search of the chunks, under the caller's audience mask.</summary>
public interface ILexicalLeg
{
    /// <summary>The default number of chunks one search takes before the best chunk per record is kept (spec §6.1).</summary>
    public const int DefaultLimit = 50;

    /// <summary>Searches the chunks of the records the scope lets through: not removed, not blocked, audience allowed.</summary>
    /// <param name="terms">Plain query terms; each is quoted, none is read as query syntax.</param>
    /// <param name="scope">The audiences the caller may read; a record with none is never returned.</param>
    /// <param name="limit">At most this many chunks are taken before one chunk per record is kept, 1 to 1,000.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The answer; a store that can't be read is an error ('SND-CONTEXT-004').</returns>
    public ValueTask<Result<LexicalAnswer>> Search(IReadOnlyList<string> terms, AudienceScope scope, int limit, CancellationToken cancellationToken);
}
