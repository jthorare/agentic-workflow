namespace Aw.Context.Testing.Fakes;

/// <summary>What a fake's handler sees of a request. The Authorization value is here for the token check only.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The path, without the query.</param>
/// <param name="Query">The query parameters (last value wins).</param>
/// <param name="Authorization">The Authorization header, or null.</param>
/// <param name="Body">The body as UTF-8 text.</param>
internal sealed record FakeHttpRequest(
    string Method, string Path, IReadOnlyDictionary<string, string> Query, string? Authorization, string Body)
{
    /// <summary>The method and path only: the generated ToString would print Authorization (spec §14 S-L2).</summary>
    /// <returns>A description without the credential or the body.</returns>
    public override string ToString() =>
        $"FakeHttpRequest {{ Method = {Method}, Path = {Path}, Authorization = {(Authorization is null ? "(none)" : "(withheld)")} }}";
}
