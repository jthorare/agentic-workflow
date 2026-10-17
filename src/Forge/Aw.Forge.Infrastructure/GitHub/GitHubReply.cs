using System.Text.Json;

namespace Aw.Forge.Infrastructure.GitHub;

/// <summary>An HTTP answer that wasn't a transport failure, a 401, a rate limit or a 5xx: the status, the parsed body and the next page.</summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Body">The JSON body, or a null-kind element when there was none.</param>
/// <param name="Next">The next page's URL from the Link header.</param>
internal sealed record GitHubReply(int Status, JsonElement Body, Uri? Next)
{
    internal bool Ok => Status is >= 200 and < 300;
}
